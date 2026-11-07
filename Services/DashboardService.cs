using Microsoft.EntityFrameworkCore;
using PusulaCrm.Api.Data;
using PusulaCrm.Api.Dtos;
using PusulaCrm.Api.Models;
using PusulaCrm.Api.Services.Ai;

namespace PusulaCrm.Api.Services;

public class DashboardService
{
    private readonly AppDbContext _db;
    private readonly ActivityService _activities;
    private readonly QuoteService _quotes;
    private readonly LocalClock _clock;

    public DashboardService(AppDbContext db, ActivityService activities, QuoteService quotes, LocalClock clock)
    {
        _db = db;
        _activities = activities;
        _quotes = quotes;
        _clock = clock;
    }

    public async Task<DashboardDto> GetAsync()
    {
        var quotes = await _quotes.GetAllAsync();
        var open = quotes.Where(q => QuoteService.IsOpen(q, _clock)).ToList();   // süresi geçenler hariç

        return new DashboardDto(
            CustomerCount: await _db.Customers.CountAsync(),
            LeadCount: await _db.Customers.CountAsync(c => c.Status == CustomerStatus.Lead),
            OpenActivityCount: await _db.Activities.CountAsync(a => !a.IsCompleted),
            OpenQuoteCount: open.Count,
            PipelineTotal: open.Sum(q => q.GrandTotal),
            WonTotal: quotes.Where(q => q.Status == QuoteStatus.Accepted).Sum(q => q.GrandTotal),
            UpcomingActivities: await _activities.GetUpcomingAsync(5),
            RecentQuotes: quotes.Take(5).ToList());
    }
}
