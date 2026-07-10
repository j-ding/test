using System.ComponentModel.DataAnnotations;
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

    // Comma-separated. Editable per email so each notification can go to a different list;
    // falls back to MailSettings.DefaultRecipients when blank.
    [Display(Name = "Recipients")]
    public string? Recipients { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? LastSendError { get; set; }

    [ValidateNever]
    public Incident Incident { get; set; } = null!;
}
