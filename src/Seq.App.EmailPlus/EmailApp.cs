using System;
using System.Collections.Concurrent;
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

    [SeqApp("Email+",
        Description = "Uses a Handlebars template to send events as SMTP email.")]
    public class EmailApp : SeqApp, ISubscribeToAsync<LogEventData>
    {
        readonly IMailGateway _mailGateway = new DirectMailGateway();
        readonly ConcurrentDictionary<uint, DateTime> _lastSeen = new ConcurrentDictionary<uint, DateTime>();
        readonly Lazy<Template> _bodyTemplate, _plainTextTemplate, _subjectTemplate, _toAddressesTemplate, _ccAddressesTemplate, _bccAddressesTemplate;
        public readonly Lazy<SmtpOptions> Options;

        const string DefaultSubjectTemplate = @"[{{$Level}}] {{{$Message}}} (via Seq)";
        const int MaxSubjectLength = 130;

        static EmailApp()
        {
            HandlebarsHelpers.Register();
        }

        internal EmailApp(IMailGateway mailGateway)
            : this()
        {
            _mailGateway = mailGateway ?? throw new ArgumentNullException(nameof(mailGateway));
        }

        public EmailApp()
        {
            Options = new Lazy<SmtpOptions>(() => new SmtpOptions
            {
                Server = Host,
                DnsDelivery = DeliverUsingDns != null && (bool) DeliverUsingDns,
                Port = Port ?? 25,
                Priority = SmtpOptions.ParsePriority(Priority, out var priorityMapping),
                PriorityMapping = priorityMapping,
                DefaultPriority = SmtpOptions.ParsePriority(DefaultPriority),
                SocketOptions = EnableSsl != null && (bool) EnableSsl
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.None,
                User = Username, Password = Password,
                RequiresAuthentication = !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password)
            });

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
                if (string.IsNullOrEmpty(toAddressTemplate))
                    return (_, __) => To;
                return Handlebars.Compile(toAddressTemplate);
            });

            _ccAddressesTemplate = new Lazy<Template>(() =>
            {
                var ccAddressTemplate = Cc;
                if (string.IsNullOrEmpty(ccAddressTemplate))
                    return (_, __) => To;
                return Handlebars.Compile(ccAddressTemplate);
            });

            _bccAddressesTemplate = new Lazy<Template>(() =>
            {
                var bccAddressTemplate = Bcc;
                if (string.IsNullOrEmpty(bccAddressTemplate))
                    return (_, __) => To;
                return Handlebars.Compile(bccAddressTemplate);
            });
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
            DisplayName = "CC address",
            HelpText = "The account to which emails should be sent as CC. Multiple addresses are separated by a comma. Handlebars syntax is supported.")]
        public string Cc { get; set; }

        [SeqAppSetting(
            DisplayName = "BCC address",
            HelpText = "The account to which the email is being sent as BCC. Multiple addresses are separated by a comma. Handlebars syntax is supported.")]
        public string Bcc { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Subject template",
            HelpText = "The subject of the email, using Handlebars syntax. If blank, a default subject will be generated.")]
        public string SubjectTemplate { get; set; }

        [SeqAppSetting(
            HelpText = "The name of the SMTP server machine. Optionally specify fallback hosts as comma-delimited string.",
            IsOptional = true)]
        public new string Host { get; set; }

        [SeqAppSetting(
            HelpText = "Deliver directly using DNS. If Host is configured, this will be used as a fallback delivery mechanism.")]
        public bool? DeliverUsingDns { get; set; }

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
                       "the default template that includes the message and properties (https://github.com/datalust/seq-apps/tree/master/src/Seq.App.EmailPlus/Resources/DefaultBodyTemplate.html).")]
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

        public async Task OnAsync(Event<LogEventData> evt)
        {
            var added = false;
            var lastSeen = _lastSeen.GetOrAdd(evt.EventType, k =>
            {
                added = true;
                return DateTime.UtcNow;
            });
            if (!added)
            {
                if (lastSeen > DateTime.UtcNow.AddMinutes(-SuppressionMinutes)) return;
                _lastSeen[evt.EventType] = DateTime.UtcNow;
            }

            var to = FormatTemplate(_toAddressesTemplate.Value, evt, base.Host)
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();

            var cc = FormatTemplate(_ccAddressesTemplate.Value, evt, base.Host)
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();

            var bcc = FormatTemplate(_bccAddressesTemplate.Value, evt, base.Host)
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();

            var body = FormatTemplate(_bodyTemplate.Value, evt, base.Host);
            var textBody = FormatTemplate(_bodyTemplate.Value, evt, base.Host);
            var subject = FormatTemplate(_subjectTemplate.Value, evt, base.Host).Trim().Replace("\r", "")
                .Replace("\n", "");
            if (subject.Length > MaxSubjectLength)
                subject = subject.Substring(0, MaxSubjectLength);

            var toList = to.Select(MailboxAddress.Parse).ToList();
            var ccList = cc.Select(MailboxAddress.Parse).ToList();
            var bccList = bcc.Select(MailboxAddress.Parse).ToList();

            var sent = false;
            var type = DeliveryType.None;
            var message = new MimeMessage(
                new List<InternetAddress> {InternetAddress.Parse(From)},
                toList, subject,
                (new BodyBuilder
                    {HtmlBody = body, TextBody = textBody == body ? string.Empty : textBody}).ToMessageBody());

            if (ccList.Any())
                message.Cc.AddRange(ccList);

            if (bccList.Any())
                message.Bcc.AddRange(bccList);
            
            switch (Options.Value.Priority)
            {
                case EmailPriority.UseMapping:
                    if (!string.IsNullOrEmpty(PriorityProperty) && Options.Value.PriorityMapping.Count > 0 &&
                        TryGetPropertyValueCI(evt.Data.Properties, PriorityProperty, out var priorityProperty) &&
                        priorityProperty is string priorityValue &&
                        Options.Value.PriorityMapping.TryGetValue(priorityValue, out var matchedPriority))
                        message.Priority = (MessagePriority) matchedPriority;
                    else
                        message.Priority = (MessagePriority) Options.Value.DefaultPriority;
                    break;
                case EmailPriority.Low:
                case EmailPriority.Normal:
                case EmailPriority.High:
                    message.Priority = (MessagePriority) Options.Value.Priority;
                    break;
            }

            Exception lastError = null;
            var errors = new List<Exception>();
            if (Options.Value.Server != null && Options.Value.Server.Any())
            {
                type = DeliveryType.MailHost;
                var result = await _mailGateway.Send(Options.Value, message);
                errors = result.Errors;
                sent = result.Success;
                if (!result.Success)
                {
                    if (result.LastError != null)
                        lastError = result.LastError;
                    Log.ForContext("From", From).ForContext("To", to).ForContext("Subject", subject)
                        .ForContext("Success", sent).ForContext("Body", body)
                        .ForContext(nameof(result.LastServer), result.LastServer)
                        .ForContext(nameof(result.Type), result.Type).ForContext(nameof(result.Errors), result.Errors)
                        .Error(result.LastError, "Error sending mail: {Message}, From: {From}, To: {To}, Subject: {Subject}",
                            result.LastError?.Message, From, to, subject);
                }
            }

            if (!sent && Options.Value.DnsDelivery)
            {
                type = type == DeliveryType.None ? DeliveryType.Dns : DeliveryType.HostDnsFallback;
                var result = await _mailGateway.SendDns(type, Options.Value, message);
                errors = result.Errors;
                sent = result.Success;

                if (!result.Success)
                {
                    if (result.LastError != null)
                        lastError = result.LastError;
                    Log.ForContext("From", From).ForContext("To", to).ForContext("Subject", subject)
                        .ForContext("Success", sent).ForContext("Body", body)
                        .ForContext(nameof(result.Results), result.Results, true).ForContext("Errors", errors)
                        .ForContext(nameof(result.LastServer), result.LastServer).Error(result.LastError,
                            "Error sending mail via DNS: {Message}, From: {From}, To: {To}, Subject: {Subject}", result.LastError?.Message, From, to, subject);
                }
            }

            switch (sent)
            {
                case false when lastError != null:
                    throw lastError;
                case true:
                    Log.ForContext("From", From).ForContext("To", to).ForContext("Subject", subject)
                        .ForContext("Success", true).ForContext("Body", body).ForContext("Errors", errors)
                        .Information("Mail Sent, From: {From}, To: {To}, Subject: {Subject}");
                    break;
            }
        }

        public static string FormatTemplate(Template template, Event<LogEventData> evt, Host host)
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
    }
}
