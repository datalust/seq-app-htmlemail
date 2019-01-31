using System.Net.Mail;

namespace Seq.App.EmailPlus
{
    interface IMailGateway
    {
        void Send(SmtpClient client, MailMessage message);
    }
}