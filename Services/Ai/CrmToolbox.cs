using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Models;

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
public class CrmToolbox
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

    // =====================================================================
    // Araç tanımları (JSON Schema). Model bu açıklamalara bakarak karar verir.
    // =====================================================================
    public JsonArray Definitions { get; } = new()
    {
        // ---------- Okuma ----------
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

        // ---------- Yazma (kullanıcı onayı gerekir) ----------
        Tool("create_customer", "Yeni müşteri kaydı oluşturur. Kullanıcı onayından sonra çalışır.",
            Props(("companyName", Str("Firma adı")),
                  ("contactName", Str("Yetkili kişi")),
                  ("email", Str("E-posta")),
                  ("phone", Str("Telefon")),
                  ("city", Str("Şehir")),
                  ("status", Choice("Durum (varsayılan Lead = potansiyel)", "Lead", "Active", "Passive"))),
            "companyName"),

        Tool("update_customer", "Mevcut bir müşterinin bilgilerini günceller. Sadece değişecek alanları gönder. Kullanıcı onayından sonra çalışır.",
            Props(("customerName", Str("Güncellenecek müşterinin mevcut firma adı veya yetkili adı")),
                  ("companyName", Str("Yeni firma adı")),
                  ("contactName", Str("Yeni yetkili kişi")),
                  ("email", Str("Yeni e-posta")),
                  ("phone", Str("Yeni telefon")),
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

    // =====================================================================
    // OKUMA ARAÇLARI
    // =====================================================================
    public async Task<string> ExecuteReadAsync(string name, string? argumentsJson)
    {
        try
        {
            var a = ParseArgs(argumentsJson);
            return name switch
            {
                "search_customers" => await SearchCustomers(a),
                "get_customer_details" => await GetCustomerDetails(a),
                "list_activities" => await ListActivities(a),
                "list_quotes" => await ListQuotes(a),
                "get_summary" => await GetSummary(),
                "get_sales_report" => await GetSalesReport(a),
                _ => Error($"Bilinmeyen araç: {name}")
            };
        }
        catch (ToolException ex)
        {
            return Error(ex.Message);
        }
    }

    private async Task<string> SearchCustomers(JsonObject a)
    {
        var status = ParseEnum<CustomerStatus>(S(a, "status"));
        var list = await _customers.GetAllAsync(S(a, "query"), status);
        if (list.Count == 0) return Ok(new { count = 0, message = "Eşleşen müşteri bulunamadı." });

        return Ok(new
        {
            count = list.Count,
            customers = list.Take(25).Select(c => new
            {
                c.Id, c.CompanyName, c.ContactName, c.City, c.Email, c.Phone,
                durum = Labels.Customer(c.Status), acikIsSayisi = c.OpenActivityCount, teklifSayisi = c.QuoteCount
            })
        });
    }

    private async Task<string> GetCustomerDetails(JsonObject a)
    {
        var customer = await ResolveCustomer(S(a, "customerName"));
        var c = (await _customers.GetAsync(customer.Id))!;
        var activities = await _activities.GetAllAsync(c.Id);
        var quotes = await _quotes.GetAllAsync(c.Id);

        return Ok(new
        {
            musteri = new
            {
                c.Id, c.CompanyName, c.ContactName, c.Email, c.Phone, c.City,
                durum = Labels.Customer(c.Status), kayitTarihi = _clock.Format(c.CreatedAt, false)
            },
            aktiviteler = activities.Take(30).Select(x => new
            {
                x.Id, tur = Labels.Activity(x.Type), x.Subject, x.Notes,
                tarih = _clock.Format(x.DueDate), tamamlandi = x.IsCompleted
            }),
            teklifler = quotes.Select(q => new
            {
                q.QuoteNumber, q.Title, durum = Labels.Quote(q.Status), genelToplam = q.GrandTotal,
                olusturma = _clock.Format(q.CreatedAt, false), gecerlilik = _clock.Format(q.ValidUntil, false),
                suresiDoldu = QuoteService.IsExpired(q, _clock)
            })
        });
    }

    private async Task<string> ListActivities(JsonObject a)
    {
        int? customerId = S(a, "customerName") is { } name ? (await ResolveCustomer(name)).Id : null;
        bool? completed = B(a, "onlyOpen") == true ? false : null;
        var list = await _activities.GetAllAsync(customerId, completed);

        // Tarih aralığı yerel güne göre uygulanır
        var fromDay = ParseDay(S(a, "from"));
        var toDay = ParseDay(S(a, "to"));
        if (fromDay.HasValue || toDay.HasValue)
        {
            list = list.Where(x => x.DueDate.HasValue).Where(x =>
            {
                var day = _clock.ToLocal(x.DueDate!.Value).Date;
                return (!fromDay.HasValue || day >= fromDay.Value) && (!toDay.HasValue || day <= toDay.Value);
            }).ToList();
        }

        return Ok(new
        {
            count = list.Count,
            activities = list.Take(30).Select(x => new
            {
                x.Id, musteri = x.CustomerName, tur = Labels.Activity(x.Type), x.Subject, x.Notes,
                tarih = _clock.Format(x.DueDate), tamamlandi = x.IsCompleted
            })
        });
    }

    private async Task<string> ListQuotes(JsonObject a)
    {
        int? customerId = S(a, "customerName") is { } name ? (await ResolveCustomer(name)).Id : null;
        var list = await _quotes.GetAllAsync(customerId, ParseEnum<QuoteStatus>(S(a, "status")));

        return Ok(new
        {
            count = list.Count,
            toplamTutar = list.Sum(q => q.GrandTotal),
            quotes = list.Take(30).Select(q => new
            {
                q.QuoteNumber, musteri = q.CustomerName, q.Title, durum = Labels.Quote(q.Status),
                araToplam = q.SubTotal, genelToplam = q.GrandTotal,
                olusturma = _clock.Format(q.CreatedAt, false), gecerlilik = _clock.Format(q.ValidUntil, false),
                suresiDoldu = QuoteService.IsExpired(q, _clock)
            })
        });
    }

    private async Task<string> GetSummary()
    {
        var d = await _dashboard.GetAsync();
        return Ok(new
        {
            musteriSayisi = d.CustomerCount,
            potansiyelMusteri = d.LeadCount,
            acikAktivite = d.OpenActivityCount,
            acikTeklifSayisi = d.OpenQuoteCount,
            acikTeklifTutari = d.PipelineTotal,
            kazanilanTutar = d.WonTotal,
            yaklasanIsler = d.UpcomingActivities.Select(x => new
            {
                x.Id, musteri = x.CustomerName, tur = Labels.Activity(x.Type), x.Subject, tarih = _clock.Format(x.DueDate)
            }),
            sonTeklifler = d.RecentQuotes.Select(q => new
            {
                q.QuoteNumber, musteri = q.CustomerName, q.Title, durum = Labels.Quote(q.Status), genelToplam = q.GrandTotal
            })
        });
    }

    private async Task<string> GetSalesReport(JsonObject a)
    {
        var fromDay = ParseDay(S(a, "from"));
        var toDay = ParseDay(S(a, "to")) ?? _clock.Now.Date;

        var quotes = (await _quotes.GetAllAsync())
            .Where(q =>
            {
                var day = _clock.ToLocal(q.CreatedAt).Date;
                return (!fromDay.HasValue || day >= fromDay.Value) && day <= toDay;
            }).ToList();

        decimal Sum(QuoteStatus s) => quotes.Where(q => q.Status == s).Sum(q => q.GrandTotal);
        int Count(QuoteStatus s) => quotes.Count(q => q.Status == s);

        var decided = Count(QuoteStatus.Accepted) + Count(QuoteStatus.Rejected);

        return Ok(new
        {
            donem = $"{(fromDay.HasValue ? fromDay.Value.ToString("dd.MM.yyyy") : "başlangıç")} – {toDay:dd.MM.yyyy}",
            teklifSayisi = quotes.Count,
            toplamTutar = quotes.Sum(q => q.GrandTotal),
            durumlar = Enum.GetValues<QuoteStatus>().Select(s => new { durum = Labels.Quote(s), adet = Count(s), tutar = Sum(s) }),
            kazanmaOraniYuzde = decided == 0 ? (decimal?)null : Math.Round(100m * Count(QuoteStatus.Accepted) / decided, 1),
            ortalamaTeklifTutari = quotes.Count == 0 ? 0 : Math.Round(quotes.Average(q => q.GrandTotal), 2),
            musteriBazindaKazanilan = quotes.Where(q => q.Status == QuoteStatus.Accepted)
                .GroupBy(q => q.CustomerName)
                .Select(g => new { musteri = g.Key, tutar = g.Sum(q => q.GrandTotal) })
                .OrderByDescending(x => x.tutar)
        });
    }

    // =====================================================================
    // YAZMA ARAÇLARI — 1) HAZIRLA (doğrula + özet çıkar, veritabanına dokunma)
    // =====================================================================
    public async Task<PreparedWrite> PrepareWriteAsync(string name, string? argumentsJson)
    {
        try
        {
            var a = ParseArgs(argumentsJson);
            return name switch
            {
                "create_customer" => PrepareCreateCustomer(a),
                "update_customer" => await PrepareUpdateCustomer(a),
                "create_activity" => await PrepareCreateActivity(a),
                "complete_activity" => await PrepareActivityById(a, delete: false),
                "delete_activity" => await PrepareActivityById(a, delete: true),
                "create_quote" => await PrepareCreateQuote(a),
                "update_quote_status" => await PrepareQuoteStatus(a),
                _ => new PreparedWrite($"Bilinmeyen araç: {name}")
            };
        }
        catch (ToolException ex)
        {
            return new PreparedWrite(ex.Message);
        }
    }

    private PreparedWrite PrepareCreateCustomer(JsonObject a)
    {
        var dto = new CustomerSaveDto
        {
            CompanyName = S(a, "companyName") ?? "",
            ContactName = S(a, "contactName"),
            Email = S(a, "email"),
            Phone = S(a, "phone"),
            City = S(a, "city"),
            Status = ParseEnum<CustomerStatus>(S(a, "status")) ?? CustomerStatus.Lead
        };
        Validate(dto);

        var details = new List<string>();
        AddIf(details, "Yetkili", dto.ContactName);
        AddIf(details, "E-posta", dto.Email);
        AddIf(details, "Telefon", dto.Phone);
        AddIf(details, "Şehir", dto.City);
        details.Add($"Durum: {Labels.Customer(dto.Status)}");

        return new PreparedWrite(null, "Yeni müşteri",
            $"\"{dto.CompanyName}\" adlı yeni müşteri kaydı oluşturulacak.", details, false, Serialize(dto));
    }

    private async Task<PreparedWrite> PrepareUpdateCustomer(JsonObject a)
    {
        var target = await ResolveCustomer(S(a, "customerName"));
        var current = (await _customers.GetAsync(target.Id))!;

        var dto = new CustomerSaveDto
        {
            CompanyName = S(a, "companyName") ?? current.CompanyName,
            ContactName = S(a, "contactName") ?? current.ContactName,
            Email = S(a, "email") ?? current.Email,
            Phone = S(a, "phone") ?? current.Phone,
            City = S(a, "city") ?? current.City,
            Status = ParseEnum<CustomerStatus>(S(a, "status")) ?? current.Status
        };
        Validate(dto);

        var changes = new List<string>();
        Diff(changes, "Firma adı", current.CompanyName, dto.CompanyName);
        Diff(changes, "Yetkili", current.ContactName, dto.ContactName);
        Diff(changes, "E-posta", current.Email, dto.Email);
        Diff(changes, "Telefon", current.Phone, dto.Phone);
        Diff(changes, "Şehir", current.City, dto.City);
        Diff(changes, "Durum", Labels.Customer(current.Status), Labels.Customer(dto.Status));

        if (changes.Count == 0)
            throw new ToolException("Değişecek bir alan yok; bilgiler zaten güncel.");

        return new PreparedWrite(null, "Müşteri güncelleme",
            $"{current.CompanyName} kaydında {changes.Count} alan güncellenecek.", changes, false,
            Serialize(new UpdateCustomerPayload(current.Id, dto)));
    }

    private async Task<PreparedWrite> PrepareCreateActivity(JsonObject a)
    {
        var customer = await ResolveCustomer(S(a, "customerName"));
        var type = ParseEnum<ActivityType>(S(a, "type")) ?? ActivityType.Task;
        DateTime? due = S(a, "dueDate") is { } d ? ParseLocalDateTime(d) : null;

        var dto = new ActivitySaveDto
        {
            CustomerId = customer.Id,
            Type = type,
            Subject = S(a, "subject") ?? "",
            Notes = S(a, "notes"),
            DueDate = due
        };
        Validate(dto);

        var details = new List<string> { $"Tür: {Labels.Activity(type)}", $"Konu: {dto.Subject}" };
        if (due.HasValue) details.Add($"Tarih: {_clock.Format(due)}");
        AddIf(details, "Not", dto.Notes);
        if (due.HasValue && due < DateTime.UtcNow && type != ActivityType.Note)
            details.Add("Dikkat: seçilen tarih geçmişte.");

        var what = type == ActivityType.Note ? "not eklenecek" : $"{Labels.Activity(type).ToLower(Tr)} kaydı oluşturulacak";
        var when = due.HasValue && type != ActivityType.Note ? $" ({_clock.Format(due)})" : "";

        return new PreparedWrite(null, type == ActivityType.Note ? "Not ekleme" : "Yeni aktivite",
            $"{customer.CompanyName} için {what}{when}.", details, false, Serialize(dto));
    }

    private async Task<PreparedWrite> PrepareActivityById(JsonObject a, bool delete)
    {
        var id = (int?)N(a, "activityId") ?? throw new ToolException("Aktivite numarası gerekli.");
        var activity = await _activities.GetAsync(id)
                       ?? throw new ToolException($"{id} numaralı aktivite bulunamadı.");

        if (!delete && activity.IsCompleted)
            throw new ToolException("Bu aktivite zaten tamamlanmış.");

        var details = new List<string>
        {
            $"Müşteri: {activity.CustomerName}",
            $"Tür: {Labels.Activity(activity.Type)}",
            $"Tarih: {_clock.Format(activity.DueDate)}"
        };

        return delete
            ? new PreparedWrite(null, "Aktivite silme",
                $"\"{activity.Subject}\" aktivitesi kalıcı olarak silinecek. Bu işlem geri alınamaz.",
                details, true, Serialize(new IdPayload(id)))
            : new PreparedWrite(null, "Aktiviteyi tamamla",
                $"\"{activity.Subject}\" aktivitesi tamamlandı olarak işaretlenecek.",
                details, false, Serialize(new IdPayload(id)));
    }

    private async Task<PreparedWrite> PrepareCreateQuote(JsonObject a)
    {
        var customer = await ResolveCustomer(S(a, "customerName"));

        var items = (a["items"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>()
            .Select(i => new QuoteItemSaveDto
            {
                Description = S(i, "description") ?? "",
                Quantity = N(i, "quantity") ?? 1,
                UnitPrice = N(i, "unitPrice") ?? throw new ToolException($"\"{S(i, "description")}\" kalemi için birim fiyat eksik."),
                DiscountPercent = N(i, "discountPercent") ?? 0
            }).ToList();

        DateTime? validUntil = S(a, "validUntil") is { } v
            ? _clock.ToUtc((ParseDay(v) ?? throw new ToolException("Geçerlilik tarihi anlaşılamadı.")).AddHours(23).AddMinutes(59))
            : null;

        var dto = new QuoteSaveDto
        {
            CustomerId = customer.Id,
            Title = S(a, "title") ?? "",
            TaxRate = N(a, "taxRate") ?? 20,
            ValidUntil = validUntil,
            Items = items
        };
        Validate(dto);
        foreach (var item in items) Validate(item);

        // Önizleme tutarları, kaydedilecek modelle aynı formülle hesaplanır
        var lines = items.Select(i => new QuoteItem
        {
            Description = i.Description, Quantity = i.Quantity, UnitPrice = i.UnitPrice, DiscountPercent = i.DiscountPercent
        }).ToList();
        var preview = new Quote { TaxRate = dto.TaxRate, Items = lines };

        var details = lines.Select(l =>
            $"{l.Description}: {l.Quantity.ToString("0.##", Tr)} × {Money(l.UnitPrice)}" +
            (l.DiscountPercent > 0 ? $" (%{l.DiscountPercent.ToString("0.##", Tr)} iskonto)" : "") +
            $" = {Money(l.LineTotal)}").ToList();
        details.Add($"Ara toplam: {Money(preview.SubTotal)}");
        details.Add($"KDV (%{dto.TaxRate.ToString("0.##", Tr)}): {Money(preview.TaxAmount)}");
        details.Add($"Genel toplam: {Money(preview.GrandTotal)}");
        details.Add($"Geçerlilik: {(validUntil.HasValue ? _clock.Format(validUntil, false) : "30 gün")}");

        return new PreparedWrite(null, "Yeni fiyat teklifi",
            $"{customer.CompanyName} için \"{dto.Title}\" teklifi oluşturulacak ({lines.Count} kalem, KDV dahil {Money(preview.GrandTotal)}).",
            details, false, Serialize(dto));
    }

    private async Task<PreparedWrite> PrepareQuoteStatus(JsonObject a)
    {
        var number = S(a, "quoteNumber") ?? throw new ToolException("Teklif numarası gerekli.");
        var status = ParseEnum<QuoteStatus>(S(a, "status")) ?? throw new ToolException("Geçersiz durum.");

        var quote = (await _quotes.GetAllAsync())
            .FirstOrDefault(q => q.QuoteNumber.Equals(number.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ToolException($"{number} numaralı teklif bulunamadı.");

        if (quote.Status == status)
            throw new ToolException($"{quote.QuoteNumber} zaten \"{Labels.Quote(status)}\" durumunda.");

        return new PreparedWrite(null, "Teklif durumu",
            $"{quote.QuoteNumber} teklifinin durumu \"{Labels.Quote(quote.Status)}\" → \"{Labels.Quote(status)}\" olarak değiştirilecek.",
            new List<string> { $"Müşteri: {quote.CustomerName}", $"Başlık: {quote.Title}", $"Tutar: {Money(quote.GrandTotal)}" },
            false, Serialize(new QuoteStatusPayload(quote.Id, status)));
    }

    // =====================================================================
    // YAZMA ARAÇLARI — 2) ÇALIŞTIR (kullanıcı "Onayla" dedikten sonra)
    // =====================================================================
    public async Task<string> ExecuteWriteAsync(string tool, string payloadJson)
    {
        switch (tool)
        {
            case "create_customer":
            {
                var c = await _customers.CreateAsync(Deserialize<CustomerSaveDto>(payloadJson));
                return $"Müşteri kaydedildi: {c.CompanyName}.";
            }
            case "update_customer":
            {
                var p = Deserialize<UpdateCustomerPayload>(payloadJson);
                var c = await _customers.UpdateAsync(p.Id, p.Data) ?? throw new KeyNotFoundException("Müşteri artık mevcut değil.");
                return $"{c.CompanyName} bilgileri güncellendi.";
            }
            case "create_activity":
            {
                var x = await _activities.CreateAsync(Deserialize<ActivitySaveDto>(payloadJson));
                return x.Type == ActivityType.Note
                    ? $"Not eklendi: {x.Subject} ({x.CustomerName})."
                    : $"{Labels.Activity(x.Type)} eklendi: {x.Subject} — {x.CustomerName}, {_clock.Format(x.DueDate)}.";
            }
            case "complete_activity":
            {
                var x = await _activities.SetCompletedAsync(Deserialize<IdPayload>(payloadJson).Id, true)
                        ?? throw new KeyNotFoundException("Aktivite artık mevcut değil.");
                return $"\"{x.Subject}\" tamamlandı olarak işaretlendi.";
            }
            case "delete_activity":
            {
                var ok = await _activities.DeleteAsync(Deserialize<IdPayload>(payloadJson).Id);
                return ok ? "Aktivite silindi." : throw new KeyNotFoundException("Aktivite zaten silinmiş.");
            }
            case "create_quote":
            {
                var q = await _quotes.CreateAsync(Deserialize<QuoteSaveDto>(payloadJson));
                return $"Teklif oluşturuldu: {q.QuoteNumber} — {q.CustomerName}, KDV dahil {Money(q.GrandTotal)}.";
            }
            case "update_quote_status":
            {
                var p = Deserialize<QuoteStatusPayload>(payloadJson);
                var q = await _quotes.UpdateStatusAsync(p.Id, p.Status) ?? throw new KeyNotFoundException("Teklif artık mevcut değil.");
                return $"{q.QuoteNumber} durumu \"{Labels.Quote(q.Status)}\" olarak güncellendi.";
            }
            default:
                throw new ArgumentException($"Bilinmeyen işlem: {tool}");
        }
    }

    public record UpdateCustomerPayload(int Id, CustomerSaveDto Data);
    public record IdPayload(int Id);
    public record QuoteStatusPayload(int Id, QuoteStatus Status);

    // =====================================================================
    // Yardımcılar
    // =====================================================================

    /// <summary>
    /// İsimden tek bir müşteri bulur. Bulamazsa veya birden fazla eşleşme varsa
    /// modele açıklayıcı bir hata döner; model de kullanıcıya sorar.
    /// </summary>
    private async Task<Customer> ResolveCustomer(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ToolException("Müşteri adı gerekli.");

        var matches = await _customers.FindMatchesAsync(name);
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ToolException($"\"{name}\" adında müşteri bulunamadı. Kullanıcıya doğru adı sor veya search_customers ile ara."),
            _ => throw new ToolException(
                $"\"{name}\" için birden fazla müşteri eşleşti: " +
                string.Join("; ", matches.Take(10).Select(m => $"{m.CompanyName}{(m.City is null ? "" : $" ({m.City})")}")) +
                ". Tahmin yürütme; kullanıcıya hangisini kastettiğini sor.")
        };
    }

    private static void Validate(object dto)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true))
            throw new ToolException(string.Join(" ", results.Select(r => r.ErrorMessage)));
    }

    private DateTime ParseLocalDateTime(string value)
    {
        string[] formats = { "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return _clock.ToUtc(local);

        // Saat dilimi bilgisi içeren bir değer geldiyse (ör. ...Z, +03:00) doğrudan kullan
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return dto.UtcDateTime;

        throw new ToolException("Tarih anlaşılamadı. yyyy-MM-ddTHH:mm biçimi kullanılmalı.");
    }

    private static DateTime? ParseDay(string? value)
    {
        if (value is null) return null;
        if (DateTime.TryParseExact(value[..Math.Min(10, value.Length)], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.Date;
        throw new ToolException($"Tarih anlaşılamadı: {value}. yyyy-MM-dd biçimi kullanılmalı.");
    }

    private static JsonObject ParseArgs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { throw new ToolException("Araç parametreleri okunamadı."); }
    }

    private static T? ParseEnum<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var r) && Enum.IsDefined(r) ? r : null;

    private static string? S(JsonObject o, string key)
    {
        var v = o[key]?.ToString();
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static bool? B(JsonObject o, string key)
    {
        if (o[key] is not JsonValue v) return null;
        if (v.TryGetValue<bool>(out var b)) return b;
        return bool.TryParse(v.ToString(), out var parsed) ? parsed : null;
    }

    /// <summary>Sayı okur. Model sayıyı metin olarak "2.500" ya da "2.500,50" gönderse bile doğru yorumlar.</summary>
    private static decimal? N(JsonObject o, string key)
    {
        var node = o[key];
        if (node is JsonValue v && v.TryGetValue<decimal>(out var d)) return d;

        var s = node?.ToString().Replace("₺", "").Replace("TL", "").Trim();
        if (string.IsNullOrEmpty(s)) return null;

        var turkishFormat = Regex.IsMatch(s, @"^\d{1,3}(\.\d{3})+(,\d+)?$") || Regex.IsMatch(s, @"^\d+,\d+$");
        return decimal.TryParse(s, NumberStyles.Number, turkishFormat ? Tr : CultureInfo.InvariantCulture, out var r) ? r : null;
    }

    private static string Money(decimal v) => v.ToString("N2", Tr) + " ₺";

    private static void AddIf(List<string> list, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) list.Add($"{label}: {value}");
    }

    private static void Diff(List<string> list, string label, string? before, string? after)
    {
        if (!string.Equals(before ?? "", after ?? "", StringComparison.Ordinal))
            list.Add($"{label}: {(string.IsNullOrEmpty(before) ? "—" : before)} → {(string.IsNullOrEmpty(after) ? "—" : after)}");
    }

    private static string Serialize(object o) => JsonSerializer.Serialize(o, Json);
    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)!;

    private static string Ok(object data) => JsonSerializer.Serialize(new { ok = true, data }, Json);
    private static string Error(string message) => JsonSerializer.Serialize(new { ok = false, error = message }, Json);

    // ---------- JSON Schema kısayolları ----------
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

/// <summary>Araç çalışırken oluşan, modele/kullanıcıya iletilebilecek açıklayıcı hata.</summary>
public class ToolException : Exception
{
    public ToolException(string message) : base(message) { }
}

/// <summary>Enum değerlerinin Türkçe karşılıkları.</summary>
public static class Labels
{
    public static string Customer(CustomerStatus s) => s switch
    {
        CustomerStatus.Lead => "Potansiyel", CustomerStatus.Active => "Aktif", _ => "Pasif"
    };

    public static string Activity(ActivityType t) => t switch
    {
        ActivityType.Call => "Arama", ActivityType.Meeting => "Toplantı", ActivityType.Email => "E-posta",
        ActivityType.Note => "Not", _ => "Görev"
    };

    public static string Quote(QuoteStatus s) => s switch
    {
        QuoteStatus.Draft => "Taslak", QuoteStatus.Sent => "Gönderildi",
        QuoteStatus.Accepted => "Kabul edildi", _ => "Reddedildi"
    };
}
