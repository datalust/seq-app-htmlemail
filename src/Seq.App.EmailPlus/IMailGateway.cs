

using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace Seq.App.EmailPlus
{
    interface IMailGateway
    {
        Task<MailResult> Send(SmtpOptions options, MimeMessage message);
    }
}