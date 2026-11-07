using PusulaCrm.Api.Models;

namespace PusulaCrm.Api.Data;

/// <summary>
/// Sunumda boş ekranla karşılaşmamak için örnek veriler.
/// Veritabanı boşsa uygulama ilk açılışta bunları ekler.
/// </summary>
public static class SeedData
{
    public static void Initialize(AppDbContext db)
    {
        if (db.Customers.Any()) return;

        var now = DateTime.UtcNow;

        var customers = new List<Customer>
        {
            new() { CompanyName = "Anadolu Lojistik A.Ş.", ContactName = "Mehmet Kaya",  Email = "mehmet@anadolulojistik.com", Phone = "0532 111 22 33", City = "İstanbul", Status = CustomerStatus.Active, CreatedAt = now.AddDays(-60) },
            new() { CompanyName = "Ege Tekstil Ltd.",       ContactName = "Ayşe Demir",   Email = "ayse@egetekstil.com",        Phone = "0533 222 33 44", City = "İzmir",    Status = CustomerStatus.Active, CreatedAt = now.AddDays(-45) },
            new() { CompanyName = "Başkent Yazılım",        ContactName = "Can Öztürk",   Email = "can@baskentyazilim.com",     Phone = "0534 333 44 55", City = "Ankara",   Status = CustomerStatus.Lead,   CreatedAt = now.AddDays(-20) },
            new() { CompanyName = "Karadeniz Gıda",         ContactName = "Zeynep Arslan",Email = "zeynep@karadenizgida.com",   Phone = "0535 444 55 66", City = "Trabzon",  Status = CustomerStatus.Lead,   CreatedAt = now.AddDays(-12) },
            new() { CompanyName = "Marmara Otomotiv",       ContactName = "Emre Şahin",   Email = "emre@marmaraoto.com",        Phone = "0536 555 66 77", City = "Bursa",    Status = CustomerStatus.Active, CreatedAt = now.AddDays(-90) },
            new() { CompanyName = "Ege Mobilya San.",       ContactName = "Burak Aydın",  Email = "burak@egemobilya.com",       Phone = "0538 777 88 99", City = "Denizli",  Status = CustomerStatus.Lead,   CreatedAt = now.AddDays(-5) },
            new() { CompanyName = "Akdeniz Turizm",         ContactName = "Elif Yıldız",  Email = "elif@akdenizturizm.com",     Phone = "0537 666 77 88", City = "Antalya",  Status = CustomerStatus.Passive,CreatedAt = now.AddDays(-150) },
        };
        db.Customers.AddRange(customers);
        db.SaveChanges();

        db.Activities.AddRange(
            new Activity { CustomerId = customers[0].Id, Type = ActivityType.Meeting, Subject = "Yıllık sözleşme yenileme toplantısı", DueDate = now.Date.AddDays(1).AddHours(11) },
            new Activity { CustomerId = customers[1].Id, Type = ActivityType.Call,    Subject = "Teklif hakkında geri dönüş araması",  DueDate = now.Date.AddDays(2).AddHours(7) },
            new Activity { CustomerId = customers[2].Id, Type = ActivityType.Email,   Subject = "Ürün tanıtım sunumu gönderilecek",    DueDate = now.Date.AddDays(3).AddHours(9) },
            new Activity { CustomerId = customers[3].Id, Type = ActivityType.Task,    Subject = "İhtiyaç analizi formu hazırla",       DueDate = now.Date.AddDays(5).AddHours(12) },
            new Activity { CustomerId = customers[4].Id, Type = ActivityType.Call,    Subject = "Memnuniyet görüşmesi",                DueDate = now.Date.AddDays(-3).AddHours(10), IsCompleted = true, Notes = "Hizmetten memnunlar, ek modül ile ilgileniyorlar." },
            new Activity { CustomerId = customers[0].Id, Type = ActivityType.Email,   Subject = "Fatura bilgileri güncellendi",        DueDate = now.Date.AddDays(-7).AddHours(14), IsCompleted = true },
            new Activity { CustomerId = customers[4].Id, Type = ActivityType.Note,    Subject = "Ek modül için fiyat listesi istendi", Notes = "Servis randevu modülü yerine daha küçük bir paket düşünüyorlar; bütçe 40.000 ₺ civarı.", IsCompleted = true, CreatedAt = now.AddDays(-2) }
        );

        var quotes = new List<Quote>
        {
            new()
            {
                QuoteNumber = $"TKL-{now.Year}-0001", CustomerId = customers[0].Id, Title = "Filo takip yazılımı lisansı",
                Status = QuoteStatus.Accepted, CreatedAt = now.AddDays(-30), ValidUntil = now.AddDays(0),
                Items = { new() { Description = "Filo takip lisansı (yıllık)", Quantity = 25, UnitPrice = 1800 },
                          new() { Description = "Kurulum ve eğitim", Quantity = 1, UnitPrice = 15000 } }
            },
            new()
            {
                QuoteNumber = $"TKL-{now.Year}-0002", CustomerId = customers[1].Id, Title = "Stok yönetim modülü",
                Status = QuoteStatus.Sent, CreatedAt = now.AddDays(-8), ValidUntil = now.AddDays(22),
                Items = { new() { Description = "Stok modülü", Quantity = 1, UnitPrice = 45000, DiscountPercent = 10 },
                          new() { Description = "Bakım desteği (12 ay)", Quantity = 12, UnitPrice = 1500 } }
            },
            new()
            {
                QuoteNumber = $"TKL-{now.Year}-0003", CustomerId = customers[2].Id, Title = "CRM danışmanlık paketi",
                Status = QuoteStatus.Draft, CreatedAt = now.AddDays(-2), ValidUntil = now.AddDays(28),
                Items = { new() { Description = "Danışmanlık (gün)", Quantity = 5, UnitPrice = 8000 } }
            },
            new()
            {
                QuoteNumber = $"TKL-{now.Year}-0004", CustomerId = customers[4].Id, Title = "Servis randevu sistemi",
                Status = QuoteStatus.Rejected, CreatedAt = now.AddDays(-40), ValidUntil = now.AddDays(-10),
                Items = { new() { Description = "Randevu sistemi kurulumu", Quantity = 1, UnitPrice = 60000 } }
            },
        };
        db.Quotes.AddRange(quotes);
        db.SaveChanges();
    }
}
