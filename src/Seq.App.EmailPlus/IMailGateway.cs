

using System.Collections.Generic;
using System.Threading.Tasks;
using MimeKit;

namespace Seq.App.EmailPlus
{
    interface IMailGateway
    {
        Task<MailResult> Send(SmtpOptions options, MimeMessage message);
        Task<DnsMailResult> SendDns(DeliveryType deliveryType, SmtpOptions options, MimeMessage message);
    }
}