using RestSharp;
using RestSharp.Authenticators;
using USCISFormTracker.Emailer.Models;

namespace USCISFormTracker.Emailer;

public class MailgunEmailSender : IEmailSender
{
    private readonly string _apiKey;
    private readonly string _domain;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string? _mailingListAddress;

    public MailgunEmailSender(string apiKey, string domain, string fromEmail, string fromName, string? mailingListAddress = null)
    {
        _apiKey = apiKey;
        _domain = domain;
        _fromEmail = fromEmail;
        _fromName = fromName;
        _mailingListAddress = mailingListAddress;
    }

    public async Task SendEmailAsync(EmailMessage message)
    {
        var options = new RestClientOptions($"https://api.mailgun.net/v3/{_domain}")
        {
            Authenticator = new HttpBasicAuthenticator("api", _apiKey)
        };

        var client = new RestClient(options);
        var request = new RestRequest("messages", Method.Post);

        request.AddParameter("from", $"{_fromName} <{_fromEmail}>");
        request.AddParameter("to", message.To);
        request.AddParameter("subject", message.Subject);
        request.AddParameter("html", message.HtmlBody);

        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            request.AddParameter("text", message.TextBody);
        }

        var response = await client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            throw new Exception($"Failed to send email via Mailgun: {response.StatusCode} - {response.Content}");
        }
    }

    public async Task AddToMailingListAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(_mailingListAddress))
        {
            throw new InvalidOperationException("Mailing list address is not configured");
        }

        var options = new RestClientOptions("https://api.mailgun.net/v3")
        {
            Authenticator = new HttpBasicAuthenticator("api", _apiKey)
        };

        var client = new RestClient(options);
        var request = new RestRequest($"lists/{_mailingListAddress}/members", Method.Post);

        request.AddParameter("address", email);
        request.AddParameter("subscribed", "yes");
        request.AddParameter("upsert", "yes"); // Update if already exists

        var response = await client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            throw new Exception($"Failed to add email to mailing list via Mailgun: {response.StatusCode} - {response.Content}");
        }
    }
}
