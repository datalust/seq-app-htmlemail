using System.Collections.Generic;
using Seq.Apps;
using Serilog;

namespace Seq.App.EmailPlus.Tests.Support
{
    class TestAppHost : IAppHost
    {
        public Apps.App App { get; } = new Apps.App(new Dictionary<string, string>(), "./storage");
        public Host Host { get; } = new Host(new [] { "https://seq.example.com" }, null);
        public ILogger Logger { get; } = new LoggerConfiguration().CreateLogger();
        public string StoragePath { get; } = "./storage";
    }
}
