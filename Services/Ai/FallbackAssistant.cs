using System.Globalization;
using System.Text;
using PusulaCrm.Api.Models;

namespace PusulaCrm.Api.Services.Ai;

//kural tabanlı yedek asistan. internetsiz çalışması için

public class FallbackAssistant
{
    private static readonly CultureInfo Tr = new("tr-TR");

    private readonly DashboardService _dashboard;
    private readonly CustomerService _customers;
    private readonly QuoteService _quotes;
    private readonly LocalClock _clock;

    public FallbackAssistant(DashboardService dashboard, CustomerService customers, QuoteService quotes, LocalClock clock)
    {
        _dashboard = dashboard;
        _customers = customers;
        _quotes = quotes;
        _clock = clock;
    }

    public async Task<string> ReplyAsync(string message)
    {
        var m = message.ToLower(Tr);

        if (m.Contains("teklif"))
        {
            var open = (await _quotes.GetAllAsync())
                .Where(q => QuoteService.IsOpen(q, _clock)).ToList();
            var sb = new StringBuilder($"{open.Count} açık teklif var, toplam {open.Sum(q => q.GrandTotal).ToString("N2", Tr)} ₺:\n");
            foreach (var q in open.Take(5))
                sb.AppendLine($"• {q.QuoteNumber} — {q.CustomerName}: {q.GrandTotal.ToString("N2", Tr)} ₺");
            return sb.ToString().TrimEnd();
        }

        if (m.Contains("aktivite") || m.Contains("görev") || m.Contains("toplantı") || m.Contains("yaklaşan"))
        {
            var d = await _dashboard.GetAsync();
            if (d.UpcomingActivities.Count == 0) return "Yaklaşan aktivite yok.";
            var sb = new StringBuilder("Yaklaşan aktiviteler:\n");
            foreach (var a in d.UpcomingActivities)
            {
                var when = a.DueDate.HasValue ? _clock.ToLocal(a.DueDate.Value).ToString("dd.MM.yyyy HH:mm") : "tarihsiz";
                sb.AppendLine($"• {when} — {a.Subject} ({a.CustomerName})");
            }
            return sb.ToString().TrimEnd();
        }

        if (m.Contains("müşteri"))
        {
            var list = await _customers.GetAllAsync();
            return $"Toplam {list.Count} müşteri kayıtlı. Son eklenenler: " +
                   string.Join(", ", list.Take(3).Select(c => c.CompanyName)) + ".";
        }

        if (m.Contains("özet") || m.Contains("durum"))
        {
            var d = await _dashboard.GetAsync();
            return $"{d.CustomerCount} müşteri ({d.LeadCount} potansiyel), {d.OpenActivityCount} açık aktivite, " +
                   $"{d.OpenQuoteCount} açık teklif ({d.PipelineTotal.ToString("N2", Tr)} ₺).";
        }

        return "Şu an basit modda çalışıyorum. " +
               "\"özet\", \"açık teklifler\", \"yaklaşan aktiviteler\" veya \"müşteriler\" yazabilirsiniz. " +
               "Kayıt oluşturma ve güncelleme için yapay zeka bağlantısı gerekir.";
    }
}
