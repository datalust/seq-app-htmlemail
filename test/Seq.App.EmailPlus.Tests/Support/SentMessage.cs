
using MailKit.Net.Smtp;
using MimeKit;

namespace Seq.App.EmailPlus.Tests.Support
{
    class SentMessage
    {
        public SmtpClient Client { get; }
        public MimeMessage Message { get; }

        public SentMessage(SmtpClient client, MimeMessage message)
        {
            Client = client;
            Message = message;
        }
    }
}