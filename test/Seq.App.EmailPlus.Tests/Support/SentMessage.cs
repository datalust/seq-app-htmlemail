using MimeKit;

namespace Seq.App.EmailPlus.Tests.Support
{
    class SentMessage
    {
        public MimeMessage Message { get; }

        public SentMessage(MimeMessage message)
        {
            Message = message;
        }
    }
}