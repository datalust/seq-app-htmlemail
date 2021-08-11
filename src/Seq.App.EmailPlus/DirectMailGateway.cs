using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace Seq.App.EmailPlus
{
    class DirectMailGateway : IMailGateway
    {
        public async Task<MailResult> Send(SmtpOptions options, MimeMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            try
            {
                var client = new SmtpClient();
                
                await client.ConnectAsync(options.Server, options.Port, options.SocketOptions);
                if (options.RequiresAuthentication)
                    await client.AuthenticateAsync(options.User, options.Password);
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