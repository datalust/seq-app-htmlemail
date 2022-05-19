using System;
using System.Collections.Generic;
using System.Linq;
using MailKit.Security;

namespace Seq.App.EmailPlus
{
    public class SmtpOptions
    {
        public List<string> Host { get; set; } = new List<string>();
        public bool DnsDelivery { get; set; }
        public int Port { get; set; } = 25;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RequiresAuthentication => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
        public TlsOptions SocketOptions { get; set; }

        public SmtpOptions(string host, bool dnsDelivery, int port, TlsOptions socketOptions, string username = null, string password = null)
        {
            Host = GetServerList(host).ToList();
            DnsDelivery = dnsDelivery;
            Port = port;
            Username = username;
            Password = password;
            SocketOptions = socketOptions;
        }
      
        IEnumerable<string> GetServerList(string hostName)
        {
            if (!string.IsNullOrEmpty(hostName))
                return hostName.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim()).ToList();
            return new List<string>();
        }

        public static TlsOptions GetSocketOptions(int port, bool? enableSsl, TlsOptions? enableTls)
        {
            if (enableSsl == null && enableTls == null) return TlsOptions.Auto;

            switch (enableTls)
            {
                case null when (bool)enableSsl && port == 465: //Implicit TLS
                case TlsOptions.None when port == 465:
                case TlsOptions.Auto when port == 465:
                case TlsOptions.StartTlsWhenAvailable when port == 465:
                    return TlsOptions.SslOnConnect;
                case null when (bool)enableSsl:
                    return TlsOptions.StartTls; //Explicit TLS
                case null:
                    return TlsOptions.Auto;
                default:
                    return (TlsOptions)enableTls;
            }
        }
    }
}
