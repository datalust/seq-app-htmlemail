using MailKit.Security;

namespace Seq.App.EmailPlus
{
    public enum TlsOptions
    {
        /// <summary>
        /// None
        /// </summary>
        None = SecureSocketOptions.None,
        /// <summary>
        /// Auto
        /// </summary>
        Auto = SecureSocketOptions.Auto,
        /// <summary>
        /// Implicit TLS
        /// </summary>
        SslOnConnect = SecureSocketOptions.SslOnConnect,
        /// <summary>
        /// Explicit TLS
        /// </summary>
        StartTls = SecureSocketOptions.StartTls,
        /// <summary>
        /// Optional TLS
        /// </summary>
        StartTlsWhenAvailable = SecureSocketOptions.StartTlsWhenAvailable

    }
}
