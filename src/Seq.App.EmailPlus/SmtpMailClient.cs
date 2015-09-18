using System.Net;
using System.Net.Mail;

namespace Seq.App.EmailPlus
{
    public class SmtpMailClient : IMailClient
    {
        private readonly SmtpClient _smtpClient;

        public SmtpMailClient(string host, int port, bool enableSsl)
        {
            _smtpClient = new SmtpClient(host, port) {EnableSsl = enableSsl};
        }

        public void Dispose()
        {
            _smtpClient.Dispose();
        }

        public ICredentialsByHost Credentials
        {
            get { return _smtpClient.Credentials; }

            set { _smtpClient.Credentials = value; }
        }

        public bool EnableSsl
        {
            get { return _smtpClient.EnableSsl; }

            set { _smtpClient.EnableSsl = value; }
        }

        public string Host
        {
            get { return _smtpClient.Host; }

            set { _smtpClient.Host = value; }
        }

        public int Port
        {
            get { return _smtpClient.Port; }

            set { _smtpClient.Port = value; }
        }

        public void Send(MailMessage message)
        {
            _smtpClient.Send(message);
        }
    }
}