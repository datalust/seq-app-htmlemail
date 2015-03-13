using System;
using System.Net;
using System.Net.Mail;

namespace Seq.App.EmailPlus
{
    public interface IMailClient : IDisposable
    {
        ICredentialsByHost Credentials { get; set; }

        bool EnableSsl { get; set; }

        string Host { get; set; }

        int Port { get; set; }

        void Send(MailMessage message);
    }
}