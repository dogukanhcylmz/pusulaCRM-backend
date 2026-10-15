using Microsoft.AspNetCore.Mvc;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Services.Ai;

namespace DigitestCrm.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chat;
    public ChatController(ChatService chat) => _chat = chat;

    /// <summary>
    /// Sohbet geçmişini ve kullanıcının bulunduğu sayfayı alır; asistanın cevabını döner.
    /// Cevap bir kayıt değişikliği öneriyorsa "pending" alanında onay kartı gelir.
    /// Sunucu sohbet durumu tutmaz; geçmiş arayüzde saklanır.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponseDto>> Post(ChatRequestDto request, CancellationToken ct)
    {
        if (request.Messages is null || request.Messages.Count == 0)
            return BadRequest("Mesaj boş olamaz.");

        return await _chat.SendAsync(request, ct);
    }

    /// <summary>Kullanıcı onay kartında "Onayla" dedi.</summary>
    [HttpPost("actions/{id}/confirm")]
    public Task<ConfirmResultDto> Confirm(string id) => _chat.ConfirmAsync(id);

    /// <summary>Kullanıcı onay kartında "Vazgeç" dedi.</summary>
    [HttpPost("actions/{id}/cancel")]
    public ConfirmResultDto Cancel(string id) => _chat.Cancel(id);
}
