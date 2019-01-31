using System.Net.Mail;

namespace Seq.App.EmailPlus.Tests.Support
{
    class SentMessage
    {
        public SmtpClient Client { get; }
        public MailMessage Message { get; }

        public SentMessage(SmtpClient client, MailMessage message)
        {
            Client = client;
            Message = message;
        }
    }
}