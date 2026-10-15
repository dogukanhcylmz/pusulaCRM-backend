namespace DigitestCrm.Api.Models;

public enum ActivityType
{
    Call,     // Telefon görüşmesi
    Meeting,  // Toplantı
    Email,    // E-posta
    Task,     // Görev
    Note      // Not (müşteri kartına düşülen bilgi)
}

public class Activity
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public ActivityType Type { get; set; } = ActivityType.Task;
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
