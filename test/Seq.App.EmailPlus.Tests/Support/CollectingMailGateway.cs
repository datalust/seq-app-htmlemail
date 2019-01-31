using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

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
