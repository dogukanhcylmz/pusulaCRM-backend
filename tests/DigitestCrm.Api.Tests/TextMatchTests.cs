using DigitestCrm.Api.Services;

namespace DigitestCrm.Api.Tests;

public class TextMatchTests
{
    [Theory]
    [InlineData("Ayşe Demir", "ayse")]
    [InlineData("İzmir", "IZMIR")]
    [InlineData("Işık Elektrik", "isik")]
    [InlineData("Gökçe Ünlü", "gokce unlu")]
    [InlineData("Anadolu   Lojistik", "anadolu lojistik")]
    public void Contains_TurkceKarakterFarkiniYokSayar(string kaynak, string aranan)
    {
        Assert.True(TextMatch.Contains(kaynak, aranan));
    }

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
