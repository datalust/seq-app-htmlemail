using System.Collections.Generic;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace Seq.App.EmailPlus.Tests.Support
{
    class CollectingMailGateway : IMailGateway
    {
        public List<SentMessage> Sent { get; } = new List<SentMessage>();

        public Task SendAsync(SmtpOptions options, MimeMessage message)
        {
            Sent.Add(new SentMessage(message));
            return Task.CompletedTask;
        }
    }
}
