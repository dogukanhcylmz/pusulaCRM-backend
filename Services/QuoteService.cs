using Microsoft.EntityFrameworkCore;
using DigitestCrm.Api.Data;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Services;

public class QuoteService
{
    private readonly AppDbContext _db;
    public QuoteService(AppDbContext db) => _db = db;

    public async Task<List<QuoteDto>> GetAllAsync(int? customerId = null, QuoteStatus? status = null)
    {
        var q = Base();
        if (customerId.HasValue) q = q.Where(x => x.CustomerId == customerId);
        if (status.HasValue) q = q.Where(x => x.Status == status);

        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<QuoteDto?> GetAsync(int id)
    {
        var entity = await Base().FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<QuoteDto> CreateAsync(QuoteSaveDto dto)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId))
            throw new KeyNotFoundException("Müşteri bulunamadı.");
        if (dto.Items.Count == 0)
            throw new ArgumentException("Teklifte en az bir kalem olmalıdır.");

        var entity = new Quote
        {
            QuoteNumber = await NextNumberAsync(),
            CustomerId = dto.CustomerId,
            Title = dto.Title.Trim(),
            TaxRate = dto.TaxRate,
            ValidUntil = dto.ValidUntil ?? DateTime.UtcNow.AddDays(30),
            Items = dto.Items.Select(i => new QuoteItem
            {
                Description = i.Description.Trim(),
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountPercent = i.DiscountPercent
            }).ToList()
        };

        _db.Quotes.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetAsync(entity.Id))!;
    }

    public async Task<QuoteDto?> UpdateStatusAsync(int id, QuoteStatus status)
    {
        var entity = await _db.Quotes.FindAsync(id);
        if (entity is null) return null;
        entity.Status = status;
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.Quotes.FindAsync(id);
        if (entity is null) return false;
        _db.Quotes.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>TKL-2026-0001 biçiminde yılın bir sonraki teklif numarası.</summary>
    private async Task<string> NextNumberAsync()
    {
        var prefix = $"TKL-{DateTime.UtcNow.Year}-";
        var numbers = await _db.Quotes
            .Where(q => q.QuoteNumber.StartsWith(prefix))
            .Select(q => q.QuoteNumber)
            .ToListAsync();

        var last = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var x) ? x : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{last + 1:D4}";
    }

    private IQueryable<Quote> Base() =>
        _db.Quotes.AsNoTracking().Include(q => q.Customer).Include(q => q.Items);

    private static QuoteDto ToDto(Quote q) => new(
        q.Id, q.QuoteNumber, q.CustomerId, q.Customer?.CompanyName ?? "", q.Title, q.Status,
        q.TaxRate, q.ValidUntil, q.CreatedAt, q.SubTotal, q.TaxAmount, q.GrandTotal,
        q.Items.Select(i => new QuoteItemDto(i.Id, i.Description, i.Quantity, i.UnitPrice, i.DiscountPercent, i.LineTotal)).ToList());
}
