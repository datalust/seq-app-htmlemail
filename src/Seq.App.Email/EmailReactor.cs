using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using Seq.App.Email.QuickStache;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.Email
{
    [SeqApp("Formatted Email",
        Description = "Uses a provided template to send events as formatted email.")]
    public class EmailReactor : Reactor, ISubscribeTo<LogEventData>
    {
        [SeqAppSetting(
            DisplayName = "From address",
            HelpText = "The account from which the email is being sent.")]
        public string From { get; set; }
        
        [SeqAppSetting(
            DisplayName = "To address",
            HelpText = "The account to which the email is being sent.")]
        public string To { get; set; }

        [SeqAppSetting(
            DisplayName = "Subject template",
            HelpText = "The subject of the email. See the help for the body template for template syntax information.")]
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
            InputType = SettingInputType.LongText,
            DisplayName = "Body template",
            HelpText = "The template to use when generating the email body. You can use Mustache-style {{PropertyName}} syntax " + 
                        "to include properties from the log event. Leave this blank to use the default format, which prints the " +
                        "event data in a basic format.")]
        public string BodyTemplate { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Body is HTML",
            HelpText = "Check this box if the email body is an HTML document. Otherwise, the email will be sent as plain text.")]
        public bool? IsBodyHtml { get; set; }

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
            var body = string.IsNullOrWhiteSpace(BodyTemplate) ?
                FormatDefaultBody(evt) :
                FormatTemplate(BodyTemplate, evt);

            var subject = FormatTemplate(SubjectTemplate, evt);

            var client = new SmtpClient(Host, Port ?? 25);
            if (!string.IsNullOrWhiteSpace(Username))
                client.Credentials = new NetworkCredential(Username, Password);

            var message = new MailMessage(From, To, subject, body);
            if (IsBodyHtml == true && !string.IsNullOrWhiteSpace(BodyTemplate))
                message.IsBodyHtml = true;

            client.Send(message);
        }

        string FormatDefaultBody(Event<LogEventData> evt)
        {
            var body = new StringBuilder();
            body.Append("{{@Timestamp}} [{{@Level}}] {{@RenderedMessage}}");

            if (evt.Data.Properties != null)
            {
                body.AppendLine();

                foreach (var property in evt.Data.Properties.OrderBy(p => p.Key))
                {
                    body.AppendFormat(" {0} = {{{{{1}}}}}", property.Key, property.Key);
                    body.AppendLine();
                }
            }

            if (evt.Data.Exception != null)
            {
                body.AppendLine();
                body.Append("{{@Exception}}");
            }

            return FormatTemplate(body.ToString(), evt);
        }

        string FormatTemplate(string template, Event<LogEventData> evt)
        {
            var tokens = StacheParser.ParseStache(template);
            var output = new StringWriter();
            foreach (var tok in tokens)
                tok.Render(output, evt);
            return output.ToString();
        }
    }
}
