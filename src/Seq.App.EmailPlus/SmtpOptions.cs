using System;
using System.Collections.Generic;
using System.Linq;
using MailKit.Security;

namespace Seq.App.EmailPlus
{
    public class SmtpOptions
    {
        public List<string> Server { get; set; } = new List<string>();
        public bool DnsDelivery { get; set; }
        public int Port { get; set; } = 25;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RequiresAuthentication { get; set; }
        public SecureSocketOptions SocketOptions { get; set; }

        public static IEnumerable<string> GetServerList(string hostName)
        {
            if (!string.IsNullOrEmpty(hostName))
                return hostName.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim()).ToList();
            return new List<string>();
        }

        public static SecureSocketOptions GetSocketOptions(bool? enableSsl, bool? useTlsWhenAvailable)
        {
            if (enableSsl == null) return SecureSocketOptions.Auto;
            switch (enableSsl)
            {
                case true:
                    return SecureSocketOptions.SslOnConnect;
                case false when useTlsWhenAvailable != null && !(bool) useTlsWhenAvailable:
                    return SecureSocketOptions.None;
                case false when useTlsWhenAvailable != null && (bool) useTlsWhenAvailable:
                    return SecureSocketOptions.StartTlsWhenAvailable;
            }

            return SecureSocketOptions.Auto;
        }
    }
}
