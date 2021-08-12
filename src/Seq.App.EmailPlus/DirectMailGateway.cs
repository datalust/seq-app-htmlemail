﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;
using MailKit.Net.Smtp;
using MimeKit;

namespace Seq.App.EmailPlus
{
    internal class DirectMailGateway : IMailGateway
    {
        private readonly SmtpClient _client = new SmtpClient();

        public async Task<MailResult> Send(SmtpOptions options, MimeMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var mailResult = new MailResult();
            var type = DeliveryType.MailHost;
            var errors = new List<Exception>();
            foreach (var server in options.ServerList)
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

        public async Task<DnsMailResult> SendDns(DeliveryType deliveryType, SmtpOptions options, MimeMessage message)
        {
            var dnsResult = new DnsMailResult();
            var resultList = new List<MailResult>();
            var lastServer = string.Empty;
            var errors = new List<Exception>();
            if (message == null) throw new ArgumentNullException(nameof(message));
            var type = deliveryType;

            try
            {
                var domains = GetDomains(message);

                var dnsClient = new LookupClient();
                foreach (var domain in domains)
                {
                    type = deliveryType;
                    lastServer = domain;
                    var mx = await dnsClient.QueryAsync(domain, QueryType.MX);
                    var mxServers =
                        (from mxServer in mx.Answers
                            where !string.IsNullOrEmpty(((MxRecord) mxServer).Exchange)
                            select ((MxRecord) mxServer).Exchange).Select(dummy => (string) dummy).ToList();
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
                            Success = mailResult.Success
                        };

                        resultList.Add(mailResult);

                        if (mailResult.Success)
                            break;
                        type = DeliveryType.DnsFallback;
                    }

                    if (mailResult.Success) continue;
                    dnsResult.Success = false;

                    break;
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


        private static IEnumerable<string> GetDomains(MimeMessage message)
        {
            var domains = new List<string>();
            foreach (var to in message.To)
            {
                var toDomain = to.ToString().Split('@')[1];
                if (string.IsNullOrEmpty(toDomain)) continue;
                foreach (var domain in domains.Where(domain =>
                    !toDomain.Equals(domain, StringComparison.OrdinalIgnoreCase)))
                    domains.Add(toDomain);
            }

            return domains;
        }

        private async Task<MailResult> TryDeliver(string server, SmtpOptions options, MimeMessage message,
            DeliveryType deliveryType)
        {
            if (string.IsNullOrEmpty(server))
                return new MailResult {Success = false, LastServer = server, Type = deliveryType};
            try
            {
                await _client.ConnectAsync(server, options.Port, options.SocketOptions);
                if (options.RequiresAuthentication)
                    await _client.AuthenticateAsync(options.User, options.Password);


                await _client.SendAsync(message);
                await _client.DisconnectAsync(true);


                return new MailResult {Success = true, LastServer = server, Type = deliveryType};
            }
            catch (Exception ex)
            {
                return new MailResult {Success = false, LastError = ex, LastServer = server, Type = deliveryType};
            }
        }
    }
}