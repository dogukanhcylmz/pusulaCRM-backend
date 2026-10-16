using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace DigitestCrm.Api.Services.Ai;

// Hata veren modelleri geçici olarak bekletip tekrar tekrar deneme
public class ModelHealth
{
    private readonly ConcurrentDictionary<string, DateTime> _coolUntil = new(StringComparer.OrdinalIgnoreCase);

    public bool IsCooling(string model) =>
        _coolUntil.TryGetValue(model, out var until) && until > DateTime.UtcNow;

    public void MarkFailed(string model, TimeSpan duration) => _coolUntil[model] = DateTime.UtcNow + duration;

    public void MarkHealthy(string model) => _coolUntil.TryRemove(model, out _);
}

public class AiClient
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<AiOptions> _options;   // appsettings.json bsğlı
    private readonly ModelHealth _health;
    private readonly ILogger<AiClient> _logger;

    public AiClient(HttpClient http, IOptionsMonitor<AiOptions> options, ModelHealth health, ILogger<AiClient> logger)
    {
        _http = http;
        _options = options;
        _health = health;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    //Modelden yanıt mesajı döner metin ya da tool_calls içereir
    public async Task<JsonObject> CompleteAsync(JsonArray messages, JsonArray tools, CancellationToken ct = default)
    {
        var options = _options.CurrentValue;
        var chain = options.ModelChain.ToList();

        var candidates = chain.Where(m => !_health.IsCooling(m)).ToList();
        if (candidates.Count == 0) candidates = chain;

        AiServiceException? last = null;
        foreach (var model in candidates)
        {
            try
            {
                var reply = await CallAsync(options, model, messages, tools, ct);
                _health.MarkHealthy(model);
                return reply;
            }
            catch (AiServiceException ex) when (CanTryNext(ex.StatusCode))
            {
                last = ex;
                _health.MarkFailed(model, TimeSpan.FromSeconds(options.CooldownSeconds));
                _logger.LogWarning("Model {Model} kullanılamadı ({Status}); {Seconds} sn atlanacak, sıradaki deneniyor.",
                    model, ex.StatusCode, options.CooldownSeconds);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                last = new AiServiceException(503);
                _health.MarkFailed(model, TimeSpan.FromSeconds(options.CooldownSeconds));
                _logger.LogWarning(ex, "Model {Model} zaman aşımı/bağlantı hatası; sıradaki deneniyor.", model);
            }
        }

        throw last ?? new AiServiceException(503);
    }

    // 401-3 key sorunu, 429 dolu, 404 kalkmış model, 400 model araç desteklememesi, 500 servis hatası.
    private static bool CanTryNext(int status) => status is 400 or 404 or 408 or 429 or >= 500;

    private async Task<JsonObject> CallAsync(AiOptions options, string model, JsonArray messages, JsonArray tools, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages.DeepClone(),
            ["tools"] = tools.DeepClone(),
            ["tool_choice"] = "auto",
            ["temperature"] = 0.2
        };

        var url = options.BaseUrl.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        try { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey?.Trim()); }
        catch (FormatException) { throw new AiServiceException(401); }  

        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("AI servisi hata döndürdü ({Model}): {Status} {Body}", model, (int)response.StatusCode, text);
            throw new AiServiceException((int)response.StatusCode);
        }

        var json = JsonNode.Parse(text);

        if (json?["error"] is JsonNode err)
        {
            var code = (int?)err["code"] ?? 502;
            _logger.LogWarning("AI servisi gövdede hata döndürdü ({Model}): {Body}", model, err.ToJsonString());
            throw new AiServiceException(code);
        }

        var message = json?["choices"]?[0]?["message"]?.AsObject()
                      ?? throw new AiServiceException(502);

        _logger.LogInformation("Yanıt veren model: {Model}", model);
        return message;
    }
}

public class AiServiceException : Exception
{
    public int StatusCode { get; }
    public AiServiceException(int statusCode) : base($"AI servisi hatası ({statusCode})") => StatusCode = statusCode;
}
