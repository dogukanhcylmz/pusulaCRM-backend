using Microsoft.EntityFrameworkCore;
using DigitestCrm.Api.Data;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Services;

/// <summary>Müşteri iş kuralları. Hem API hem de asistan bu servisi kullanır.</summary>
public class CustomerService
{
    private readonly AppDbContext _db;
    public CustomerService(AppDbContext db) => _db = db;

    public async Task<List<CustomerDto>> GetAllAsync(string? search = null, CustomerStatus? status = null)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(c => c.Status == status);

        var list = await Project(query.OrderByDescending(c => c.CreatedAt)).ToListAsync();

        // Arama Türkçe karakterlere duyarlı olsun diye bellekte yapılır.
        // (Bu ölçekte yeterli; büyük veride veritabanı tam metin araması kullanılır.)
        if (!string.IsNullOrWhiteSpace(search))
            list = list.Where(c => TextMatch.Contains(c.CompanyName, search)
                                || TextMatch.Contains(c.ContactName, search)
                                || TextMatch.Contains(c.City, search)
                                || TextMatch.Contains(c.Email, search)).ToList();
        return list;
    }

    public Task<CustomerDto?> GetAsync(int id) =>
        Project(_db.Customers.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync();

    /// <summary>
    /// Asistan için: isimden müşteri bulur. Tam eşleşme varsa yalnızca onu döner;
    /// yoksa firma veya yetkili adında geçen tüm kayıtları döner (birden fazlaysa kullanıcıya sorulur).
    /// </summary>
    public async Task<List<Customer>> FindMatchesAsync(string name)
    {
        var all = await _db.Customers.AsNoTracking().ToListAsync();

        var exact = all.Where(c => TextMatch.EqualsTr(c.CompanyName, name) || TextMatch.EqualsTr(c.ContactName, name)).ToList();
        if (exact.Count > 0) return exact;

        return all.Where(c => TextMatch.Contains(c.CompanyName, name) || TextMatch.Contains(c.ContactName, name))
                  .OrderBy(c => c.CompanyName)
                  .ToList();
    }

    public async Task<CustomerDto> CreateAsync(CustomerSaveDto dto)
    {
        var entity = new Customer();
        Apply(entity, dto);
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetAsync(entity.Id))!;
    }

    public async Task<CustomerDto?> UpdateAsync(int id, CustomerSaveDto dto)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity is null) return null;
        Apply(entity, dto);
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity is null) return false;
        _db.Customers.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    private static IQueryable<CustomerDto> Project(IQueryable<Customer> q) =>
        q.Select(c => new CustomerDto(
            c.Id, c.CompanyName, c.ContactName, c.Email, c.Phone, c.City, c.Status, c.CreatedAt,
            c.Activities.Count(a => !a.IsCompleted), c.Quotes.Count));

    private static void Apply(Customer e, CustomerSaveDto dto)
    {
        e.CompanyName = dto.CompanyName.Trim();
        e.ContactName = Clean(dto.ContactName);
        e.Email = Clean(dto.Email);
        e.Phone = Clean(dto.Phone);
        e.City = Clean(dto.City);
        e.Status = dto.Status;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
