using System;
using System.Collections.Generic;

namespace Seq.App.EmailPlus
{
    public class DnsMailResult
    {
        public bool Success { get; set; }
        public DeliveryType Type { get; set; }
        public string LastServer { get; set; }
        public Exception LastError { get; set; }
        public List<MailResult> Results { get; set; }
    }
}
