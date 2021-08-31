using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;
using MailKit.Net.Smtp;
using MimeKit;

namespace Seq.App.EmailPlus
{
    class DirectMailGateway : IMailGateway
    {
        static readonly SmtpClient Client = new SmtpClient();
        static readonly LookupClient DnsClient = new LookupClient();

        public async Task<MailResult> SendAsync(SmtpOptions options, MimeMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var mailResult = new MailResult();
            var type = DeliveryType.MailHost;
            var errors = new List<Exception>();
            foreach (var server in options.Host)
            {
                mailResult = await TryDeliver(server, options, message, type);
                if (!mailResult.Success)
                {
                    errors.Add(mailResult.LastError);
                    type = DeliveryType.MailFallback;
                }
                else
                {
                    break;
                }
            }

            mailResult.Errors = errors;
            return mailResult;
        }

        public async Task<DnsMailResult> SendDnsAsync(DeliveryType deliveryType, SmtpOptions options,
            MimeMessage message)
        {
            var dnsResult = new DnsMailResult();
            var resultList = new List<MailResult>();
            var lastServer = string.Empty;
            var errors = new List<Exception>();
            if (message == null) throw new ArgumentNullException(nameof(message));
            var type = deliveryType;

            try
            {
                var domains = GetDomains(message).ToList();
                var successCount = 0;

                foreach (var domain in domains)
                {
                    type = deliveryType;
                    lastServer = domain;
                    var mx = await DnsClient.QueryAsync(domain, QueryType.MX);
                    var mxServers =
                        (from mxServer in mx.Answers
                            where !string.IsNullOrEmpty(((MxRecord)mxServer).Exchange)
                            select ((MxRecord)mxServer).Exchange).Select(dummy => (string)dummy).ToList();
                    var mailResult = new MailResult();
                    foreach (var server in mxServers)
                    {
                        lastServer = server;
                        mailResult = await TryDeliver(server, options, message, type);
                        var lastError = dnsResult.LastError;
                        errors.AddRange(mailResult.Errors);

                        dnsResult = new DnsMailResult
                        {
                            LastServer = server,
                            LastError = mailResult.LastError ?? lastError,
                            Type = type,
                        };

                        resultList.Add(mailResult);

                        if (mailResult.Success)
                            break;
                        type = DeliveryType.DnsFallback;
                    }

                    if (mailResult.Success)
                    {
                        successCount++;
                        continue;
                    }

                    if (dnsResult.LastError != null) continue;
                    dnsResult.LastError = mxServers.Count == 0
                        ? new Exception("DNS delivery failed - no MX records detected for " + domain)
                        : new Exception("DNS delivery failed - no error detected");
                    dnsResult.Success = false;
                    dnsResult.Errors.Add(dnsResult.LastError);
                }

                if (!domains.Any())
                {
                    dnsResult.Success = false;
                    dnsResult.LastError =
                        new Exception("DNS delivery failed - no domains parsed from recipient addresses");
                }

                if (successCount < domains.Count)
                {
                    if (successCount == 0)
                    {
                        dnsResult.Success = false;
                        dnsResult.LastError =
                            new Exception("DNS delivery failure - no domains could be successfully delivered.");
                    }
                    else
                    {
                        dnsResult.Success = true; // A qualified success ...
                        dnsResult.LastError =
                            new Exception(
                                $"DNS delivery partial failure - {domains.Count - successCount} of {successCount} domains could not be delivered.");
                    }

                    dnsResult.Errors.Add(dnsResult.LastError);
                }
                else
                {
                    dnsResult.Success = true;
                }
            }
            catch (Exception ex)
            {
                dnsResult = new DnsMailResult
                {
                    Type = type,
                    LastServer = lastServer,
                    Success = false,
                    LastError = ex
                };
            }

            dnsResult.Errors = errors;
            dnsResult.Results = resultList;
            return dnsResult;
        }


        public static IEnumerable<string> GetDomains(MimeMessage message)
        {
            var domains = new List<string>();
            foreach (var to in message.To)
            {
                var toDomain = to.ToString().Split('@')[1];
                if (string.IsNullOrEmpty(toDomain)) continue;
                if (!domains.Any(domain => domain.Equals(toDomain, StringComparison.OrdinalIgnoreCase)))
                    domains.Add(toDomain);
            }

            return domains;
        }

        private static async Task<MailResult> TryDeliver(string server, SmtpOptions options, MimeMessage message,
            DeliveryType deliveryType)
        {
            if (string.IsNullOrEmpty(server))
                return new MailResult {Success = false, LastServer = server, Type = deliveryType};
            try
            {
                await Client.ConnectAsync(server, options.Port, options.SocketOptions);
                if (options.RequiresAuthentication)
                    await Client.AuthenticateAsync(options.Username, options.Password);


                await Client.SendAsync(message);
                await Client.DisconnectAsync(true);

                return new MailResult {Success = true, LastServer = server, Type = deliveryType};
            }
            catch (Exception ex)
            {
                return new MailResult {Success = false, LastError = ex, LastServer = server, Type = deliveryType};
            }
        }
    }
}