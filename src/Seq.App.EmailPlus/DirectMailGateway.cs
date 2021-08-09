using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace Seq.App.EmailPlus
{
    class DirectMailGateway : IMailGateway
    {
        public async Task<MailResult> Send(SmtpClient client, SmtpOptions options, MimeMessage message)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (message == null) throw new ArgumentNullException(nameof(message));
            try
            {
                await client.ConnectAsync(options.Server, options.Port, options.UseSsl);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return new MailResult {Success = true};
            }
            catch (Exception ex)
            {
                return new MailResult {Success = false, Errors = ex};
            }
        }
    }
}