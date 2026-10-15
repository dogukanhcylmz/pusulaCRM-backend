using Microsoft.EntityFrameworkCore;
using DigitestCrm.Api.Data;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Services;

public class DashboardService
{
    private readonly AppDbContext _db;
    private readonly ActivityService _activities;
    private readonly QuoteService _quotes;

    public DashboardService(AppDbContext db, ActivityService activities, QuoteService quotes)
    {
        _db = db;
        _activities = activities;
        _quotes = quotes;
    }

    public async Task<DashboardDto> GetAsync()
    {
        var quotes = await _quotes.GetAllAsync();
        var open = quotes.Where(q => q.Status is QuoteStatus.Draft or QuoteStatus.Sent).ToList();

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
