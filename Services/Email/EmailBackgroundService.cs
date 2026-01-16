using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using one_db_mitra.Data;
using one_db_mitra.Hubs;
using one_db_mitra.Models.Db;

namespace one_db_mitra.Services.Email
{
    public class EmailBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

        public EmailBackgroundService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueueAsync(stoppingToken);
                }
                catch
                {
                    // swallow background errors to keep service alive
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneDbMitraContext>();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationsHub>>();

            var setting = await db.tbl_m_email_setting.AsNoTracking()
                .OrderBy(s => s.created_at)
                .FirstOrDefaultAsync(cancellationToken);
            if (setting is null)
            {
                return;
            }

            var queued = await db.tbl_m_email_notifikasi
                .Where(n => n.status == null || n.status == "queued")
                .OrderBy(n => n.created_at)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (queued.Count == 0)
            {
                return;
            }

            foreach (var item in queued)
            {
                item.status = "processing";
                item.updated_at = DateTime.UtcNow;
                item.updated_by = "system";
            }

            await db.SaveChangesAsync(cancellationToken);

            foreach (var item in queued)
            {
                try
                {
                    await SendEmailAsync(setting, item, cancellationToken);
                    item.status = "sent";
                    item.error_message = null;
                    await hub.Clients.All.SendAsync("notify", new { message = $"Email terkirim ke {item.email_to}", type = "success" }, cancellationToken);
                }
                catch (Exception ex)
                {
                    item.status = "failed";
                    item.error_message = ex.Message;
                    await hub.Clients.All.SendAsync("notify", new { message = $"Email gagal: {item.email_to}", type = "danger" }, cancellationToken);
                }

                item.updated_at = DateTime.UtcNow;
                item.updated_by = "system";
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        private static Task SendEmailAsync(tbl_m_email_setting setting, tbl_m_email_notifikasi message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(message.email_to))
            {
                throw new InvalidOperationException("Email penerima kosong.");
            }

            var fromEmail = string.IsNullOrWhiteSpace(setting.from_email)
                ? setting.smtp_username
                : setting.from_email;
            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("From email belum diset.");
            }

            using var mail = new MailMessage();
            mail.From = new MailAddress(fromEmail, setting.from_name ?? string.Empty);
            foreach (var address in ParseEmails(message.email_to))
            {
                mail.To.Add(address);
            }

            foreach (var address in ParseEmails(message.email_cc))
            {
                mail.CC.Add(address);
            }

            foreach (var address in ParseEmails(message.email_bcc))
            {
                mail.Bcc.Add(address);
            }

            mail.Subject = message.subject ?? "(No Subject)";
            mail.Body = message.pesan_html ?? string.Empty;
            mail.IsBodyHtml = true;

            foreach (var attachment in ParseEmails(message.file_path))
            {
                if (File.Exists(attachment))
                {
                    mail.Attachments.Add(new Attachment(attachment));
                }
            }

            using var client = new SmtpClient(setting.smtp_host, setting.smtp_port)
            {
                EnableSsl = setting.enable_ssl
            };

            if (!string.IsNullOrWhiteSpace(setting.smtp_username))
            {
                client.Credentials = new System.Net.NetworkCredential(setting.smtp_username, setting.smtp_password);
            }

            return client.SendMailAsync(mail);
        }

        private static IEnumerable<string> ParseEmails(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            return raw
                .Split(new[] { ';', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item));
        }
    }
}
