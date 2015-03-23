using NUnit.Framework;

namespace Seq.App.EmailPlus.Tests
{
    [TestFixture]
    public class EmailFormatterTests : EventTestsBase
    {
        [Test]
        public void CanHavePropertyInSubject()
        {
            var formatter = new EmailFormatter("foo", "bar", subjectTemplate: "[{{$Events.[0].$Properties.Category}}]");
            var subject = formatter.FormatSubject(new[] {GetLogEvent()});
            Assert.IsTrue(subject.Contains("Security"), "Subject did not contain the property.");
        }

        [Test]
        public void DefaultTemplateBodyContainsAllBatchedEvents()
        {
            var formatter = new EmailFormatter("foo", "bar");
            var body = formatter.FormatBody(new[] {GetLogEvent(), GetLogEvent(1)});
            Assert.AreEqual(2, CountSubstrings(body, "div class=\"seq-event-detail\""), "Body did not contain the correct number of events.");
        }

        [Test]
        public void DefaultTemplateSubjectEndsWithCountOnBatchedEvents()
        {
            var formatter = new EmailFormatter("foo", "bar");
            var subject = formatter.FormatSubject(new[] { GetLogEvent(), GetLogEvent(1) });
            Assert.IsTrue(subject.EndsWith("(2)"));
        }

        [Test]
        public void DefaultTemplateSubjectDoesNotEndWithCountSingleEvents()
        {
            var formatter = new EmailFormatter("foo", "bar");
            var subject = formatter.FormatSubject(new[] { GetLogEvent(), GetLogEvent(1) });
            Assert.IsFalse(subject.EndsWith("(1)"));
        }

        [Test]
        public void MaxSubjectLengthIsEnforced()
        {
            var formatter = new EmailFormatter("foo", "bar", maxSubjectLength: 5);
            var subject = formatter.FormatSubject(new[] { GetLogEvent() });
            Assert.IsTrue(subject.Length == 5, "Subject was the wrong length.");
        }

        [Test]
        public void FormatsSubjectCorrectlyWhenMaxLengthIsLonger()
        {
            var formatter = new EmailFormatter("foo", "bar", maxSubjectLength: 130);
            var subject = formatter.FormatSubject(new[] { GetLogEvent() });
            Assert.IsTrue(subject.EndsWith("(via Seq)"), "Subject was truncated when it should not have been.");
        }

        private static int CountSubstrings(string source, string substring)
        {
            return (source.Length - source.Replace(substring, string.Empty).Length) / substring.Length;
        }
    }
}