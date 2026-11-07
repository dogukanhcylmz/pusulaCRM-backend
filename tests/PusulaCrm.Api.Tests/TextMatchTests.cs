using PusulaCrm.Api.Services;

namespace PusulaCrm.Api.Tests;

public class TextMatchTests
{
    [Fact]
    public void Contains_EslesmeYoksaFalseDoner()
    {
        Assert.False(TextMatch.Contains("Ege Tekstil", "mobilya"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Contains_KaynakBossaFalseDoner(string? kaynak)
    {
        Assert.False(TextMatch.Contains(kaynak, "ege"));
    }

    [Fact]
    public void Contains_AramaMetniBoslukluysaHicbirSeyleEslesmez()
    {
        Assert.False(TextMatch.Contains("Ege Tekstil", "   "));
    }

    [Fact]
    public void EqualsTr_BuyukKucukHarfVeTurkceHarfleriAyniSayar()
    {
        Assert.True(TextMatch.EqualsTr("ŞAHİN İNŞAAT", "sahin insaat"));
        Assert.False(TextMatch.EqualsTr("Şahin İnşaat", "Şahin"));
    }
}
