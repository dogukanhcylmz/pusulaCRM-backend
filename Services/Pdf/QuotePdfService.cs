using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PusulaCrm.Api.Dtos;
using PusulaCrm.Api.Models;
using PusulaCrm.Api.Services.Ai;

namespace PusulaCrm.Api.Services.Pdf;

public record QuotePdf(byte[] Content, string FileName);

/// (QuestPDF, A4)
public class QuotePdfService
{
    private const string Navy = "#0C1A41";
    private const string NavySoft = "#E9ECF4";
    private const string Zebra = "#F6F7FA";
    private const string Line = "#D5DAE5";
    private const string Muted = "#5F6B7D";
    private const string Ink = "#1F2937";

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly Lazy<byte[]> Logo = new(LoadLogo);

    private readonly QuoteService _quotes;
    private readonly CustomerService _customers;
    private readonly CompanyOptions _company;
    private readonly AssistantOptions _user;
    private readonly LocalClock _clock;

    public QuotePdfService(QuoteService quotes, CustomerService customers, IOptions<CompanyOptions> company,
                           IOptions<AssistantOptions> user, LocalClock clock)
    {
        _quotes = quotes;
        _customers = customers;
        _company = company.Value;
        _user = user.Value;
        _clock = clock;
    }

    public async Task<QuotePdf?> CreateAsync(int quoteId)
    {
        var quote = await _quotes.GetAsync(quoteId);
        if (quote is null) return null;
        var customer = await _customers.GetAsync(quote.CustomerId);

        var bytes = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(42);
            page.MarginTop(34);
            page.MarginBottom(28);
            page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(9).FontColor(Ink).LineHeight(1.3f));

            page.Header().Element(c => Header(c, quote));
            page.Content().PaddingTop(16).Element(c => Content(c, quote, customer));
            page.Footer().Element(Footer);

            if (quote.Status == QuoteStatus.Draft)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-35)
                    .Text("TASLAK").FontSize(120).Bold().FontColor("#140C1A41").LetterSpacing(0.1f); // sadece taslaklara eklendi. ön izleme de olr, sor
        })).GeneratePdf();

        return new QuotePdf(bytes, FileName(quote));
    }

    private void Header(IContainer container, QuoteDto q)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(170).AlignMiddle().Image(Logo.Value);
                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text("FİYAT TEKLİFİ").FontSize(19).Bold().FontColor(Navy).LetterSpacing(0.06f);
                    right.Item().PaddingTop(5).AlignRight().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(72); c.ConstantColumn(80); });
                        MetaRow(t, "Teklif No", q.QuoteNumber);
                        MetaRow(t, "Tarih", Date(q.CreatedAt));
                        MetaRow(t, "Geçerlilik", Date(q.ValidUntil));
                    });
                });
            });
            col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Navy);
        });
    }

    private static void MetaRow(TableDescriptor t, string label, string value)
    {
        t.Cell().AlignRight().PaddingRight(8).Text(label).FontColor(Muted);
        t.Cell().AlignRight().Text(value).SemiBold();
    }

    private void Content(IContainer container, QuoteDto q, CustomerDto? customer)
    {
        container.Column(col =>
        {
            col.Spacing(14);

            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => InfoBox(c, "TEKLİFİ VEREN", CompanyLines()));
                row.ConstantItem(14);
                row.RelativeItem().Element(c => InfoBox(c, "SAYIN", CustomerLines(q, customer)));
            });

            col.Item().Column(subject =>
            {
                subject.Item().Text(t =>
                {
                    t.Span("Konu: ").Bold().FontColor(Navy);
                    t.Span(q.Title).Bold();
                });
                subject.Item().PaddingTop(3).Text("Talebiniz doğrultusunda hazırlanan fiyat teklifimiz aşağıda bilgilerinize sunulmuştur.")
                    .FontColor(Muted);
            });

            col.Item().Element(c => ItemsTable(c, q));
            col.Item().Element(c => Totals(c, q));
            col.Item().Element(c => Terms(c, q));
            col.Item().Element(Signatures);
        });
    }

    private static void InfoBox(IContainer container, string title, List<(string Text, bool Strong)> lines)
    {
        container.Border(0.75f).BorderColor(Line).Column(col =>
        {
            col.Item().Background(NavySoft).PaddingVertical(5).PaddingHorizontal(10)
                .Text(title).FontSize(8).Bold().FontColor(Navy).LetterSpacing(0.08f);
            col.Item().PaddingVertical(8).PaddingHorizontal(10).Column(body =>
            {
                body.Spacing(2);
                foreach (var (text, strong) in lines)
                {
                    var span = body.Item().Text(text);
                    if (strong) span.Bold().FontSize(10);
                }
            });
        });
    }

    private List<(string, bool)> CompanyLines()
    {
        var c = _company;
        var lines = new List<(string, bool)> { (c.Name, true) };
        AddLine(lines, c.Address);
        AddLine(lines, Join("  ·  ", Prefix("Tel: ", c.Phone), Prefix("Faks: ", c.Fax)));
        AddLine(lines, Join("  ·  ", c.Email, c.Website));
        AddLine(lines, Join("  ·  ", Prefix("V.D.: ", c.TaxOffice), Prefix("V.No: ", c.TaxNumber)));
        AddLine(lines, Prefix("MERSİS: ", c.Mersis));
        return lines;
    }

    private static List<(string, bool)> CustomerLines(QuoteDto q, CustomerDto? c)
    {
        var lines = new List<(string, bool)> { (c?.CompanyName ?? q.CustomerName, true) };
        if (c is null) return lines;

        AddLine(lines, Prefix("Yetkili: ", Join(" – ", c.ContactName, c.ContactTitle)));
        AddLine(lines, Join(", ", c.Address, c.City));
        AddLine(lines, Join("  ·  ", Prefix("Tel: ", c.Phone), Prefix("Sabit: ", c.Landline)));
        AddLine(lines, Join("  ·  ", c.Email, c.Website));
        return lines;
    }

    private static void ItemsTable(IContainer container, QuoteDto q)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(26);
                c.RelativeColumn();
                c.ConstantColumn(52);
                c.ConstantColumn(78);
                c.ConstantColumn(50);
                c.ConstantColumn(84);
            });

            t.Header(h =>
            {
                HeadCell(h.Cell(), "#", false);
                HeadCell(h.Cell(), "ÜRÜN / HİZMET", false);
                HeadCell(h.Cell(), "MİKTAR", true);
                HeadCell(h.Cell(), "BİRİM FİYAT", true);
                HeadCell(h.Cell(), "İSKONTO", true);
                HeadCell(h.Cell(), "TUTAR", true);
            });

            for (var i = 0; i < q.Items.Count; i++)
            {
                var item = q.Items[i];
                var bg = i % 2 == 1 ? Zebra : "#FFFFFF";

                BodyCell(t.Cell(), bg).Text((i + 1).ToString()).FontColor(Muted);
                BodyCell(t.Cell(), bg).Text(item.Description);
                BodyCell(t.Cell(), bg).AlignRight().Text(item.Quantity.ToString("#,##0.##", Tr));
                BodyCell(t.Cell(), bg).AlignRight().Text(Money(item.UnitPrice));
                BodyCell(t.Cell(), bg).AlignRight().Text(item.DiscountPercent > 0 ? $"%{item.DiscountPercent.ToString("0.##", Tr)}" : "—")
                    .FontColor(item.DiscountPercent > 0 ? Ink : Muted);
                BodyCell(t.Cell(), bg).AlignRight().Text(Money(item.LineTotal)).SemiBold();
            }
        });
    }

    private static void HeadCell(IContainer cell, string text, bool right)
    {
        // Aynı renkte kenarlık: hücre aralarında beyaz çizgi kalmasın
        var c = cell.Background(Navy).Border(0.5f).BorderColor(Navy).PaddingVertical(6).PaddingHorizontal(6);
        (right ? c.AlignRight() : c).Text(text).FontSize(7.5f).Bold().FontColor(Colors.White).LetterSpacing(0.06f);
    }

    // ShowEntire: kalem satırı iki sayfaya bölünmesin, sığmazsa bütün olarak sonraki sayfaya geçsin
    private static IContainer BodyCell(IContainer cell, string background) =>
        cell.ShowEntire().Background(background).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(6);

    private static void Totals(IContainer container, QuoteDto q)
    {
        var gross = Math.Round(q.Items.Sum(i => i.Quantity * i.UnitPrice), 2);
        var discount = gross - q.SubTotal;

        container.ShowEntire().Row(row =>
        {
            // Sol: tutar yazıyla
            row.RelativeItem().PaddingRight(16).AlignBottom().Background(Zebra).Padding(10).Column(words =>
            {
                words.Item().Text("YALNIZ").FontSize(7.5f).Bold().FontColor(Muted).LetterSpacing(0.08f);
                words.Item().PaddingTop(2).Text(Capitalize(TurkishMoneyText.ToText(q.GrandTotal))).Italic();
            });

            // Sağ: toplamlar
            row.ConstantItem(220).Column(col =>
            {
                if (discount > 0)
                {
                    TotalRow(col, "Brüt Toplam", Money(gross));
                    TotalRow(col, "İskonto", $"−{Money(discount)}");
                }
                TotalRow(col, "Ara Toplam", Money(q.SubTotal));
                TotalRow(col, $"KDV (%{q.TaxRate.ToString("0.##", Tr)})", Money(q.TaxAmount));

                col.Item().PaddingTop(4).Background(Navy).PaddingVertical(7).PaddingHorizontal(10).Row(r =>
                {
                    r.RelativeItem().Text("GENEL TOPLAM").Bold().FontColor(Colors.White).LetterSpacing(0.05f);
                    r.AutoItem().Text(Money(q.GrandTotal)).FontSize(11).Bold().FontColor(Colors.White);
                });
            });
        });
    }

    private static void TotalRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(10).Row(r =>
        {
            r.RelativeItem().Text(label).FontColor(Muted);
            r.AutoItem().Text(value).SemiBold();
        });
    }

    private void Terms(IContainer container, QuoteDto q)
    {
        var terms = new List<string> { $"Bu teklif {Date(q.ValidUntil)} tarihine kadar geçerlidir." };
        terms.AddRange(_company.Terms.Where(t => !string.IsNullOrWhiteSpace(t)));
        if (!string.IsNullOrWhiteSpace(_company.Iban))
            terms.Add($"Ödemeler {Join(" ", _company.BankName, _company.Iban)} hesabına yapılabilir.");

        container.ShowEntire().Column(col =>
        {
            col.Item().Text("TEKLİF KOŞULLARI").FontSize(8).Bold().FontColor(Navy).LetterSpacing(0.08f);
            col.Item().PaddingTop(4).LineHorizontal(0.75f).LineColor(Line);
            col.Item().PaddingTop(6).Column(list =>
            {
                list.Spacing(3);
                for (var i = 0; i < terms.Count; i++)
                {
                    var n = i + 1;
                    list.Item().Row(r =>
                    {
                        r.ConstantItem(16).Text($"{n}.").FontColor(Muted);
                        r.RelativeItem().Text(terms[i]);
                    });
                }
            });
        });
    }

    private void Signatures(IContainer container)
    {
        container.PaddingTop(10).ShowEntire().Row(row =>
        {
            row.RelativeItem().Element(c => SignatureBox(c, "HAZIRLAYAN", _user.UserName, _user.UserRole, _company.Name));
            row.ConstantItem(40);
            row.RelativeItem().Element(c => SignatureBox(c, "MÜŞTERİ ONAYI", "Ad Soyad:", "Tarih:", null));
        });
    }

    private static void SignatureBox(IContainer container, string title, string line1, string line2, string? line3)
    {
        container.Column(col =>
        {
            col.Item().Text(title).FontSize(8).Bold().FontColor(Navy).LetterSpacing(0.08f);
            col.Item().PaddingTop(4).Text(line1).SemiBold();
            col.Item().Text(line2).FontColor(Muted);
            col.Item().Text(line3 ?? " ").FontColor(Muted); // boşsa da yer tutar; imza çizgileri hizalı kalsın
            col.Item().PaddingTop(34).LineHorizontal(0.75f).LineColor(Muted);
            col.Item().PaddingTop(3).Text("İmza / Kaşe").FontSize(7.5f).FontColor(Muted);
        });
    }

    // ---------- Altbilgi (her sayfada) ----------
    private void Footer(IContainer container)
    {
        var c = _company;
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.75f).LineColor(Line);
            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Column(info =>
                {
                    info.Item().Text(Join("  ·  ", c.Name, c.Address)).FontSize(7).FontColor(Muted);
                    info.Item().Text(Join("  ·  ", Prefix("Tel: ", c.Phone), Prefix("Faks: ", c.Fax), c.Email, c.Website))
                        .FontSize(7).FontColor(Muted);
                });
                row.AutoItem().AlignBottom().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7).FontColor(Muted));
                    t.Span("Sayfa ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });
    }

    // ---------- Yardımcılar ----------
    private string Date(DateTime utc) => _clock.ToLocal(utc).ToString("dd.MM.yyyy", Tr);

    private static string Money(decimal v) => $"{v.ToString("N2", Tr)} TL";

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Tr) + s[1..];

    private static string? Prefix(string prefix, string? value) => string.IsNullOrWhiteSpace(value) ? null : prefix + value;

    private static string Join(string separator, params string?[] parts) =>
        string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static void AddLine(List<(string, bool)> lines, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text)) lines.Add((text, false));
    }

    // TKL-2026-0002_Anadolu-Lojistik.pdf
    private static string FileName(QuoteDto q)
    {
        var name = new string(q.CustomerName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (name.Contains("--")) name = name.Replace("--", "-");
        return $"{q.QuoteNumber}_{name.Trim('-')}.pdf";
    }

    private static byte[] LoadLogo()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PusulaCrm.Api.Assets.logo.png")
            ?? throw new InvalidOperationException("Logo kaynağı bulunamadı (Assets/logo.png).");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
