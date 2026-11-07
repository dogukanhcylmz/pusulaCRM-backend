using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using PusulaCrm.Api.Dtos;

namespace PusulaCrm.Api.Services.Ai;

public class ChatService
{
    private const int MaxSteps = 6;
    private const int MaxHistory = 20;
    private const int MaxMessageLength = 4000;

    private readonly AiClient _ai;
    private readonly CrmToolbox _tools;
    private readonly PromptBuilder _prompt;
    private readonly PendingActionStore _pending;
    private readonly FallbackAssistant _fallback;
    private readonly AiOptions _options;
    private readonly ILogger<ChatService> _logger;

    public ChatService(AiClient ai, CrmToolbox tools, PromptBuilder prompt, PendingActionStore pending,
                       FallbackAssistant fallback, IOptions<AiOptions> options, ILogger<ChatService> logger)
    {
        _ai = ai;
        _tools = tools;
        _prompt = prompt;
        _pending = pending;
        _fallback = fallback;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatResponseDto> SendAsync(ChatRequestDto request, CancellationToken ct)
    {
        var history = request.Messages
            .Where(m => m.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(MaxHistory)
            .Select(m => m with { Content = m.Content.Length > MaxMessageLength ? m.Content[..MaxMessageLength] : m.Content })
            .ToList();

        if (!_options.IsConfigured)
        {
            var lastUser = history.LastOrDefault(m => m.Role == "user")?.Content ?? "";
            return new ChatResponseDto(await _fallback.ReplyAsync(lastUser), new List<string>(), AiEnabled: false);
        }

        var messages = new JsonArray { Message("system", await _prompt.BuildAsync(request.Context)) };
        foreach (var m in history) messages.Add(Message(m.Role, m.Content));

        try
        {
            for (var step = 0; step < MaxSteps; step++)
            {
                var reply = await _ai.CompleteAsync(messages, _tools.Definitions, ct);

                // Cevabı olduğu gibi geçmişe ekliyorum;
                messages.Add(reply.DeepClone());

                if (reply["tool_calls"] is not JsonArray calls || calls.Count == 0)
                {
                    var text = reply["content"]?.ToString();
                    return new ChatResponseDto(
                        string.IsNullOrWhiteSpace(text) ? "Bu isteği yanıtlayamadım, lütfen farklı ifade eder misiniz?" : text.Trim(),
                        new List<string>(), AiEnabled: true);
                }

                foreach (var call in calls.OfType<JsonObject>())
                {
                    var id = call["id"]?.ToString() ?? Guid.NewGuid().ToString("N");
                    var name = call["function"]?["name"]?.ToString() ?? "";
                    var args = call["function"]?["arguments"]?.ToString();
                    _logger.LogInformation("Araç çağrısı: {Tool} {Args}", name, args);

                    if (!CrmToolbox.IsWriteTool(name))
                    {
                        messages.Add(ToolResult(id, name, await SafeReadAsync(name, args)));
                        continue;
                    }

                    // Yazma aracı: çalıştırma, onaya hazırla
                    var prepared = await _tools.PrepareWriteAsync(name, args);
                    if (prepared.Error is not null)
                    {
                        // Eksik hatalı bilgiyi modele bildir kullanıcıya sor
                        messages.Add(ToolResult(id, name, JsonSerializer.Serialize(new { ok = false, error = prepared.Error })));
                        continue;
                    }

                    var action = _pending.Add(name, prepared.PayloadJson, prepared.Title, prepared.Summary,
                                              prepared.Details ?? new List<string>(), prepared.Danger);

                    var intro = reply["content"]?.ToString();
                    return new ChatResponseDto(
                        string.IsNullOrWhiteSpace(intro) ? "" : intro.Trim(),
                        new List<string>(), AiEnabled: true,
                        Pending: new PendingActionDto(action.Id, action.Title, action.Summary, action.Details, action.Danger));
                }
            }

            return new ChatResponseDto("Lütfen daha kısa bir istekle tekrar deneyin.",
                                       new List<string>(), true);
        }
        catch (AiServiceException ex)
        {
            var msg = ex.StatusCode switch
            {
                400 => "Yapay zeka servisi isteği kabul etmedi. Model adını ve ayarları kontrol edin.",
                401 or 403 => "Yapay zeka servisine bağlanılamadı: API anahtarı geçersiz görünüyor.",
                404 => "Yapay zeka modeli bulunamadı. appsettings.json içindeki model adını kontrol edin.",
                429 => "Ücretsiz kullanım limitine ulaşıldı. Bir dakika sonra tekrar deneyin.",
                _ => "Yapay zeka servisi şu anda yanıt vermiyor. Lütfen tekrar deneyin."
            };
            return new ChatResponseDto(msg, new List<string>(), true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Yapay zeka servisine ulaşılamadı");
            return new ChatResponseDto("Yapay zeka servisine ulaşılamadı. İnternet bağlantısını kontrol edin.", new List<string>(), true);
        }
    }

    public async Task<ConfirmResultDto> ConfirmAsync(string id)
    {
        var action = _pending.Take(id);
        if (action is null)
            return new ConfirmResultDto(false, "Bu işlemin süresi dolmuş veya daha önce işlenmiş. Lütfen isteğinizi tekrar yazın.");

        try
        {
            var message = await _tools.ExecuteWriteAsync(action.Tool, action.PayloadJson);
            _logger.LogInformation("Onaylanan işlem çalıştırıldı: {Tool}", action.Tool);
            return new ConfirmResultDto(true, message);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException)
        {
            return new ConfirmResultDto(false, $"İşlem yapılamadı: {ex.Message}");
        }
    }

    public ConfirmResultDto Cancel(string id)
    {
        _pending.Take(id);
        return new ConfirmResultDto(true, "İşlem iptal edildi; herhangi bir değişiklik yapılmadı.");
    }

    private async Task<string> SafeReadAsync(string name, string? args)
    {
        try
        {
            return await _tools.ExecuteReadAsync(name, args);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Araç hatası: {Tool}", name);
            return JsonSerializer.Serialize(new { ok = false, error = "Veri alınırken beklenmeyen bir hata oluştu." });
        }
    }

    private static JsonObject ToolResult(string id, string name, string content) => new()
    {
        ["role"] = "tool",
        ["tool_call_id"] = id,
        ["name"] = name,
        ["content"] = content
    };

    private static JsonObject Message(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };
}
