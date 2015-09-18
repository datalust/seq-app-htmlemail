using System.Collections.Generic;
using Seq.App.EmailPlus.Tests.Support;
using Xunit;

namespace Seq.App.EmailPlus.Tests
{
    public class EmailReactorTests
    {
        [Fact]
        public void BuiltInPropertiesAreRenderedInTemplates()
        {
            var template = Handlebars.Handlebars.Compile("{{$Level}}");
            var data = Some.LogEvent();
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal(data.Data.Level.ToString(), result);
        }

        [Fact]
        public void PayloadPropertiesAreRenderedInTemplates()
        {
            var template = Handlebars.Handlebars.Compile("See {{What}}");
            var data = Some.LogEvent(new Dictionary<string, object>{{ "What", 10 }});
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal("See 10", result);
        }
    }
}
