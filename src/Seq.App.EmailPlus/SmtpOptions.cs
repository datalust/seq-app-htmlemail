using System;
using System.Collections.Generic;
using System.Linq;
using MailKit.Security;

namespace Seq.App.EmailPlus
{
    public class SmtpOptions
    {
        public string Server { get; set; }
        public bool DnsDelivery { get; set; }
        public int Port { get; set; } = 25;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RequiresAuthentication { get; set; }
        public SecureSocketOptions SocketOptions { get; set; }

        public IEnumerable<string> ServerList
        {
            get
            {
                if (!string.IsNullOrEmpty(Server))
                    return Server.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim()).ToList();
                return new List<string>();
            }
        }
    }

    
}
