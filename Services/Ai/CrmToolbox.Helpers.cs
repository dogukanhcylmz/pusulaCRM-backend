using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Services.Ai;

/// <summary>Ortak yardımcılar: müşteri çözümleme, doğrulama, tarih/sayı ayrıştırma, JSON.</summary>
public partial class CrmToolbox
{
    // =====================================================================
    // Yardımcılar
    // =====================================================================

    /// <summary>
    /// İsimden tek bir müşteri bulur. Bulamazsa veya birden fazla eşleşme varsa
    /// modele açıklayıcı bir hata döner; model de kullanıcıya sorar.
    /// </summary>
    private async Task<Customer> ResolveCustomer(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ToolException("Müşteri adı gerekli.");

        var matches = await _customers.FindMatchesAsync(name);
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ToolException($"\"{name}\" adında müşteri bulunamadı. Kullanıcıya doğru adı sor veya search_customers ile ara."),
            _ => throw new ToolException(
                $"\"{name}\" için birden fazla müşteri eşleşti: " +
                string.Join("; ", matches.Take(10).Select(m => $"{m.CompanyName}{(m.City is null ? "" : $" ({m.City})")}")) +
                ". Tahmin yürütme; kullanıcıya hangisini kastettiğini sor.")
        };
    }

    private static void Validate(object dto)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true))
            throw new ToolException(string.Join(" ", results.Select(r => r.ErrorMessage)));
    }

    private DateTime ParseLocalDateTime(string value)
    {
        string[] formats = { "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return _clock.ToUtc(local);

        // Saat dilimi bilgisi içeren bir değer geldiyse (ör. ...Z, +03:00) doğrudan kullan
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return dto.UtcDateTime;

        throw new ToolException("Tarih anlaşılamadı. yyyy-MM-ddTHH:mm biçimi kullanılmalı.");
    }

    private static DateTime? ParseDay(string? value)
    {
        if (value is null) return null;
        if (DateTime.TryParseExact(value[..Math.Min(10, value.Length)], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.Date;
        throw new ToolException($"Tarih anlaşılamadı: {value}. yyyy-MM-dd biçimi kullanılmalı.");
    }

    private static JsonObject ParseArgs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { throw new ToolException("Araç parametreleri okunamadı."); }
    }

    private static T? ParseEnum<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var r) && Enum.IsDefined(r) ? r : null;

    private static string? GetString(JsonObject o, string key)
    {
        var v = o[key]?.ToString();
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static bool? GetBool(JsonObject o, string key)
    {
        if (o[key] is not JsonValue v) return null;
        if (v.TryGetValue<bool>(out var b)) return b;
        return bool.TryParse(v.ToString(), out var parsed) ? parsed : null;
    }

    /// <summary>Sayı okur. Model sayıyı metin olarak "2.500" ya da "2.500,50" gönderse bile doğru yorumlar.</summary>
    private static decimal? GetDecimal(JsonObject o, string key)
    {
        var node = o[key];
        if (node is JsonValue v && v.TryGetValue<decimal>(out var d)) return d;

        var s = node?.ToString().Replace("₺", "").Replace("TL", "").Trim();
        if (string.IsNullOrEmpty(s)) return null;

        var turkishFormat = Regex.IsMatch(s, @"^\d{1,3}(\.\d{3})+(,\d+)?$") || Regex.IsMatch(s, @"^\d+,\d+$");
        return decimal.TryParse(s, NumberStyles.Number, turkishFormat ? Tr : CultureInfo.InvariantCulture, out var r) ? r : null;
    }

    private static string Money(decimal v) => v.ToString("N2", Tr) + " ₺";

    private static void AddIf(List<string> list, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) list.Add($"{label}: {value}");
    }

    private static void Diff(List<string> list, string label, string? before, string? after)
    {
        if (!string.Equals(before ?? "", after ?? "", StringComparison.Ordinal))
            list.Add($"{label}: {(string.IsNullOrEmpty(before) ? "—" : before)} → {(string.IsNullOrEmpty(after) ? "—" : after)}");
    }

    private static string Serialize(object o) => JsonSerializer.Serialize(o, Json);
    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)!;

    private static string Ok(object data) => JsonSerializer.Serialize(new { ok = true, data }, Json);
    private static string Error(string message) => JsonSerializer.Serialize(new { ok = false, error = message }, Json);
}
