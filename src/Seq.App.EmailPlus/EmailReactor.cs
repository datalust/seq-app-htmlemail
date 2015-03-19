using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Handlebars;
using Newtonsoft.Json;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.EmailPlus
{
    [SeqApp("Email+",
        Description = "Uses a Handlebars template to send events as SMTP email.")]
    public class EmailReactor : Reactor, ISubscribeTo<LogEventData>
    {
        private readonly IMailClientFactory _mailClientFactory;

        readonly Lazy<Func<object,string>> _bodyTemplate, _subjectTemplate;

        readonly Lazy<Subject<Event<LogEventData>>> _messages;

        const string DefaultSubjectTemplate = @"[{{$Events.[0].$Level}}] {{{$Events.[0].$Message}}} (via Seq){{#if $MultipleEvents}} ({{$EventCount}}){{/if}}";

        public EmailReactor(IMailClientFactory mailClientFactory = null)
        {
            Handlebars.Handlebars.RegisterHelper("pretty", PrettyPrint);

            _subjectTemplate = new Lazy<Func<object, string>>(() =>
            {
                var subjectTemplate = SubjectTemplate;
                if (string.IsNullOrEmpty(subjectTemplate))
                    subjectTemplate = DefaultSubjectTemplate;
                return Handlebars.Handlebars.Compile(subjectTemplate);                
            });

            _bodyTemplate = new Lazy<Func<object, string>>(() =>
            {
                var bodyTemplate = BodyTemplate;
                if (string.IsNullOrEmpty(bodyTemplate))
                    bodyTemplate = Resources.DefaultBodyTemplate;
                return Handlebars.Handlebars.Compile(bodyTemplate);
            });

            _mailClientFactory = mailClientFactory ?? new SmtpMailClientFactory();

            _messages = new Lazy<Subject<Event<LogEventData>>>(() =>
            {
                var messageSubject = new Subject<Event<LogEventData>>();
                var sendDelay = TimeSpan.FromSeconds(BatchDuplicateSubjectsDelay ?? 0);

                messageSubject.GroupByUntil(evt => FormatSubject(new[] {evt}).GetHashCode(),
                    group =>
                    {
                        var maxAmount = BatchMaxAmount ?? int.MaxValue;
                        Debug.WriteLine("Starting group [{0}] at [{1:O}] with max [{2}] events and [{3}]s delay.",
                            group.Key, DateTime.UtcNow, maxAmount, sendDelay.TotalSeconds);
                        return group.Skip(maxAmount - 1).Merge(group.Throttle(sendDelay)).Take(1).Do(
                            _ =>
                                Debug.WriteLine("Ending group [{0}] at [{1:O}].",
                                    group.Key,
                                    DateTime.UtcNow));
                    })
                    .Subscribe(group =>
                    {
                        var tokenSource = new CancellationTokenSource();
                        var events = new List<Event<LogEventData>>();

                        group.Subscribe(evt =>
                        {
                            Debug.WriteLine("Event [{0}] received on group [{1}] at [{2:O}].",
                                evt.Id, group.Key, DateTime.UtcNow);
                            events.Add(evt);
                        }, () =>
                        {
                            Debug.WriteLine("Sending [{0}] events from group [{1}].", events.Count, group.Key);
                            Send(events);
                            tokenSource.Cancel();
                        }, tokenSource.Token);
                    });

                return messageSubject;
            });
        }

        [SeqAppSetting(
            DisplayName = "From address",
            HelpText = "The account from which the email is being sent.")]
        public string From { get; set; }
        
        [SeqAppSetting(
            DisplayName = "To address",
            HelpText = "The account to which the email is being sent.")]
        public string To { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Subject template",
            HelpText = "The subject of the email, using Handlebars syntax. If blank, a default subject will be generated.")]
        public string SubjectTemplate { get; set; }

        [SeqAppSetting(
            HelpText = "The name of the SMTP server machine.")]
        new public string Host { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            HelpText = "The port on the SMTP server machine to send mail to. Leave this blank to use the standard port (25).")]
        public int? Port { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Enable SSL",
            HelpText = "Check this box if SSL is required to send email messages.")]
        public bool? EnableSsl { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            InputType = SettingInputType.LongText,
            DisplayName = "Body template",
            HelpText = "The template to use when generating the email body, using Handlebars.NET syntax. Leave this blank to use " +
                       "the default template that includes the message and properties (https://github.com/continuousit/seq-apps/tree/master/src/Seq.App.EmailPlus/Resources/DefaultBodyTemplate.html).")]
        public string BodyTemplate { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            HelpText = "The username to use when authenticating to the SMTP server, if required.")]
        public string Username { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            InputType = SettingInputType.Password,
            HelpText = "The password to use when authenticating to the SMTP server, if required.")]
        public string Password { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            HelpText = "The amount of time in seconds to wait for a subsequent event with the same subject to send as a single batch email.")]
        public double? BatchDuplicateSubjectsDelay { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            HelpText = "The maximum number of events to include in a batch.")]
        public int? BatchMaxAmount { get; set; }

        public void On(Event<LogEventData> evt)
        {
            _messages.Value.OnNext(evt);
        }

        void Send(ICollection<Event<LogEventData>> events)
        {
            if (events.Count < 1)
                return;

            var message = BuildMessage(events);
            using (var client = _mailClientFactory.Create(Host, Port ?? 25, EnableSsl ?? false))
            {
                if (!string.IsNullOrWhiteSpace(Username))
                    client.Credentials = new NetworkCredential(Username, Password);

                Debug.WriteLine("Sending message with subject: " + message.Subject);
                client.Send(message);
            }
        }

        MailMessage BuildMessage(ICollection<Event<LogEventData>> events)
        {
            var subject = FormatSubject(events);
            if (subject.Length > 130)
                subject = subject.Substring(0, 130);

            return new MailMessage(From, To)
            {
                Subject = subject,
                Body = FormatTemplate(_bodyTemplate.Value, events)
            };
        }

        string FormatSubject(ICollection<Event<LogEventData>> events)
        {
            return FormatTemplate(_subjectTemplate.Value, events).Trim().Replace("\r", "").Replace("\n", "");
        }

        string FormatTemplate(Func<object, string> template, ICollection<Event<LogEventData>> events)
        {
            var payload = ToDynamic(new Dictionary<string, object>
            {
                {"$Instance", base.Host.InstanceName},
                {"$ServerUri", base.Host.ListenUris.FirstOrDefault()},
                {"$MultipleEvents", events.Count > 1},
                {"$EventCount", events.Count},
                {
                    "$Events", events.Select(evt => new Dictionary<string, object>
                    {
                        {"$Id", evt.Id},
                        {"$UtcTimestamp", evt.TimestampUtc},
                        {"$LocalTimestamp", evt.Data.LocalTimestamp},
                        {"$Level", evt.Data.Level},
                        {"$MessageTemplate", evt.Data.MessageTemplate},
                        {"$Message", evt.Data.RenderedMessage},
                        {"$Exception", evt.Data.Exception},
                        {"$Properties", ToDynamic(evt.Data.Properties)},
                        {"$EventType", "$" + evt.EventType.ToString("X8")}
                    })
                }
            });
            
            return template(payload);
        }

        void PrettyPrint(TextWriter output, object context, object[] arguments)
        {
            var value = arguments.FirstOrDefault();
            if (value == null)
                output.WriteSafeString("null");
            else if (value is IEnumerable<object> || value is IEnumerable<KeyValuePair<string, object>>)
                output.WriteSafeString(JsonConvert.SerializeObject(FromDynamic(value)));
            else
                output.WriteSafeString(value.ToString());
        }

        static object FromDynamic(object o)
        {
            var dictionary = o as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                return dictionary.ToDictionary(kvp => kvp.Key, kvp => FromDynamic(kvp.Value));
            }

            var enumerable = o as IEnumerable<object>;
            if (enumerable != null)
            {
                return enumerable.Select(FromDynamic).ToArray();
            }

            return o;
        }

        static object ToDynamic(object o)
        {
            var dictionary = o as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                var result = new ExpandoObject();
                var asDict = (IDictionary<string, object>) result;
                foreach (var kvp in dictionary)
                    asDict.Add(kvp.Key, ToDynamic(kvp.Value));
                return result;
            }

            var enumerable = o as IEnumerable<object>;
            if (enumerable != null)
            {
                return enumerable.Select(ToDynamic).ToArray();
            }

            return o;
        }
    }
}
