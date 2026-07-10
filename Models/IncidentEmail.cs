using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SFSWebForm.Models;

public enum EmailType
{
    InitialOutage,
    Update,
    Resolution
}

public class IncidentEmail
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public EmailType Type { get; set; }
    public int Sequence { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [ValidateNever]
    public Incident Incident { get; set; } = null!;
}
