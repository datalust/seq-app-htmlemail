using System.Collections.Generic;
using System.Net.Mail;

namespace Seq.App.EmailPlus.Tests.Support
{
    class CollectingMailGateway : IMailGateway
    {
        public List<SentMessage> Sent { get; } = new List<SentMessage>();

        public void Send(SmtpClient client, MailMessage message)
        {
            Sent.Add(new SentMessage(client, message));
        }
    }
}
