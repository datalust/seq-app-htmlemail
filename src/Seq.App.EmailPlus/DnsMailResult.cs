﻿using System;
using System.Collections.Generic;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Seq.App.EmailPlus
{
    public class DnsMailResult
    {
        public bool Success { get; set; }
        public DeliveryType Type { get; set; }
        public string LastServer { get; set; }
        public Exception LastError { get; set; }
        public List<Exception> Errors { get; set; } = new List<Exception>();
        public List<MailResult> Results { get; set; } = new List<MailResult>();
    }
}
