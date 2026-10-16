using System.Text.Json.Nodes;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Services.Ai;

public partial class CrmToolbox
{
    //doğrulatır  özet çıkarır, db dokunmayacak
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
            CompanyName = GetString(a, "companyName") ?? "",
            ContactName = GetString(a, "contactName"),
            Email = GetString(a, "email"),
            Phone = GetString(a, "phone"),
            City = GetString(a, "city"),
            Status = ParseEnum<CustomerStatus>(GetString(a, "status")) ?? CustomerStatus.Lead
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
        var target = await ResolveCustomer(GetString(a, "customerName"));
        var current = (await _customers.GetAsync(target.Id))!;

        var dto = new CustomerSaveDto
        {
            CompanyName = GetString(a, "companyName") ?? current.CompanyName,
            ContactName = GetString(a, "contactName") ?? current.ContactName,
            Email = GetString(a, "email") ?? current.Email,
            Phone = GetString(a, "phone") ?? current.Phone,
            City = GetString(a, "city") ?? current.City,
            Status = ParseEnum<CustomerStatus>(GetString(a, "status")) ?? current.Status
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
        var customer = await ResolveCustomer(GetString(a, "customerName"));
        var type = ParseEnum<ActivityType>(GetString(a, "type")) ?? ActivityType.Task;
        DateTime? due = GetString(a, "dueDate") is { } d ? ParseLocalDateTime(d) : null;

        var dto = new ActivitySaveDto
        {
            CustomerId = customer.Id,
            Type = type,
            Subject = GetString(a, "subject") ?? "",
            Notes = GetString(a, "notes"),
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
        var id = (int?)GetDecimal(a, "activityId") ?? throw new ToolException("Aktivite numarası gerekli.");
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
        var customer = await ResolveCustomer(GetString(a, "customerName"));

        var items = (a["items"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>()
            .Select(i => new QuoteItemSaveDto
            {
                Description = GetString(i, "description") ?? "",
                Quantity = GetDecimal(i, "quantity") ?? 1,
                UnitPrice = GetDecimal(i, "unitPrice") ?? throw new ToolException($"\"{GetString(i, "description")}\" kalemi için birim fiyat eksik."),
                DiscountPercent = GetDecimal(i, "discountPercent") ?? 0
            }).ToList();

        DateTime? validUntil = GetString(a, "validUntil") is { } v
            ? _clock.ToUtc((ParseDay(v) ?? throw new ToolException("Geçerlilik tarihi anlaşılamadı.")).AddHours(23).AddMinutes(59))
            : null;

        var dto = new QuoteSaveDto
        {
            CustomerId = customer.Id,
            Title = GetString(a, "title") ?? "",
            TaxRate = GetDecimal(a, "taxRate") ?? 20,
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
        var number = GetString(a, "quoteNumber") ?? throw new ToolException("Teklif numarası gerekli.");
        var status = ParseEnum<QuoteStatus>(GetString(a, "status")) ?? throw new ToolException("Geçersiz durum.");

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

    //onaydan sonraki çalıştırma araçları
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
}
