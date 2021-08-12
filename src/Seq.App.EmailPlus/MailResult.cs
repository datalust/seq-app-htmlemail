using System;
using System.Collections.Generic;

namespace Seq.App.EmailPlus
{
    public class MailResult
    {
        public bool Success { get; set; }
        public DeliveryType Type { get; set; }
        public string LastServer { get; set; }
        public Exception LastError { get; set; }
        public List<Exception> Errors { get; set; } = new List<Exception>();
    }
}