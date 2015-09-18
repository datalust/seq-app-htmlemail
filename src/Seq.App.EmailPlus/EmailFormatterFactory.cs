namespace Seq.App.EmailPlus
{
    public class EmailFormatterFactory : IEmailFormatterFactory
    {
        public IEmailFormatter Create(string instanceName, string serverUri, string bodyTemplate, string subjectTemplate, int? maxSubjectLength)
        {
            return new EmailFormatter(instanceName, serverUri, bodyTemplate, subjectTemplate, maxSubjectLength);
        }
    }
}