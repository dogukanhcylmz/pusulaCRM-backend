namespace DigitestCrm.Api.Services.Pdf;

/// Tutarı yazıya çevirir: 12500.50 → "on iki bin beş yüz Türk Lirası elli Kuruş"
public static class TurkishMoneyText
{
    private static readonly string[] Ones = { "", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz" };
    private static readonly string[] Tens = { "", "on", "yirmi", "otuz", "kırk", "elli", "altmış", "yetmiş", "seksen", "doksan" };
    private static readonly string[] Scales = { "", "bin", "milyon", "milyar", "trilyon" };

    public static string ToText(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Tutar negatif olamaz.");

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var lira = (long)decimal.Truncate(amount);
        var kurus = (int)((amount - lira) * 100);

        if (lira == 0 && kurus > 0) return $"{ToWords(kurus)} Kuruş";

        var text = $"{ToWords(lira)} Türk Lirası";
        return kurus > 0 ? $"{text} {ToWords(kurus)} Kuruş" : text;
    }

    public static string ToWords(long number)
    {
        if (number == 0) return "sıfır";
        if (number >= 1_000_000_000_000_000) throw new ArgumentOutOfRangeException(nameof(number));

        var parts = new List<string>();
        for (var scale = 0; number > 0; scale++, number /= 1000)
        {
            var group = (int)(number % 1000);
            if (group == 0) continue;

            // Türkçede "bir bin" denmez, sadece "bin"
            var words = scale == 1 && group == 1 ? "" : GroupWords(group);
            parts.Insert(0, string.Join(" ", new[] { words, Scales[scale] }.Where(w => w.Length > 0)));
        }
        return string.Join(" ", parts);
    }

    private static string GroupWords(int n)
    {
        var h = n / 100;
        var t = n / 10 % 10;
        var o = n % 10;

        var words = new List<string>();
        if (h > 0) words.Add(h == 1 ? "yüz" : $"{Ones[h]} yüz"); // "bir yüz" denmez
        if (t > 0) words.Add(Tens[t]);
        if (o > 0) words.Add(Ones[o]);
        return string.Join(" ", words);
    }
}
