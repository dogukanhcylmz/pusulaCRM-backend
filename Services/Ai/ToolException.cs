namespace DigitestCrm.Api.Services.Ai;

/// <summary>Araç çalışırken oluşan, modele/kullanıcıya iletilebilecek açıklayıcı hata.</summary>
public class ToolException : Exception
{
    public ToolException(string message) : base(message) { }
}
