using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace DigitestCrm.Api.Services.Ai;

/// <summary>
/// OpenAI uyumlu "chat/completions" uç noktasına istek atan ince istemci.
/// Bir SDK'ya bağımlı değil; böylece Gemini, Groq ya da OpenAI arasında
/// sadece ayar değiştirerek geçiş yapılabilir.
/// </summary>
public class AiClient
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<AiClient> _logger;

    public AiClient(HttpClient http, IOptions<AiOptions> options, ILogger<AiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    /// <summary>Modelden bir yanıt mesajı döner (metin ya da tool_calls içerebilir).</summary>
    public async Task<JsonObject> CompleteAsync(JsonArray messages, JsonArray tools, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["messages"] = messages.DeepClone(),
            ["tools"] = tools.DeepClone(),
            ["tool_choice"] = "auto",
            ["temperature"] = 0.2
        };

        var url = _options.BaseUrl.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("AI servisi hata döndü: {Status} {Body}", (int)response.StatusCode, text);
            throw new AiServiceException((int)response.StatusCode);
        }

        var json = JsonNode.Parse(text);
        return json?["choices"]?[0]?["message"]?.AsObject()
               ?? throw new AiServiceException(502);
    }
}

public class AiServiceException : Exception
{
    public int StatusCode { get; }
    public AiServiceException(int statusCode) : base($"AI servisi hatası ({statusCode})") => StatusCode = statusCode;
}
