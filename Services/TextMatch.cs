using System.Globalization;
using System.Text;

namespace DigitestCrm.Api.Services;

/// <summary>
/// Türkçeye uygun metin karşılaştırma: büyük/küçük harf (İ/i, I/ı) ve
/// ş/s, ğ/g, ü/u, ö/o, ç/c, ı/i gibi farkları yok sayar. "ayse demir" → "Ayşe Demir", "IZMIR" → "İzmir" eşleşir.
///
/// Not: CompareOptions.IgnoreNonSpace bu işi yapmaz; ICU tr-TR kurallarında ş, ö, ü ayrı harflerdir.
/// Bu yüzden iki taraf da önce aynı biçime (Normalize) getirilip sıradan karşılaştırılır.
/// </summary>
public static class TextMatch
{
    private static readonly CultureInfo Tr = new("tr-TR");

    public static bool Contains(string? source, string term)
    {
        if (string.IsNullOrEmpty(source)) return false;
        var t = Normalize(term);
        return t.Length > 0 && Normalize(source).Contains(t, StringComparison.Ordinal);
    }

    public static bool EqualsTr(string? a, string? b) =>
        a is not null && b is not null && Normalize(a) == Normalize(b);

    /// <summary>Küçük harf (Türkçe kurallarla) + Türkçe harfler ASCII + diğer aksanlar atılır + boşluklar tekleştirilir.</summary>
    public static string Normalize(string value)
    {
        // tr-TR küçültme: "I" → "ı", "İ" → "i". Ardından ı → i yapılınca ikisi de "i" olur.
        var lower = value.Trim().ToLower(Tr);

        var sb = new StringBuilder(lower.Length);
        var lastWasSpace = false;
        foreach (var ch in lower.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;   // ö → o + ¨, ü → u + ¨, ç → c + ¸, ş → s + ¸, ğ → g + ˘, â → a + ^

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
                continue;
            }

            sb.Append(ch == 'ı' ? 'i' : ch);   // noktasız ı ayrışmaz, elle eşlenir
            lastWasSpace = false;
        }
        return sb.ToString();
    }
}
