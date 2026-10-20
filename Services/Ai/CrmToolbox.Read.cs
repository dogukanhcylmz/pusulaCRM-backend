using System.Text.Json.Nodes;
using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Services.Ai;

public partial class CrmToolbox
{
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
        var status = ParseEnum<CustomerStatus>(GetString(a, "status"));
        var list = await _customers.GetAllAsync(GetString(a, "query"), status);
        if (list.Count == 0) return Ok(new { count = 0, message = "Eşleşen müşteri bulunamadı." });

        return Ok(new
        {
            count = list.Count,
            customers = list.Take(25).Select(c => new
            {
                c.Id, c.CompanyName, c.ContactName, c.ContactTitle, c.City, c.Email, c.Phone, c.Landline,
                durum = Labels.Customer(c.Status), acikIsSayisi = c.OpenActivityCount, teklifSayisi = c.QuoteCount
            })
        });
    }

    private async Task<string> GetCustomerDetails(JsonObject a)
    {
        var customer = await ResolveCustomer(GetString(a, "customerName"));
        var c = (await _customers.GetAsync(customer.Id))!;
        var activities = await _activities.GetAllAsync(c.Id);
        var quotes = await _quotes.GetAllAsync(c.Id);

        return Ok(new
        {
            musteri = new
            {
                c.Id, c.CompanyName, c.ContactName, c.ContactTitle, c.Email, c.Phone, c.Landline,
                c.Website, c.Address, c.City,
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
        int? customerId = GetString(a, "customerName") is { } name ? (await ResolveCustomer(name)).Id : null;
        bool? completed = GetBool(a, "onlyOpen") == true ? false : null;
        var list = await _activities.GetAllAsync(customerId, completed);

        // Tarih aralığı yerel gün
        var fromDay = ParseDay(GetString(a, "from"));
        var toDay = ParseDay(GetString(a, "to"));
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
        int? customerId = GetString(a, "customerName") is { } name ? (await ResolveCustomer(name)).Id : null;
        var list = await _quotes.GetAllAsync(customerId, ParseEnum<QuoteStatus>(GetString(a, "status")));

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
        var fromDay = ParseDay(GetString(a, "from"));
        var toDay = ParseDay(GetString(a, "to")) ?? _clock.Now.Date;

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
}
