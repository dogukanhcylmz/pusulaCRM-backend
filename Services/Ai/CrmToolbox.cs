using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PusulaCrm.Api.Services.Ai;

/*
   Okuma araçları sonucu hemen modele döner.
   Yazma araçları 
    Önce doğrulanır ve kullanıcıya özet hazırlr (PrepareWriteAsync).
    Kullanıcı "Onayla" derse ExecuteWriteAsync 
*/
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

    //Error doluysa işlem hazırlanamamış
    public record PreparedWrite(string? Error, string Title = "", string Summary = "", List<string>? Details = null,
                                bool Danger = false, string PayloadJson = "");
}
