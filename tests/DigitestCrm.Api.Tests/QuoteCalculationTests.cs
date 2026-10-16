using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Tests;

public class QuoteCalculationTests
{
    [Fact]
    public void LineTotal_IndirimUygulanir()
    {
        var kalem = new QuoteItem { Quantity = 3, UnitPrice = 1000m, DiscountPercent = 10 };

        Assert.Equal(2700m, kalem.LineTotal);
    }

    [Fact]
    public void Toplamlar_KdvDahilDogruHesaplanir()
    {
        var teklif = new Quote
        {
            TaxRate = 20,
            Items =
            {
                new QuoteItem { Quantity = 2, UnitPrice = 5000m },
                new QuoteItem { Quantity = 1, UnitPrice = 2000m, DiscountPercent = 50 }
            }
        };

        Assert.Equal(11000m, teklif.SubTotal);
        Assert.Equal(2200m, teklif.TaxAmount);
        Assert.Equal(13200m, teklif.GrandTotal);
    }

    [Fact]
    public void Toplamlar_KalemYoksaSifirdir()
    {
        var teklif = new Quote();

        Assert.Equal(0m, teklif.SubTotal);
        Assert.Equal(0m, teklif.GrandTotal);
    }
}
