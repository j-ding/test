using System.ComponentModel.DataAnnotations;

namespace SFSWebForm.Models;

public enum IncidentStatus
{
    Open,
    InProgress,
    Resolved
}

public class Incident
{
    public int Id { get; set; }

    [Required, Display(Name = "Date & Time of Failure")]
    public DateTime FailureTime { get; set; } = DateTime.Now;

    [Required, Display(Name = "Report Created By")]
    public string ReportedBy { get; set; } = "";

    [Required, Display(Name = "Impacted Application / System")]
    public string Application { get; set; } = "";

    [Required, Display(Name = "Summary")]
    public string Summary { get; set; } = "";

    [Required, Display(Name = "Root Cause")]
    public string RootCause { get; set; } = "";

    [Display(Name = "Status")]
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;

    [Display(Name = "Impacted Apps")]
    public string? ImpactedApps { get; set; }

    // The system/app believed to have caused the impact (distinct from ImpactedApps, which lists
    // what else was affected as a result).
    [Display(Name = "Causal System")]
    public string? CausalSystem { get; set; }

    [Required, Display(Name = "Team / Resources Engaged")]
    public string ResourcesWorkingOn { get; set; } = "";

    [Display(Name = "Resolution / Action")]
    public string? ResolutionAction { get; set; }

    [Required, Display(Name = "ETA")]
    public string Eta { get; set; } = "";

    [Required, Display(Name = "Next Steps")]
    public string NextSteps { get; set; } = "";

    [Display(Name = "Sending Team / Group")]
    public string SenderTeam { get; set; } = "IT Operations Team";

    [Display(Name = "Resolution Time")]
    public DateTime? ResolvedTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<IncidentEmail> Emails { get; set; } = new List<IncidentEmail>();
    public ICollection<IncidentUpdate> Updates { get; set; } = new List<IncidentUpdate>();
}

