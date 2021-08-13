using MimeKit;

namespace Seq.App.EmailPlus
{
    public enum EmailPriority
    {
        Low = MessagePriority.NonUrgent,
        Normal = MessagePriority.Normal,
        High = MessagePriority.Urgent,
        UseMapping = -1,
    }
}