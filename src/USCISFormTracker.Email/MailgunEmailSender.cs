using RestSharp;
using RestSharp.Authenticators;

namespace USCISFormTracker.Email;

public class MailgunEmailSender : IEmailSender
{
    private readonly MailgunOptions _options;

    public MailgunEmailSender(MailgunOptions options)
    {
        _options = options;
    }

    public async Task SendEmailAsync(EmailMessage message)
    {
        var clientOptions = new RestClientOptions($"https://api.mailgun.net/v3/{_options.Domain}")
        {
            Authenticator = new HttpBasicAuthenticator("api", _options.ApiKey)
        };

        using var client = new RestClient(clientOptions);
        var request = new RestRequest("messages", Method.Post);

        request.AddParameter("from", $"{_options.FromName} <{_options.FromEmail}>");
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
        var clientOptions = new RestClientOptions("https://api.mailgun.net/v3")
        {
            Authenticator = new HttpBasicAuthenticator("api", _options.ApiKey)
        };

        using var client = new RestClient(clientOptions);
        var request = new RestRequest($"lists/{_options.MailingListAddress}/members", Method.Post);

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
