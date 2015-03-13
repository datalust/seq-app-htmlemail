namespace Seq.App.EmailPlus
{
    public interface IMailClientFactory
    {
        IMailClient Create(string host, int port, bool enableSsl);
    }
}