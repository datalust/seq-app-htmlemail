using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using HandlebarsDotNet;
using MimeKit;
using Seq.Apps;
using Seq.Apps.LogEvents;

// ReSharper disable UnusedAutoPropertyAccessor.Global, MemberCanBePrivate.Global

namespace Seq.App.EmailPlus
{
    using Template = HandlebarsTemplate<object, object>;

    [SeqApp("HTML Email",
        Description = "Uses Handlebars templates to format events and notifications into HTML email, with optional alternate plain text body.")]
    public class EmailApp : SeqApp, ISubscribeToAsync<LogEventData>
    {
        readonly IMailGateway _mailGateway;
        readonly IClock _clock;
        readonly Dictionary<uint, DateTime> _suppressions = new Dictionary<uint, DateTime>();
        readonly Lazy<Template> _bodyTemplate, _plainTextTemplate, _subjectTemplate, _toAddressesTemplate, _replyToAddressesTemplate, _ccAddressesTemplate, _bccAddressesTemplate;
        readonly Lazy<SmtpOptions> _options;

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

            var port = Port ?? DefaultPort;
            _options = new Lazy<SmtpOptions>(() => new SmtpOptions(
                Host,
                DeliverUsingDns != null && (bool) DeliverUsingDns,
                port,
				Priority,				
				DefaultPriority,
                SmtpOptions.GetSocketOptions(port, EnableSsl, UseTlsWhenAvailable),
                Username,
                Password));

            _subjectTemplate = new Lazy<Template>(() =>
            {
                var subjectTemplate = SubjectTemplate;
                if (string.IsNullOrEmpty(subjectTemplate))
                    subjectTemplate = DefaultSubjectTemplate;
                return Handlebars.Compile(subjectTemplate);
            });

            _bodyTemplate = new Lazy<Template>(() =>
            {
                var bodyTemplate = BodyTemplate;
                if (string.IsNullOrEmpty(bodyTemplate))
                    bodyTemplate = Resources.DefaultBodyTemplate;
                return Handlebars.Compile(bodyTemplate);
            });

            _plainTextTemplate = new Lazy<Template>(() =>
            {
                var plainTextTemplate = PlainTextTemplate;
                if (string.IsNullOrEmpty(plainTextTemplate))
                    plainTextTemplate = Resources.DefaultBodyTemplate;
                return Handlebars.Compile(plainTextTemplate);
            });

            _toAddressesTemplate = new Lazy<Template>(() =>
            {
                var toAddressTemplate = To;
                return string.IsNullOrEmpty(toAddressTemplate) ? (_, __) => To : Handlebars.Compile(toAddressTemplate);
            });

            _replyToAddressesTemplate = new Lazy<Template>(() =>
            {
                var replyToAddressTemplate = ReplyTo;
                return string.IsNullOrEmpty(replyToAddressTemplate) ? (_, __) => ReplyTo : Handlebars.Compile(replyToAddressTemplate);
            });

            _ccAddressesTemplate = new Lazy<Template>(() =>
            {
                var ccAddressTemplate = Cc;
                return string.IsNullOrEmpty(ccAddressTemplate) ? (_, __) => Cc : Handlebars.Compile(ccAddressTemplate);
            });

            _bccAddressesTemplate = new Lazy<Template>(() =>
            {
                var bccAddressTemplate = Bcc;
                return string.IsNullOrEmpty(bccAddressTemplate) ? (_, __) => Bcc : Handlebars.Compile(bccAddressTemplate);
            });
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
            HelpText =
                "The account to which the email is being sent. Multiple addresses are separated by a comma. Handlebars syntax is supported.")]
        public string To { get; set; }

        [SeqAppSetting(
            DisplayName = "ReplyTo address",
            HelpText = "Optional account to which replies will be sent. Multiple addresses are separated by a comma. Handlebars syntax is supported.",
            IsOptional = true)]
        public string ReplyTo { get; set; }

        [SeqAppSetting(
            DisplayName = "CC address",
            HelpText = "Optional account to which emails should be sent as CC. Multiple addresses are separated by a comma. Handlebars syntax is supported.",
            IsOptional = true)]
        public string Cc { get; set; }

        [SeqAppSetting(
            DisplayName = "BCC address",
            HelpText = "Optional account to which the email is being sent as BCC. Multiple addresses are separated by a comma. Handlebars syntax is supported.",
            IsOptional = true)]
        public string Bcc { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Subject template",
            HelpText =
                "The subject of the email, using Handlebars syntax. If blank, a default subject will be generated.")]
        public string SubjectTemplate { get; set; }

        [SeqAppSetting(
            DisplayName = "SMTP Mail Host(s)",
            HelpText =
                "The name of the SMTP server machine. Optionally specify fallback hosts as comma-delimited string. If not specified, Deliver Using DNS should be enabled.",
            IsOptional = true)]
        public new string Host { get; set; }

        [SeqAppSetting(
            DisplayName = "Deliver using DNS",
            HelpText =
                "Deliver directly using DNS. If SMTP Mail Host(s) is configured, this will be used as a fallback delivery mechanism. If not enabled, SMTP Mail Host(s) should be configured.")]
        public bool? DeliverUsingDns { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            HelpText =
                "The port on the SMTP server machine to send mail to. Leave this blank to use the standard port (25).")]
        public int? Port { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Require TLS",
            HelpText = "Check this box to require that the server supports SSL/TLS for sending messages. If the port used is 465," +
                       "implicit SSL will ALWAYS be enabled; otherwise, the STARTTLS extension will be used if Require TLS is enabled. If this is disabled, Use Optional TLS If Available can be configured to allow TLS to be used when offered.")]
        public bool? EnableSsl { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Use Optional TLS if available",
            HelpText = "If Enable SSL is disabled but the host supports TLS, allow Seq to negotiate using TLS. This has no effect if Enable SSL is enabled.")]
        public bool? UseTlsWhenAvailable { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            InputType = SettingInputType.LongText,
            DisplayName = "Body template",
            HelpText = "The template to use when generating the email body, using Handlebars syntax. Leave this blank to use " +
                       "the default template that includes the message and " +
                       "properties (https://github.com/datalust/seq-app-htmlemail/blob/main/src/Seq.App.EmailPlus/Resources/DefaultBodyTemplate.html).")]
        public string BodyTemplate { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            InputType = SettingInputType.LongText,
            DisplayName = "Plain text template",
            HelpText = "Optional plain text template to use when generating the email body, using Handlebars.NET syntax. Leave this blank if disable.")]
        public string PlainTextTemplate { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Email Priority Property",
            HelpText = "Event Property that can be used to map email priority; properties can be mapped to email priority using the Email Priority or Property Mapping field.")]
        public string PriorityProperty { get; set; }
        
        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Email Priority or Property Mapping",
            HelpText = "The Priority of the email - High, Normal, Low - Default Normal, or 'Email Priority Property' mapping using Property=Mapping format, eg. Highest=High,Error=Normal,Low=Low.")]
        public string Priority { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Default Priority",
            HelpText = "If using Email Priority mapping - Default for events not matching the mapping - High, Normal, or Low. Defaults to Normal.")]
        public string DefaultPriority { get; set; }

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
            if (string.IsNullOrEmpty(Host) && (DeliverUsingDns == null || !(bool) DeliverUsingDns))
                throw new Exception("There are no delivery methods selected - you must specify at least one SMTP Mail Host, or enable Deliver Using DNS");
        }

        public async Task OnAsync(Event<LogEventData> evt)
        {
            if (ShouldSuppress(evt)) return;

            var to = FormatTemplate(_toAddressesTemplate.Value, evt, base.Host)
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();

            if (to.Count == 0)
            {
                Log.ForContext("To", _toAddressesTemplate.Value).Error("Email 'to' address template did not evaluate to one or more recipient addresses - email cannot be sent!");
                return;
            }

            var replyTo = string.IsNullOrEmpty(ReplyTo) ? new List<string>() : FormatTemplate(_replyToAddressesTemplate.Value, evt, base.Host)
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();

            var cc = string.IsNullOrEmpty(Cc) ? new List<string>() : FormatTemplate(_ccAddressesTemplate.Value, evt, base.Host)
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();

            var bcc = string.IsNullOrEmpty(Bcc) ? new List<string>() : FormatTemplate(_bccAddressesTemplate.Value, evt, base.Host)
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();


            var body = FormatTemplate(_bodyTemplate.Value, evt, base.Host);
            var textBody = FormatTemplate(_plainTextTemplate.Value, evt, base.Host);
            var subject = FormatTemplate(_subjectTemplate.Value, evt, base.Host).Trim().Replace("\r", "")
                .Replace("\n", "");
            if (subject.Length > MaxSubjectLength)
                subject = subject.Substring(0, MaxSubjectLength);

            var replyToList = replyTo.Select(MailboxAddress.Parse).ToList();
            var toList = to.Select(MailboxAddress.Parse).ToList();
            var ccList = cc.Select(MailboxAddress.Parse).ToList();
            var bccList = bcc.Select(MailboxAddress.Parse).ToList();
            var sent = false;
            var logged = false;
            var type = DeliveryType.None;
            var message = new MimeMessage(
                new List<InternetAddress> {InternetAddress.Parse(From)},
                toList, subject,
                new BodyBuilder
                    {HtmlBody = body, TextBody = textBody == body ? string.Empty : textBody}.ToMessageBody());

            if (replyToList.Any())
                message.ReplyTo.AddRange(replyToList);

            if (ccList.Any())
                message.Cc.AddRange(ccList);

            if (bccList.Any())
                message.Bcc.AddRange(bccList);

            var priority = EmailPriority.Normal;
            switch (_options.Value.Priority)
            {
                case EmailPriority.UseMapping:
                    if (!string.IsNullOrEmpty(PriorityProperty) && _options.Value.PriorityMapping.Count > 0 &&
                        TryGetPropertyValueCI(evt.Data.Properties, PriorityProperty, out var priorityProperty) &&
                        priorityProperty is string priorityValue &&
                        _options.Value.PriorityMapping.TryGetValue(priorityValue, out var matchedPriority))
                        priority = matchedPriority;
                    else
                        priority = _options.Value.DefaultPriority;
                    break;
                case EmailPriority.Low:
                case EmailPriority.Normal:
                case EmailPriority.High:
                    priority = _options.Value.Priority;
                    break;
                default:
                    priority = EmailPriority.Normal;
                    break;
            }

            message.Priority = (MessagePriority) priority;
            var errors = new List<Exception>();
            var lastServer = string.Empty;
            if (_options.Value.Host != null && _options.Value.Host.Any())
            {
                type = DeliveryType.MailHost;
                var result = await _mailGateway.SendAsync(_options.Value, message);
                errors = result.Errors;
                sent = result.Success;
                lastServer = result.LastServer;

                if (!result.Success)
                {
                    Log.ForContext("From", From).ForContext("To", to)
                        .ForContext("ReplyTo", replyTo).ForContext("CC", cc).ForContext("BCC", bcc)
                        .ForContext("Priority", priority).ForContext("Subject", subject)
                        .ForContext("Success", sent).ForContext("Body", body)
                        .ForContext(nameof(result.LastServer), result.LastServer)
                        .ForContext(nameof(result.Type), result.Type).ForContext(nameof(result.Errors), result.Errors)
                        .Error(result.LastError,
                            "Error sending mail: {Message}, From: {From}, To: {To}, Subject: {Subject}",
                            result.LastError?.Message, From, to, subject);
                    logged = true;
                }
            }

            if (!sent && _options.Value.DnsDelivery)
            {
                type = type == DeliveryType.None ? DeliveryType.Dns : DeliveryType.HostDnsFallback;
                var result = await _mailGateway.SendDnsAsync(type, _options.Value, message);
                errors = result.Errors;
                sent = result.Success;
                lastServer = result.LastServer;
                type = result.Type;

                if (!result.Success)
                {
                    Log.ForContext("From", From).ForContext("To", to)
                        .ForContext("ReplyTo", replyTo).ForContext("CC", cc).ForContext("BCC", bcc)
                        .ForContext("Priority", priority).ForContext("Subject", subject)
                        .ForContext("Success", sent).ForContext("Body", body)
                        .ForContext(nameof(result.Results), result.Results, true).ForContext("Errors", errors)
                        .ForContext(nameof(result.Type), result.Type).ForContext(nameof(result.LastServer), result.LastServer)
                        .Error(result.LastError,
                            "Error sending mail via DNS: {Message}, From: {From}, To: {To}, Subject: {Subject}",
                            result.LastError?.Message, From, to, subject);
                    logged = true;
                }
            }

            if (sent)
            {
                    Log.ForContext("From", From).ForContext("To", to)
                        .ForContext("ReplyTo", replyTo).ForContext("CC", cc).ForContext("BCC", bcc)
                        .ForContext("Priority", priority).ForContext("Subject", subject)
                        .ForContext("Success", true).ForContext("Body", body).ForContext("Errors", errors)
                        .ForContext("Type", type).ForContext("LastServer", lastServer)
                        .Information("Mail Sent, From: {From}, To: {To}, Subject: {Subject}", From, to, subject);
            }
            else if (!logged)
                Log.ForContext("From", From).ForContext("To", to)                        
				.ForContext("ReplyTo", replyTo).ForContext("CC", cc).ForContext("BCC", bcc)
                .ForContext("Priority", priority).ForContext("Subject", subject)
                .ForContext("Success", true).ForContext("Body", body).ForContext("Errors", errors)
                .ForContext("Type", type).ForContext("LastServer", lastServer)
                .Error("Unhandled mail error, From: {From}, To: {To}, Subject: {Subject}", From, to, subject);
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

        internal static string FormatTemplate(Template template, Event<LogEventData> evt, Host host)
        {
            var properties =
                (IDictionary<string, object>) ToDynamic(evt.Data.Properties ?? new Dictionary<string, object>());

            var payload = (IDictionary<string, object>) ToDynamic(new Dictionary<string, object>
            {
                {"$Id", evt.Id},
                {"$UtcTimestamp", evt.TimestampUtc},
                {"$LocalTimestamp", evt.Data.LocalTimestamp},
                {"$Level", evt.Data.Level},
                {"$MessageTemplate", evt.Data.MessageTemplate},
                {"$Message", evt.Data.RenderedMessage},
                {"$Exception", evt.Data.Exception},
                {"$Properties", properties},
                {"$EventType", "$" + evt.EventType.ToString("X8")},
                {"$Instance", host.InstanceName},
                {"$ServerUri", host.BaseUri},
                // Note, this will only be valid when events are streamed directly to the app, and not when the app is sending an alert notification.
                {
                    "$EventUri",
                    string.Concat(host.BaseUri, "#/events?filter=@Id%20%3D%20'", evt.Id, "'&amp;show=expanded")
                }
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

        static bool TryGetPropertyValueCI(IReadOnlyDictionary<string, object> properties, string propertyName,
            out object propertyValue)
        {
            var pair = properties.FirstOrDefault(p => p.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (pair.Key == null)
            {
                propertyValue = null;
                return false;
            }

            propertyValue = pair.Value;
            return true;
		}
		
        public SmtpOptions GetOptions()
        {
            return _options.Value;
        }
    }
}
