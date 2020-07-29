using System.Collections.Generic;
using Seq.Apps;
using Serilog;

namespace Seq.App.EmailPlus.Tests.Support
{
    class TestAppHost : IAppHost
    {
        public Apps.App App { get; } = new Apps.App("appinstance-0", "Test", new Dictionary<string, string>(), "./storage");
        public Host Host { get; } = Some.Host();
        public ILogger Logger { get; } = new LoggerConfiguration().CreateLogger();
        public string StoragePath { get; } = "./storage";
    }
}
