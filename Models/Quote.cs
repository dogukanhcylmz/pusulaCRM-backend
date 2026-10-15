namespace DigitestCrm.Api.Models;

public enum QuoteStatus
{
    Draft,     // Taslak
    Sent,      // Gönderildi
    Accepted,  // Kabul edildi
    Rejected   // Reddedildi
}

public class Quote
{
    public int Id { get; set; }
    public string QuoteNumber { get; set; } = string.Empty; // Örn: TKL-2026-0001
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string Title { get; set; } = string.Empty;
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public decimal TaxRate { get; set; } = 20m; // KDV %
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<QuoteItem> Items { get; set; } = new();

    // Hesaplanan alanlar veritabanına yazılmaz (bkz. AppDbContext)
    public decimal SubTotal => Items.Sum(i => i.LineTotal);
    public decimal TaxAmount => Math.Round(SubTotal * TaxRate / 100m, 2);
    public decimal GrandTotal => SubTotal + TaxAmount;
}

public class QuoteItem
{
    public int Id { get; set; }
    public int QuoteId { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }

    public decimal LineTotal => Math.Round(Quantity * UnitPrice * (1 - DiscountPercent / 100m), 2);
}
