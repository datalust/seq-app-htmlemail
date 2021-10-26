using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace Seq.App.EmailPlus
{
    class DirectMailGateway : IMailGateway
    {
        public async Task SendAsync(SmtpOptions options, MimeMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var client = new SmtpClient
            {
                ServerCertificateValidationCallback = ServerCertificateValidation
            };

            await client.ConnectAsync(options.Host, options.Port, options.SocketOptions);
            if (options.RequiresAuthentication)
                await client.AuthenticateAsync(options.Username, options.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        private static bool ServerCertificateValidation(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors errors)
        {
            var cert2 = new X509Certificate2(certificate);
            return chain.Build(cert2);
        }
    }
}