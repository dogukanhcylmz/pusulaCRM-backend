using System.ComponentModel.DataAnnotations;
using PusulaCrm.Api.Models;

namespace PusulaCrm.Api.Dtos;

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record CustomerDto(
    int Id, string CompanyName, string? ContactName, string? ContactTitle, string? Email, string? Phone,
    string? Landline, string? Website, string? Address, string? City,
    CustomerStatus Status, DateTime CreatedAt, int OpenActivityCount, int QuoteCount);

public class CustomerSaveDto
{
    [Required(ErrorMessage = "Firma adı zorunludur."), MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;
    [MaxLength(150)] public string? ContactName { get; set; }
    [MaxLength(100)] public string? ContactTitle { get; set; }
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")] public string? Email { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(30)] public string? Landline { get; set; }
    [MaxLength(200)] public string? Website { get; set; }
    [MaxLength(500)] public string? Address { get; set; }
    [MaxLength(80)] public string? City { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Lead;
}

public record ActivityDto(
    int Id, int CustomerId, string CustomerName, ActivityType Type, string Subject,
    string? Notes, DateTime? DueDate, bool IsCompleted, DateTime CreatedAt);

public class ActivitySaveDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Müşteri seçilmelidir.")]
    public int CustomerId { get; set; }
    public ActivityType Type { get; set; } = ActivityType.Task;
    [Required(ErrorMessage = "Konu zorunludur."), MaxLength(300)]
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
}

public class ActivityUpdateDto
{
    public ActivityType Type { get; set; } = ActivityType.Task;
    [Required(ErrorMessage = "Konu zorunludur."), MaxLength(300)]
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
}

public record QuoteItemDto(int Id, string Description, decimal Quantity, decimal UnitPrice, decimal DiscountPercent, decimal LineTotal);

public record QuoteDto(
    int Id, string QuoteNumber, int CustomerId, string CustomerName, string Title, QuoteStatus Status,
    decimal TaxRate, DateTime ValidUntil, DateTime CreatedAt,
    decimal SubTotal, decimal TaxAmount, decimal GrandTotal, List<QuoteItemDto> Items);

public class QuoteItemSaveDto
{
    [Required, MaxLength(300)] public string Description { get; set; } = string.Empty;
    [Range(0.01, 1_000_000)] public decimal Quantity { get; set; } = 1;
    [Range(0, 100_000_000)] public decimal UnitPrice { get; set; }
    [Range(0, 100)] public decimal DiscountPercent { get; set; }
}

public class QuoteSaveDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Müşteri seçilmelidir.")]
    public int CustomerId { get; set; }
    [Required(ErrorMessage = "Başlık zorunludur."), MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [Range(0, 100)] public decimal TaxRate { get; set; } = 20;
    public DateTime? ValidUntil { get; set; }
    [MinLength(1, ErrorMessage = "En az bir kalem eklenmelidir.")]
    public List<QuoteItemSaveDto> Items { get; set; } = new();
}

public class QuoteUpdateDto
{
    [Required(ErrorMessage = "Başlık zorunludur."), MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [Range(0, 100)] public decimal TaxRate { get; set; } = 20;
    public DateTime? ValidUntil { get; set; }
    [MinLength(1, ErrorMessage = "En az bir kalem eklenmelidir.")]
    public List<QuoteItemSaveDto> Items { get; set; } = new();
}

public record QuoteStatusDto(QuoteStatus Status);

public record DashboardDto(
    int CustomerCount, int LeadCount, int OpenActivityCount, int OpenQuoteCount,
    decimal PipelineTotal, decimal WonTotal,
    List<ActivityDto> UpcomingActivities, List<QuoteDto> RecentQuotes);

public record ChatMessageDto(string Role, string Content);

public record ChatPageContextDto(string? Page, int? CustomerId, int? QuoteId);

public record ChatRequestDto(List<ChatMessageDto> Messages, ChatPageContextDto? Context);

///onayla denmesini beklesin
public record PendingActionDto(string Id, string Title, string Summary, List<string> Details, bool Danger);

public record ChatResponseDto(string Reply, List<string> Actions, bool AiEnabled, PendingActionDto? Pending = null);

public record ConfirmResultDto(bool Ok, string Message);

public record CurrentUserDto(string Name, string Role, string Team, string ProductName);
