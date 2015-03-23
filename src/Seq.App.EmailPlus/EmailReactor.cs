using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reactive.Concurrency;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.EmailPlus
{
    [SeqApp("Email+", Description = "Uses a Handlebars template to send events as SMTP email.")]
    public class EmailReactor : Reactor, ISubscribeTo<LogEventData>
    {
        private readonly IScheduler _scheduler;
        readonly IMailClientFactory _mailClientFactory;
        readonly IEmailFormatterFactory _emailFormatterFactory;
        readonly IBatchingStreamFactory<string,Event<LogEventData>> _eventStreamFactory;
        
        IEmailFormatter _formatter;
        IBatchingStream<Event<LogEventData>> _eventStream;
        const int MaxSubjectLength = 130;

        public EmailReactor()
            : this(new SmtpMailClientFactory(), new EmailFormatterFactory(), new BatchingStreamFactory<string, Event<LogEventData>>(), Scheduler.Default)
        {}

        public EmailReactor(IMailClientFactory mailClientFactory = null, IEmailFormatterFactory emailFormatterFactory = null, IBatchingStreamFactory<string,Event<LogEventData>> batchingStreamFactory = null, IScheduler scheduler = null)
        {
            _mailClientFactory = mailClientFactory;
            _emailFormatterFactory = emailFormatterFactory;
            _eventStreamFactory = batchingStreamFactory;
            _scheduler = scheduler;
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
        public double? BatchDelay { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            HelpText = "The maximum number of events to include in a batch. This setting requires the BatchDelay setting to be set.")]
        public int? BatchMaxAmount { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            HelpText = "The maximum amount of time in seconds to wait while building a batch email. This setting requires the BatchDelay setting to be set.")]
        public double? BatchMaxDelay { get; set; }

        protected override void OnAttached()
        {
            base.OnAttached();

            _formatter = _emailFormatterFactory.Create(
                base.Host.InstanceName, base.Host.ListenUris.FirstOrDefault(), BodyTemplate, SubjectTemplate, MaxSubjectLength);

            var delay = BatchDelay.HasValue ? TimeSpan.FromSeconds(BatchDelay.Value) : (TimeSpan?) null;
            var maxDelay = BatchMaxDelay.HasValue ? TimeSpan.FromSeconds(BatchMaxDelay.Value) : (TimeSpan?) null;
            _eventStream = _eventStreamFactory.Create(
                evt => _formatter.FormatSubject(new[] {evt}), _scheduler ?? Scheduler.Default, delay, maxDelay, BatchMaxAmount);
            _eventStream.Batches.Subscribe(Send, ex => Debug.WriteLine(ex));
        }

        public void On(Event<LogEventData> evt)
        {
            _eventStream.Add(evt);
        }

        void Send(ICollection<Event<LogEventData>> events)
        {
            if (events.Count < 1)
                return;

            using (var client = _mailClientFactory.Create(Host, Port ?? 25, EnableSsl ?? false))
            {
                if (!string.IsNullOrWhiteSpace(Username))
                    client.Credentials = new NetworkCredential(Username, Password);

                client.Send(BuildMessage(events));
            }
        }

        MailMessage BuildMessage(ICollection<Event<LogEventData>> events)
        {
            return new MailMessage(From, To) {Subject = _formatter.FormatSubject(events), Body = _formatter.FormatBody(events)};
        }
    }
}
