using DigitestCrm.Api.Data;
using DigitestCrm.Api.Models;
using DigitestCrm.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DigitestCrm.Api.Tests;

public class CustomerPagingTests
{
    // guid bos veitabaninda calisşin diye
    private static AppDbContext YeniDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<CustomerService> OrnekVeriyleServis(int musteriSayisi)
    {
        var db = YeniDb();
        var baslangic = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 1; i <= musteriSayisi; i++)
        {
            db.Customers.Add(new Customer
            {
                CompanyName = $"Firma {i}",
                City = i % 2 == 0 ? "İzmir" : "Ankara",
                Status = i <= 3 ? CustomerStatus.Active : CustomerStatus.Lead,
                CreatedAt = baslangic.AddDays(i)
            });
        }
        await db.SaveChangesAsync();
        return new CustomerService(db);
    }

    [Fact]
    public async Task IlkSayfa_EnYeniKayitlarlaBaslar()
    {
        var servis = await OrnekVeriyleServis(7);

        var sonuc = await servis.GetPagedAsync(null, null, page: 1, pageSize: 3);

        Assert.Equal(7, sonuc.TotalCount);
        Assert.Equal(3, sonuc.TotalPages);
        Assert.Equal(new[] { "Firma 7", "Firma 6", "Firma 5" }, sonuc.Items.Select(c => c.CompanyName));
    }

    [Fact]
    public async Task DurumFiltresi_ToplamiDaFiltreler()
    {
        var servis = await OrnekVeriyleServis(7);

        var sonuc = await servis.GetPagedAsync(null, CustomerStatus.Active, page: 1, pageSize: 2);

        Assert.Equal(3, sonuc.TotalCount);
        Assert.Equal(2, sonuc.TotalPages);
        Assert.All(sonuc.Items, c => Assert.Equal(CustomerStatus.Active, c.Status));
    }

}
