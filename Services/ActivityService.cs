using Microsoft.EntityFrameworkCore;
using PusulaCrm.Api.Data;
using PusulaCrm.Api.Dtos;
using PusulaCrm.Api.Models;

namespace PusulaCrm.Api.Services;

public class ActivityService
{
    private readonly AppDbContext _db;
    public ActivityService(AppDbContext db) => _db = db;

    public async Task<List<ActivityDto>> GetAllAsync(int? customerId = null, bool? completed = null)
    {
        var q = _db.Activities.AsNoTracking().AsQueryable();
        if (customerId.HasValue) q = q.Where(a => a.CustomerId == customerId);
        if (completed.HasValue) q = q.Where(a => a.IsCompleted == completed);

        return await Project(q.OrderBy(a => a.IsCompleted).ThenBy(a => a.DueDate == null).ThenBy(a => a.DueDate)).ToListAsync();
    }

    public async Task<List<ActivityDto>> GetUpcomingAsync(int take = 5) =>
        await Project(_db.Activities.AsNoTracking()
                .Where(a => !a.IsCompleted)
                .OrderBy(a => a.DueDate == null).ThenBy(a => a.DueDate)
                .Take(take))
            .ToListAsync();

    public async Task<ActivityDto> CreateAsync(ActivitySaveDto dto)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId))
            throw new KeyNotFoundException("Müşteri bulunamadı.");

        var entity = new Activity
        {
            CustomerId = dto.CustomerId,
            Type = dto.Type,
            Subject = dto.Subject.Trim(),
            Notes = dto.Notes?.Trim(),
            DueDate = dto.DueDate,
            IsCompleted = dto.Type == ActivityType.Note // not u kayıt olarak düşdündüm
        };
        _db.Activities.Add(entity);
        await _db.SaveChangesAsync();
        return (await GetAsync(entity.Id))!;
    }

    // Tür, konu, açıklama ve tarihi güncelleme.    NOT yok
    public async Task<ActivityDto?> UpdateAsync(int id, ActivityUpdateDto dto)
    {
        var entity = await _db.Activities.FindAsync(id);
        if (entity is null) return null;

        var wasNote = entity.Type == ActivityType.Note;
        var isNote = dto.Type == ActivityType.Note;
        if (wasNote != isNote)
            throw new ArgumentException(wasNote
                ? "Not başka bir aktivite türüne çevrilemez."
                : "Aktivite nota çevrilemez.");

        entity.Type = dto.Type;
        entity.Subject = dto.Subject.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        entity.DueDate = isNote ? null : dto.DueDate;
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<ActivityDto?> SetCompletedAsync(int id, bool completed)
    {
        var entity = await _db.Activities.FindAsync(id);
        if (entity is null) return null;
        entity.IsCompleted = completed;
        await _db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.Activities.FindAsync(id);
        if (entity is null) return false;
        _db.Activities.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<ActivityDto?> GetAsync(int id) =>
        Project(_db.Activities.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync();

    private static IQueryable<ActivityDto> Project(IQueryable<Activity> q) =>
        q.Select(a => new ActivityDto(
            a.Id, a.CustomerId, a.Customer!.CompanyName, a.Type, a.Subject,
            a.Notes, a.DueDate, a.IsCompleted, a.CreatedAt));
}
