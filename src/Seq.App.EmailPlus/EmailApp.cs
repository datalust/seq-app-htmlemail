using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using HandlebarsDotNet;
using MailKit.Security;
using MimeKit;
using Seq.Apps;
using Seq.Apps.LogEvents;

// ReSharper disable UnusedAutoPropertyAccessor.Global, MemberCanBePrivate.Global

namespace Seq.App.EmailPlus
{
    using Template = HandlebarsTemplate<object, object>;

    [SeqApp("HTML Email",
        Description = "Uses Handlebars templates to format events and notifications into HTML email.")]
    public class EmailApp : SeqApp, ISubscribeToAsync<LogEventData>
    {
        readonly IMailGateway _mailGateway;
        readonly IClock _clock;
        readonly Dictionary<uint, DateTime> _suppressions = new Dictionary<uint, DateTime>();
        Template _bodyTemplate, _subjectTemplate, _toAddressesTemplate;
        SmtpOptions _options;

        const string DefaultSubjectTemplate = @"[{{$Level}}] {{{$Message}}} (via Seq)";
        const int MaxSubjectLength = 130;
        const int DefaultPort = 25;
        const int DefaultSslPort = 465;

        static EmailApp()
        {
            HandlebarsHelpers.Register();
        }

        internal EmailApp(IMailGateway mailGateway, IClock clock)
        {
            _mailGateway = mailGateway ?? throw new ArgumentNullException(nameof(mailGateway));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public EmailApp()
            : this(new DirectMailGateway(), new SystemClock())
        {
        }

        [SeqAppSetting(
            DisplayName = "From address",
            HelpText = "The account from which the email is being sent.")]
        public string From { get; set; }

        [SeqAppSetting(
            DisplayName = "To address",
            HelpText = "The account to which the email is being sent. Multiple addresses are separated by a comma. Handlebars syntax is supported.")]
        public string To { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Subject template",
            HelpText = "The subject of the email, using Handlebars syntax. If blank, a default subject will be generated.")]
        public string SubjectTemplate { get; set; }

        [SeqAppSetting(
            HelpText = "The name of the SMTP server machine.")]
        public new string Host { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            HelpText = "The port on the SMTP server machine to send mail to. Leave this blank to use the standard port (25).")]
        public int? Port { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Require TLS",
            HelpText = "Check this box to require that the server supports SSL/TLS for sending messages. If the port used is 465, " +
                       "implicit SSL will be enabled; otherwise, the STARTTLS extension will be used.")]
        public bool? EnableSsl { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            InputType = SettingInputType.LongText,
            DisplayName = "Body template",
            HelpText = "The template to use when generating the email body, using Handlebars syntax. Leave this blank to use " +
                       "the default template that includes the message and " +
                       "properties (https://github.com/datalust/seq-app-htmlemail/blob/main/src/Seq.App.EmailPlus/Resources/DefaultBodyTemplate.html).")]
        public string BodyTemplate { get; set; }

        [SeqAppSetting(
            DisplayName = "Suppression time (minutes)",
            IsOptional = true,
            HelpText = "Once an event type has been sent, the time to wait before sending again. The default is zero.")]
        public int SuppressionMinutes { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            HelpText = "The username to use when authenticating to the SMTP server, if required.")]
        public string Username { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            InputType = SettingInputType.Password,
            HelpText = "The password to use when authenticating to the SMTP server, if required.")]
        public string Password { get; set; }
        
        protected override void OnAttached()
        {
            var port = Port ?? DefaultPort;
            _options = _options = new SmtpOptions(
                Host,
                port,
                EnableSsl ?? false
                    ? RequireSslForPort(port)
                    : SecureSocketOptions.StartTlsWhenAvailable,
                Username,
                Password);

            _subjectTemplate = Handlebars.Compile(string.IsNullOrEmpty(SubjectTemplate) 
                ? DefaultSubjectTemplate 
                : SubjectTemplate);
            _bodyTemplate = Handlebars.Compile(string.IsNullOrEmpty(BodyTemplate) 
                ? Resources.DefaultBodyTemplate 
                : BodyTemplate);
            _toAddressesTemplate = string.IsNullOrEmpty(To) ? (_, __) => To : Handlebars.Compile(To);
        }

        public async Task OnAsync(Event<LogEventData> evt)
        {
            if (ShouldSuppress(evt)) return;

            var to = FormatTemplate(_toAddressesTemplate, evt, base.Host)
                .Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries);

            if (to.Length == 0)
            {
                Log.Warning("Email 'to' address template did not evaluate to one or more recipient addresses");
                return;
            }

            var body = FormatTemplate(_bodyTemplate, evt, base.Host);
            var subject = FormatTemplate(_subjectTemplate, evt, base.Host).Trim().Replace("\r", "")
                .Replace("\n", "");
            if (subject.Length > MaxSubjectLength)
                subject = subject.Substring(0, MaxSubjectLength);

            await _mailGateway.SendAsync(
                _options,
                new MimeMessage(
                    new[] {MailboxAddress.Parse(From)},
                    to.Select(MailboxAddress.Parse),
                    subject,
                    new BodyBuilder {HtmlBody = body}.ToMessageBody()));
        }

        bool ShouldSuppress(Event<LogEventData> evt)
        {
            if (SuppressionMinutes == 0)
                return false;

            var now = _clock.UtcNow;
            if (!_suppressions.TryGetValue(evt.EventType, out var suppressedSince) ||
                suppressedSince.AddMinutes(SuppressionMinutes) < now)
            {
                // Not suppressed, or suppression expired

                // Clean up old entries
                var expired = _suppressions.FirstOrDefault(kvp => kvp.Value.AddMinutes(SuppressionMinutes) < now);
                while (expired.Value != default)
                {
                    _suppressions.Remove(expired.Key);
                    expired = _suppressions.FirstOrDefault(kvp => kvp.Value.AddMinutes(SuppressionMinutes) < now);
                }

                // Start suppression again
                suppressedSince = now;
                _suppressions[evt.EventType] = suppressedSince;
                return false;
            }

            // Suppressed
            return true;
        }

        internal static SecureSocketOptions RequireSslForPort(int port)
        {
            return (port == DefaultSslPort ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
        }
        
        internal static string FormatTemplate(Template template, Event<LogEventData> evt, Host host)
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
                { "$ServerUri",           host.BaseUri },
                // Note, this will only be valid when events are streamed directly to the app, and not when the app is sending an alert notification.
                { "$EventUri",            string.Concat(host.BaseUri, "#/events?filter=@Id%20%3D%20'", evt.Id, "'&amp;show=expanded") } 
            });

            foreach (var property in properties)
            {
                payload[property.Key] = property.Value;
            }

            return template(payload);
        }

        static object ToDynamic(object o)
        {
            if (o is IEnumerable<KeyValuePair<string, object>> dictionary)
            {
                var result = new ExpandoObject();
                var asDict = (IDictionary<string, object>) result;
                foreach (var kvp in dictionary)
                    asDict.Add(kvp.Key, ToDynamic(kvp.Value));
                return result;
            }

            if (o is IEnumerable<object> enumerable)
            {
                return enumerable.Select(ToDynamic).ToArray();
            }

            return o;
        }
    }
}
