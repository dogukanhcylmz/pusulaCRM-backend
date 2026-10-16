using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Services.Ai;

//Enum  Türkçe karşılıkları
public static class Labels
{
    public static string Customer(CustomerStatus s) => s switch
    {
        CustomerStatus.Lead => "Potansiyel", CustomerStatus.Active => "Aktif", _ => "Pasif"
    };

    public static string Activity(ActivityType t) => t switch
    {
        ActivityType.Call => "Arama", ActivityType.Meeting => "Toplantı", ActivityType.Email => "E-posta",
        ActivityType.Note => "Not", _ => "Görev"
    };

    public static string Quote(QuoteStatus s) => s switch
    {
        QuoteStatus.Draft => "Taslak", QuoteStatus.Sent => "Gönderildi",
        QuoteStatus.Accepted => "Kabul edildi", _ => "Reddedildi"
    };
}
