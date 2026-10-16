namespace DigitestCrm.Api.Services.Ai;

public class AiOptions
{
    public const string Section = "Ai";

    public string Provider { get; set; } = "OpenRouter";
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";
    public string Model { get; set; } = "nvidia/nemotron-3-super-120b-a12b:free";

    /// Yedek modeller appsteings e koydum
    public List<string> FallbackModels { get; set; } = new();

    public int CooldownSeconds { get; set; } = 90;

    public string? ApiKey { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public IEnumerable<string> ModelChain =>
        new[] { Model }.Concat(FallbackModels)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
}


public class AssistantOptions
{
    public const string Section = "Assistant";

    public string ProductName { get; set; } = "digitestCRM";
    public string SupportContact { get; set; } = "destek@digitestcrm.com";
    public string TimeZone { get; set; } = "Europe/Istanbul";
    public string UserName { get; set; } = "Demo Kullanıcı";
    public string UserRole { get; set; } = "Satış Temsilcisi";
    public string UserTeam { get; set; } = "Kurumsal Satış";
}

/// Kullanıcının yerel saati ile UTC arasında çeviri
public class LocalClock
{
    private readonly TimeZoneInfo _tz;

    public LocalClock(AssistantOptions options)
    {
        try { _tz = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone); }
        catch { _tz = TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "Türkiye", "Türkiye"); }
    }

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);

    public DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _tz);

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _tz);

    public string Format(DateTime? utc, bool withTime = true) =>
        utc is null ? "tarihsiz" : ToLocal(utc.Value).ToString(withTime ? "dd.MM.yyyy HH:mm" : "dd.MM.yyyy");
}
