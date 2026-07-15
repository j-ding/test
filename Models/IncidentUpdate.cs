using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SFSWebForm.Models;

public enum UpdateEntryType { Update, Resolved, Reopened, EmailSent, EmailSendFailed, ResolutionDrafted, EmailEdited, PriorityChanged, SenderChanged }

public class IncidentUpdate
{
    public int Id { get; set; }
    public int IncidentId { get; set; }

    [BindNever]
    public UpdateEntryType EntryType { get; set; } = UpdateEntryType.Update;

    [Required, Display(Name = "Update Note")]
    public string Note { get; set; } = "";

    [Required, Display(Name = "Resources Working On")]
    public string ResourcesWorkingOn { get; set; } = "";

    [Required, Display(Name = "Updated ETA")]
    public string Eta { get; set; } = "";

    [Display(Name = "Status")]
    public IncidentStatus Status { get; set; }

    [Required, Display(Name = "Next Steps")]
    public string NextSteps { get; set; } = "";

    [Required, Display(Name = "Next Update Expected")]
    public DateTime? NextExpectedUpdate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ValidateNever]
    public Incident Incident { get; set; } = null!;
}
