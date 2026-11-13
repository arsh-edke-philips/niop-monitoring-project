using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace NIOP_Monitoring.Agent;

public class ChatAgentOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8001";
    public string? ApiKey { get; set; }
}

[Route("chat")]
public class ChatController(
    IHttpClientFactory httpFactory,
    IOptions<ChatAgentOptions> options,
    ILogger<ChatController> logger) : Controller
{
    public const string HttpClientName = "chat-agent";
    private const string SessionKey = "ChatSessionId";
    private const int MaxMessageLength = 2000;

    public record SendRequest(string? Message);

    [HttpPost("send")]
    [ValidateAntiForgeryToken]
    public async Task Send([FromBody] SendRequest req, CancellationToken ct)
    {
        var message = req.Message?.Trim();
        if (string.IsNullOrEmpty(message) || message.Length > MaxMessageLength)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        if (string.IsNullOrEmpty(options.Value.ApiKey))
        {
            logger.LogError("ChatAgent:ApiKey is not configured");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        HttpResponseMessage? upstream = null;
        try
        {
            var sessionId = HttpContext.Session.GetString(SessionKey);
            upstream = await CallAgentAsync(message, sessionId, ct);

            if (upstream.StatusCode == HttpStatusCode.NotFound && sessionId is not null)
            {
                logger.LogInformation("chat session {SessionId} not found, starting a new one", sessionId);
                upstream.Dispose();
                HttpContext.Session.Remove(SessionKey);
                upstream = await CallAgentAsync(message, null, ct);
            }

            if (upstream.StatusCode == HttpStatusCode.Conflict)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }
            if (!upstream.IsSuccessStatusCode)
            {
                logger.LogError("chat agent returned HTTP {Status}", (int)upstream.StatusCode);
                Response.StatusCode = StatusCodes.Status502BadGateway;
                return;
            }

            await RelayAsync(upstream, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "chat agent unreachable");
            if (!Response.HasStarted) Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        finally
        {
            upstream?.Dispose();
        }
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public IActionResult New()
    {
        HttpContext.Session.Remove(SessionKey);
        return NoContent();
    }

    private async Task<HttpResponseMessage> CallAgentAsync(string message, string? sessionId, CancellationToken ct)
    {
        var http = httpFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat")
        {
            Content = JsonContent.Create(new { message, session_id = sessionId })
        };
        request.Headers.Add("X-Chat-Key", options.Value.ApiKey);
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private async Task RelayAsync(HttpResponseMessage upstream, CancellationToken ct)
    {
        await using var body = await upstream.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(body, Encoding.UTF8);

        var first = await ReadEventAsync(reader, ct);
        if (first?.Kind == "session")
        {
            RememberSession(first.Value.Data);
            first = null;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        try
        {
            var evt = first ?? await ReadEventAsync(reader, ct);
            while (evt is not null)
            {
                await Response.WriteAsync(evt.Value.Raw, ct);
                await Response.Body.FlushAsync(ct);
                evt = await ReadEventAsync(reader, ct);
            }
        }
        catch (IOException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "chat stream from agent broke");
            await Response.WriteAsync(
                "event: error\ndata: {\"message\":\"The answer was interrupted. Please try again.\"}\n\n", ct);
        }
    }

    private void RememberSession(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var id = doc.RootElement.GetProperty("session_id").GetString();
        if (!string.IsNullOrEmpty(id))
            HttpContext.Session.SetString(SessionKey, id);
    }

    private static async Task<(string Kind, string Data, string Raw)?> ReadEventAsync(
        StreamReader reader, CancellationToken ct)
    {
        string kind = "", data = "";
        var raw = new StringBuilder();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            raw.Append(line).Append('\n');
            if (line.Length == 0)
                return (kind, data, raw.ToString());
            if (line.StartsWith("event: ")) kind = line["event: ".Length..];
            else if (line.StartsWith("data: ")) data = line["data: ".Length..];
        }
        return null;
    }
}