using Microsoft.Extensions.Caching.Memory;

namespace DigitestCrm.Api.Services.Ai;

/// <summary>Asistanın hazırladığı ama henüz kullanıcı onayı almamış işlem.</summary>
public record PendingAction(string Id, string Tool, string PayloadJson, string Title, string Summary, List<string> Details, bool Danger);

/// <summary>
/// Onay bekleyen işlemleri 15 dakika bellekte tutar. Her işlem tek kullanımlıktır:
/// onaylanınca ya da iptal edilince silinir, süresi dolunca kendiliğinden düşer.
/// </summary>
public class PendingActionStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private readonly IMemoryCache _cache;

    public PendingActionStore(IMemoryCache cache) => _cache = cache;

    public PendingAction Add(string tool, string payloadJson, string title, string summary, List<string> details, bool danger)
    {
        var action = new PendingAction(Guid.NewGuid().ToString("N"), tool, payloadJson, title, summary, details, danger);
        _cache.Set(Key(action.Id), action, Lifetime);
        return action;
    }

    /// <summary>İşlemi alır ve depodan siler (aynı işlem iki kez çalıştırılamaz).</summary>
    public PendingAction? Take(string id)
    {
        if (!_cache.TryGetValue(Key(id), out PendingAction? action)) return null;
        _cache.Remove(Key(id));
        return action;
    }

    private static string Key(string id) => $"pending:{id}";
}
