using System;
using System.Collections.Generic;
using System.Linq;
using HandlebarsDotNet.MemberAccessors;
using MailKit.Security;
using MimeKit;

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
        public EmailPriority Priority { get; set; } = EmailPriority.Normal;
        public Dictionary<string, EmailPriority> PriorityMapping { get; set; } =
            new Dictionary<string, EmailPriority>(StringComparer.OrdinalIgnoreCase);
        public EmailPriority DefaultPriority { get; set; } = EmailPriority.Normal;
        public SecureSocketOptions SocketOptions { get; set; }

        public SmtpOptions(string host, bool dnsDelivery, int port, string priority, string defaultPriority, SecureSocketOptions socketOptions, string username = null, string password = null)
        {
            Host = GetServerList(host).ToList();
            DnsDelivery = dnsDelivery;
			Priority = ParsePriority(priority, out var priorityMapping);
			PriorityMapping = priorityMapping;
			DefaultPriority = ParsePriority(defaultPriority);
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

        static EmailPriority ParsePriority(string priority, out Dictionary<string,EmailPriority> priorityList)
        {
            priorityList = new Dictionary<string, EmailPriority>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(priority)) return EmailPriority.Normal;
            if (!priority.Contains('='))
                return Enum.TryParse(priority, out EmailPriority priorityValue) ? priorityValue : EmailPriority.Normal;
            foreach (var kv in priority.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList().Select(p => p.Split(new[] {'='}, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim())
                    .ToArray()))
            {
                if (kv.Length != 2 || !Enum.TryParse(kv[1], true, out EmailPriority value) ||
                    value == EmailPriority.UseMapping) return EmailPriority.Normal;

                priorityList.Add(kv[0], value);
            }

            return priorityList.Count > 0 ? EmailPriority.UseMapping : EmailPriority.Normal;

        }

        static EmailPriority ParsePriority(string priority)
        {
            if (string.IsNullOrEmpty(priority)) return EmailPriority.Normal;
            return Enum.TryParse(priority, out EmailPriority priorityValue) ? priorityValue : EmailPriority.Normal;
        }
    }
}
