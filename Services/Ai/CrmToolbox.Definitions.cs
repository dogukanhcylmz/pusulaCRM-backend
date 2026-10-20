using System.Text.Json.Nodes;

namespace DigitestCrm.Api.Services.Ai;

/// Modele gönderilen ai nin kullanabileci araç listesi JSON olarak döndürüyorum.
public partial class CrmToolbox
{
    public JsonArray Definitions { get; } = new()
    {
        // ---------- Okuma ----------6 arac
        Tool("search_customers", "Müşterileri firma adı, yetkili adı, şehir veya e-postaya göre arar. Sorgu boşsa tüm müşterileri listeler.",
            Props(("query", Str("Aranacak metin (isteğe bağlı)")),
                  ("status", Choice("Durum filtresi", "Lead", "Active", "Passive")))),

        Tool("get_customer_details", "Bir müşterinin kartını getirir: iletişim bilgileri, tüm aktiviteleri ve notları, teklifleri. Müşteri özeti, e-posta veya görüşme taslağı hazırlamadan önce kullan.",
            Props(("customerName", Str("Müşteri firma adı veya yetkili adı"))),
            "customerName"),

        Tool("list_activities", "Aktiviteleri (arama, toplantı, e-posta, görev, not) listeler. Tarih aralığı aktivite tarihine göre filtreler.",
            Props(("customerName", Str("Sadece bu müşterinin aktiviteleri (isteğe bağlı)")),
                  ("onlyOpen", Bool("true ise sadece tamamlanmamış işler")),
                  ("from", Str("Başlangıç tarihi yyyy-MM-dd (isteğe bağlı)")),
                  ("to", Str("Bitiş tarihi yyyy-MM-dd (isteğe bağlı)")))),

        Tool("list_quotes", "Fiyat tekliflerini (satış fırsatlarını) listeler.",
            Props(("customerName", Str("Sadece bu müşterinin teklifleri (isteğe bağlı)")),
                  ("status", Choice("Durum filtresi", "Draft", "Sent", "Accepted", "Rejected")))),

        Tool("get_summary", "Genel CRM özetini getirir: müşteri sayıları, açık aktiviteler, açık teklif tutarı, yaklaşan işler, son teklifler.",
            new JsonObject()),

        Tool("get_sales_report", "Belirli bir dönemde oluşturulan tekliflerin raporunu getirir: duruma göre adet ve tutarlar, kazanma oranı, müşteri bazında kazanılan tutar.",
            Props(("from", Str("Başlangıç tarihi yyyy-MM-dd (boşsa tüm zamanlar)")),
                  ("to", Str("Bitiş tarihi yyyy-MM-dd (boşsa bugün)")))),

        // ---------- Yazma --------- 7 araç yazıldı
        Tool("create_customer", "Yeni müşteri kaydı oluşturur. Kullanıcı onayından sonra çalışır.",
            Props(("companyName", Str("Firma adı")),
                  ("contactName", Str("Yetkili kişi")),
                  ("contactTitle", Str("Yetkilinin unvanı, ör. Satın Alma Müdürü")),
                  ("email", Str("E-posta")),
                  ("phone", Str("Telefon (cep)")),
                  ("landline", Str("Sabit telefon")),
                  ("website", Str("Web sitesi")),
                  ("address", Str("Açık adres (mahalle, cadde, no, ilçe)")),
                  ("city", Str("Şehir")),
                  ("status", Choice("Durum (varsayılan Lead = potansiyel)", "Lead", "Active", "Passive"))),
            "companyName"),

        Tool("update_customer", "Mevcut bir müşterinin bilgilerini günceller. Sadece değişecek alanları gönder. Kullanıcı onayından sonra çalışır.",
            Props(("customerName", Str("Güncellenecek müşterinin mevcut firma adı veya yetkili adı")),
                  ("companyName", Str("Yeni firma adı")),
                  ("contactName", Str("Yeni yetkili kişi")),
                  ("contactTitle", Str("Yetkilinin yeni unvanı")),
                  ("email", Str("Yeni e-posta")),
                  ("phone", Str("Yeni telefon (cep)")),
                  ("landline", Str("Yeni sabit telefon")),
                  ("website", Str("Yeni web sitesi")),
                  ("address", Str("Yeni açık adres")),
                  ("city", Str("Yeni şehir")),
                  ("status", Choice("Yeni durum", "Lead", "Active", "Passive"))),
            "customerName"),

        Tool("create_activity", "Bir müşteri için aktivite, görev veya not oluşturur. Kullanıcı onayından sonra çalışır.",
            Props(("customerName", Str("Müşteri firma adı veya yetkili adı")),
                  ("type", Choice("Tür: Call=arama, Meeting=toplantı, Email=e-posta, Task=görev, Note=not", "Call", "Meeting", "Email", "Task", "Note")),
                  ("subject", Str("Kısa konu başlığı")),
                  ("dueDate", Str("Yerel saatle tarih-saat: yyyy-MM-ddTHH:mm (not için boş bırakılabilir)")),
                  ("notes", Str("Ayrıntı / not metni"))),
            "customerName", "type", "subject"),

        Tool("complete_activity", "Bir aktiviteyi tamamlandı olarak işaretler. Aktivite numarasını list_activities veya get_customer_details ile bul. Kullanıcı onayından sonra çalışır.",
            Props(("activityId", Int("Aktivite numarası"))),
            "activityId"),

        Tool("delete_activity", "Bir aktiviteyi kalıcı olarak siler. Kullanıcı açıkça onaylamadan çalışmaz.",
            Props(("activityId", Int("Aktivite numarası"))),
            "activityId"),
        //iç içe yapı  var teslim etmeden kontrol et !!!
        Tool("create_quote", "Bir müşteri için fiyat teklifi oluşturur. Fiyatlar TL ve KDV hariçtir. Kullanıcı onayından sonra çalışır.",
            Props(("customerName", Str("Müşteri firma adı veya yetkili adı")),
                  ("title", Str("Teklif başlığı")),
                  ("taxRate", Num("KDV oranı (%), varsayılan 20")),
                  ("validUntil", Str("Geçerlilik tarihi yyyy-MM-dd (varsayılan 30 gün sonra)")),
                  ("items", new JsonObject
                  {
                      ["type"] = "array",
                      ["description"] = "Teklif kalemleri",
                      ["items"] = new JsonObject
                      {
                          ["type"] = "object",
                          ["properties"] = Props(
                              ("description", Str("Ürün/hizmet")),
                              ("quantity", Num("Miktar")),
                              ("unitPrice", Num("Birim fiyat (TL, KDV hariç)")),
                              ("discountPercent", Num("İskonto yüzdesi (isteğe bağlı)"))),
                          ["required"] = new JsonArray("description", "quantity", "unitPrice")
                      }
                  })),
            "customerName", "title", "items"),

        Tool("update_quote_status", "Bir teklifin durumunu değiştirir (ör. gönderildi, kabul edildi, reddedildi). Kullanıcı onayından sonra çalışır.",
            Props(("quoteNumber", Str("Teklif numarası, ör. TKL-2026-0002")),
                  ("status", Choice("Yeni durum", "Draft", "Sent", "Accepted", "Rejected"))),
            "quoteNumber", "status"),
    };

    private static JsonObject Tool(string name, string description, JsonObject properties, params string[] required)
    {
        var parameters = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Length > 0)
            parameters["required"] = new JsonArray(required.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray());

        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject { ["name"] = name, ["description"] = description, ["parameters"] = parameters }
        };
    }

    private static JsonObject Props(params (string Name, JsonObject Schema)[] props)
    {
        var o = new JsonObject();
        foreach (var (n, s) in props) o[n] = s;
        return o;
    }

    private static JsonObject Str(string d) => new() { ["type"] = "string", ["description"] = d };
    private static JsonObject Num(string d) => new() { ["type"] = "number", ["description"] = d };
    private static JsonObject Int(string d) => new() { ["type"] = "integer", ["description"] = d };
    private static JsonObject Bool(string d) => new() { ["type"] = "boolean", ["description"] = d };

    private static JsonObject Choice(string d, params string[] values) => new()
    {
        ["type"] = "string",
        ["description"] = d,
        ["enum"] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray())
    };
}
