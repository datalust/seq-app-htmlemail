using System.Collections.Generic;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;


namespace Seq.App.EmailPlus.Tests.Support
{
    class CollectingMailGateway : IMailGateway
    {
        public List<SentMessage> Sent { get; } = new List<SentMessage>();

        public async Task<MailResult> Send(SmtpClient client, SmtpOptions options, MimeMessage message)
        {
            await Task.Run(() => Sent.Add(new SentMessage(client, message)));
            
            return new MailResult() {Success = true};
        }
    }
}
