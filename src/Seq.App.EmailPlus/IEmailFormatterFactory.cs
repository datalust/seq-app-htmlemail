namespace Seq.App.EmailPlus
{
    public interface IEmailFormatterFactory
    {
        IEmailFormatter Create(string instanceName, string serverUri, string bodyTemplate, string subjectTemplate, int? maxSubjectLength);
    }
}