namespace Seq.App.EmailPlus
{
    public enum DeliveryType
    {
        MailHost,
        MailFallback,
        Dns,
        DnsFallback,
        HostDnsFallback,
        None = -1
    }
}
