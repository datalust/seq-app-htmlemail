using System.Threading.Tasks;
using MimeKit;

namespace Seq.App.EmailPlus
{
    interface IMailGateway
    {
        Task SendAsync(SmtpOptions options, MimeMessage message);
    }
}