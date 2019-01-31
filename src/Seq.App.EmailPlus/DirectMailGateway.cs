using System;
using System.Net.Mail;

namespace Seq.App.EmailPlus
{
    class DirectMailGateway : IMailGateway
    {
        public void Send(SmtpClient client, MailMessage message)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (message == null) throw new ArgumentNullException(nameof(message));
            client.Send(message);
        }
    }
}