using System.Net;
using System.Net.Mail;

namespace Atlas_Application.Services;

/// <summary>
/// Sends plain-text alert emails via SMTP.
/// Silently no-ops when SMTP host or recipient is not configured.
/// </summary>
public class Email_Notifier
{
    private readonly string _smtp_host;
    private readonly int    _smtp_port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _from;
    private readonly string _to;

    private bool Active => !string.IsNullOrWhiteSpace(_smtp_host) && !string.IsNullOrWhiteSpace(_to);

    public Email_Notifier(
        string smtp_host,
        int    smtp_port,
        string username,
        string password,
        string from_address,
        string to_address)
    {
        _smtp_host = smtp_host;
        _smtp_port = smtp_port;
        _username  = username;
        _password  = password;
        _from      = string.IsNullOrWhiteSpace(from_address) ? username : from_address;
        _to        = to_address;
    }

    public async Task Send_Async(string subject, string body)
    {
        if (!Active) return;
        try
        {
#pragma warning disable SYSLIB0021 // SmtpClient is functional in .NET 8 for this use case
            using var smtp = new SmtpClient(_smtp_host, _smtp_port)
            {
                EnableSsl   = true,
                Credentials = new NetworkCredential(_username, _password)
            };
            var msg = new MailMessage(_from, _to, subject, body) { IsBodyHtml = false };
            await smtp.SendMailAsync(msg).ConfigureAwait(false);
#pragma warning restore SYSLIB0021
        }
        catch
        {
            // Notification failures must never abort bot operation
        }
    }
}
