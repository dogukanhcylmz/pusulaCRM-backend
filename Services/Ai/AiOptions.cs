namespace DigitestCrm.Api.Services.Ai;

/// <summary>
/// appsettings.json → "Ai": hangi yapay zeka servisine bağlanılacağı.
/// OpenAI uyumlu her servis çalışır: Google Gemini (ücretsiz katman), Groq, OpenAI...
/// </summary>
public class AiOptions
{
    public const string Section = "Ai";

    public string Provider { get; set; } = "Gemini";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";
    public string Model { get; set; } = "gemini-3.8-flash";
    public string? ApiKey { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// appsettings.json → "Assistant": sistem promptundaki yer tutucuların değerleri.
/// Giriş sistemi eklendiğinde kullanıcı bilgileri oturumdaki kullanıcıdan alınır.
/// </summary>
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

/// <summary>Kullanıcının yerel saati ile UTC arasında çeviri.</summary>
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

    /// <summary>Kullanıcıya gösterilecek biçim: GG.AA.YYYY SS:DD</summary>
    public string Format(DateTime? utc, bool withTime = true) =>
        utc is null ? "tarihsiz" : ToLocal(utc.Value).ToString(withTime ? "dd.MM.yyyy HH:mm" : "dd.MM.yyyy");
}
