using System.Threading.Tasks;
using MimeKit;

namespace Seq.App.EmailPlus
{
    interface IMailGateway
    {
        Task<MailResult> SendAsync(SmtpOptions options, MimeMessage message);
        Task<DnsMailResult> SendDnsAsync(DeliveryType deliveryType, SmtpOptions options, MimeMessage message);
    }
}