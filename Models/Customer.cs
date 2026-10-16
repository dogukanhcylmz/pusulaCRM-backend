namespace DigitestCrm.Api.Models;

public enum CustomerStatus
{
    Lead,     // Potansiel müsteri
    Active,   
    Passive   
}

public class Customer
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Lead;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Activity> Activities { get; set; } = new();
    public List<Quote> Quotes { get; set; } = new();
}
