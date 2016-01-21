using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
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
        readonly Lazy<Func<object,string>> _bodyTemplate, _subjectTemplate;

        const string DefaultSubjectTemplate = @"[{{$Level}}] {{{$Message}}} (via Seq)";
        const int MaxSubjectLength = 130;

        static EmailReactor()
        {
            Handlebars.Handlebars.RegisterHelper("pretty", PrettyPrint);
        }

        public EmailReactor()
        {
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

        public void On(Event<LogEventData> evt)
        {
            var body = FormatTemplate(_bodyTemplate.Value, evt, base.Host);
            var subject = FormatTemplate(_subjectTemplate.Value, evt, base.Host).Trim().Replace("\r", "").Replace("\n", "");
            if (subject.Length > MaxSubjectLength)
                subject = subject.Substring(0, MaxSubjectLength);

            var client = new SmtpClient(Host, Port ?? 25) {EnableSsl = EnableSsl ?? false};
            if (!string.IsNullOrWhiteSpace(Username))
                client.Credentials = new NetworkCredential(Username, Password);

            var message = new MailMessage(From, To, subject, body) {IsBodyHtml = true};

            client.Send(message);
        }

        public static string FormatTemplate(Func<object, string> template, Event<LogEventData> evt, Host host)
        {
            var properties = (IDictionary<string,object>) ToDynamic(evt.Data.Properties ?? new Dictionary<string, object>());

            var payload = (IDictionary<string,object>) ToDynamic(new Dictionary<string, object>
            {
                { "$Id",                  evt.Id },
                { "$UtcTimestamp",        evt.TimestampUtc },
                { "$LocalTimestamp",      evt.Data.LocalTimestamp },
                { "$Level",               evt.Data.Level },
                { "$MessageTemplate",     evt.Data.MessageTemplate },
                { "$Message",             evt.Data.RenderedMessage },
                { "$Exception",           evt.Data.Exception },
                { "$Properties",          properties },
                { "$EventType",           "$" + evt.EventType.ToString("X8") },
                { "$Instance",            host.InstanceName },
                { "$ServerUri",           host.ListenUris.FirstOrDefault() }
            });

            foreach (var property in properties)
            {
                payload[property.Key] = property.Value;
            }
            
            return template(payload);
        }

        static void PrettyPrint(TextWriter output, object context, object[] arguments)
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
