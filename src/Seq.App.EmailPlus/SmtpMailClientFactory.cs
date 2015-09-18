namespace Seq.App.EmailPlus
{
    public class SmtpMailClientFactory : IMailClientFactory
    {
        public IMailClient Create(string host, int port, bool enableSsl)
        {
            return new SmtpMailClient(host, port, enableSsl);
        }
    }
}