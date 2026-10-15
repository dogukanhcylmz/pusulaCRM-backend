using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitestCrm.Api.Services.Ai;

/// <summary>
/// Asistanın kullanabildiği araçlar (function calling).
///
/// İki tür araç vardır:
///  • Okuma araçları (search_*, list_*, get_*) hemen çalışır, sonucu modele döner.
///  • Yazma araçları (create_*, update_*, complete_*, delete_*) HEMEN ÇALIŞMAZ.
///    Önce doğrulanır ve kullanıcıya gösterilecek bir özet hazırlanır (PrepareWriteAsync).
///    Kullanıcı "Onayla" derse ExecuteWriteAsync ile gerçekleştirilir.
///    Böylece onay kuralı modelin "söz dinlemesine" değil, koda bağlıdır.
/// </summary>
public partial class CrmToolbox
{
    private static readonly CultureInfo Tr = new("tr-TR");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> WriteTools = new()
    {
        "create_customer", "update_customer", "create_activity", "complete_activity",
        "delete_activity", "create_quote", "update_quote_status"
    };

    private readonly CustomerService _customers;
    private readonly ActivityService _activities;
    private readonly QuoteService _quotes;
    private readonly DashboardService _dashboard;
    private readonly LocalClock _clock;

    public CrmToolbox(CustomerService customers, ActivityService activities, QuoteService quotes,
                      DashboardService dashboard, LocalClock clock)
    {
        _customers = customers;
        _activities = activities;
        _quotes = quotes;
        _dashboard = dashboard;
        _clock = clock;
    }

    public static bool IsWriteTool(string name) => WriteTools.Contains(name);

    /// <summary>Yazma aracı için hazırlanan önizleme. Error doluysa işlem hazırlanamamıştır.</summary>
    public record PreparedWrite(string? Error, string Title = "", string Summary = "", List<string>? Details = null,
                                bool Danger = false, string PayloadJson = "");
}
