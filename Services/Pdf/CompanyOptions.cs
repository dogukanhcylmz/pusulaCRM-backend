namespace DigitestCrm.Api.Services.Pdf;

/// Teklif PDF'inde görünen firma bilgileri (appsettings "Company").
/// Boş bırakılan alan PDF'te hiç gösterilmez.
public class CompanyOptions
{
    public const string Section = "Company";

    public string Name { get; set; } = "DİGİTEST A.Ş.";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? TaxOffice { get; set; }
    public string? TaxNumber { get; set; }
    public string? Mersis { get; set; }
    public string? BankName { get; set; }
    public string? Iban { get; set; }

    /// Teklif koşulları. Geçerlilik tarihi ve IBAN maddesi koddan eklenir.
    public List<string> Terms { get; set; } = new();
}
