using System.Globalization;

namespace DigitestCrm.Api.Services;

/// <summary>
/// Türkçeye uygun metin karşılaştırma: büyük/küçük harf (İ/i, I/ı) ve
/// ş/s, ğ/g, ü/u gibi işaret farklarını yok sayar. "ege tekstil" → "Ege Tekstil Ltd." eşleşir.
/// </summary>
public static class TextMatch
{
    private static readonly CompareInfo Tr = new CultureInfo("tr-TR").CompareInfo;
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static bool Contains(string? source, string term) =>
        !string.IsNullOrEmpty(source) && Tr.IndexOf(source, term.Trim(), Options) >= 0;

    public static bool EqualsTr(string? a, string? b) =>
        a is not null && b is not null && Tr.Compare(a.Trim(), b.Trim(), Options) == 0;
}
