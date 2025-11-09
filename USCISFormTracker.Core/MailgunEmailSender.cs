using RestSharp;
using RestSharp.Authenticators;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public class MailgunEmailSender : IEmailSender
{
    private readonly string _apiKey;
    private readonly string _domain;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public MailgunEmailSender(string apiKey, string domain, string fromEmail, string fromName)
    {
        _apiKey = apiKey;
        _domain = domain;
        _fromEmail = fromEmail;
        _fromName = fromName;
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
}
