using Microsoft.EntityFrameworkCore;
using DigitestCrm.Api.Data;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Services;

public class CustomerService
{
    private readonly AppDbContext _db;
    public CustomerService(AppDbContext db) => _db = db;

    public async Task<List<CustomerDto>> GetAllAsync(string? search = null, CustomerStatus? status = null)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(c => c.Status == status);

        var list = await Project(query.OrderByDescending(c => c.CreatedAt)).ToListAsync();

        if (!string.IsNullOrWhiteSpace(search))  //arama bellekten
            list = list.Where(c => TextMatch.Contains(c.CompanyName, search)
                                || TextMatch.Contains(c.ContactName, search)
                                || TextMatch.Contains(c.City, search)
                                || TextMatch.Contains(c.Email, search)).ToList();
        return list;
    }

    //Müşteri listesini sayfa sayfa döner 
    public async Task<PagedResult<CustomerDto>> GetPagedAsync(string? search, CustomerStatus? status, int page, int pageSize)
    {
        if (page < 1) throw new ArgumentException("Sayfa numrası 1 ve ya daha büyük olmalıdır.");
        if (pageSize < 1 || pageSize > 100) throw new ArgumentException("Sayfa boyutu 1 ile 100 arasında olmalıdr.");

        List<CustomerDto> items;
        int total;

        if (string.IsNullOrWhiteSpace(search))
        {
            var query = _db.Customers.AsNoTracking().AsQueryable();
            if (status.HasValue) query = query.Where(c => c.Status == status);

            total = await query.CountAsync();
            items = await Project(query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                                       .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync();
        }
        else
        {
            var all = await GetAllAsync(search, status);
            total = all.Count;
            items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResult<CustomerDto>(items, page, pageSize, total, totalPages);
    }

    public Task<CustomerDto?> GetAsync(int id) =>
        Project(_db.Customers.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync();

    // isimden müşteri bulcak.
    // Tam eşleşme varsa  onu döncek ,yoksa firma veya yetkili adında geçen tüm kayıtlar
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
            c.Id, c.CompanyName, c.ContactName, c.ContactTitle, c.Email, c.Phone,
            c.Landline, c.Website, c.Address, c.City, c.Status, c.CreatedAt,
            c.Activities.Count(a => !a.IsCompleted), c.Quotes.Count));

    private static void Apply(Customer e, CustomerSaveDto dto)       //dto mapper
    {
        e.CompanyName = dto.CompanyName.Trim();
        e.ContactName = Clean(dto.ContactName);
        e.ContactTitle = Clean(dto.ContactTitle);
        e.Email = Clean(dto.Email);
        e.Phone = Clean(dto.Phone);
        e.Landline = Clean(dto.Landline);
        e.Website = Clean(dto.Website);
        e.Address = Clean(dto.Address);
        e.City = Clean(dto.City);
        e.Status = dto.Status;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
