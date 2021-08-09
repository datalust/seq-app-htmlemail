using System;
using System.Collections.Generic;
using System.Text;
using MailKit.Security;

namespace Seq.App.EmailPlus
{
    class SmtpOptions
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 25;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseSsl { get; set; } = false;
        public bool RequiresAuthentication { get; set; } = false;
        public string PreferredEncoding { get; set; } = string.Empty;
        public SecureSocketOptions? SocketOptions { get; set; }
    }
}
