using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SFSWebForm.Data;
using SFSWebForm.Models;
using SFSWebForm.Services;

namespace SFSWebForm.Controllers;

[Authorize]
public class IncidentsController(AppDbContext db, EmailComposerService composer, EmailSenderService emailSender, ILogger<IncidentsController> logger) : Controller
{
    // GET /Incidents
    public async Task<IActionResult> Index()
    {
        var incidents = await db.Incidents
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
        return View(incidents);
    }

    // GET /Incidents/Create
    public IActionResult Create() => View(new Incident { FailureTime = DateTime.Now });

    // POST /Incidents/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Incident incident)
    {
        if (!ModelState.IsValid) return View(incident);

        incident.CreatedAt = DateTime.UtcNow;
        incident.UpdatedAt = DateTime.UtcNow;
        incident.Status = IncidentStatus.Open;
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        var email = composer.ComposeInitial(incident);
        db.IncidentEmails.Add(email);
        await db.SaveChangesAsync();

        logger.LogInformation("Incident {Id} created for {App} by {Reporter}", incident.Id, incident.Application, incident.ReportedBy);
        return RedirectToAction(nameof(Detail), new { id = incident.Id });
    }

    // GET /Incidents/Detail/5
    public async Task<IActionResult> Detail(int id)
    {
        var incident = await db.Incidents
            .Include(i => i.Emails.OrderBy(e => e.GeneratedAt))
            .Include(i => i.Updates.OrderBy(u => u.CreatedAt))
            .FirstOrDefaultAsync(i => i.Id == id);

        if (incident == null) return NotFound();
        return View(incident);
    }

    // GET /Incidents/AddUpdate/5
    public async Task<IActionResult> AddUpdate(int id)
    {
        var incident = await db.Incidents.FindAsync(id);
        if (incident == null) return NotFound();
        if (incident.Status == IncidentStatus.Resolved)
            return RedirectToAction(nameof(Detail), new { id });

        var model = new IncidentUpdate
        {
            IncidentId = id,
            Status = incident.Status,
            ResourcesWorkingOn = incident.ResourcesWorkingOn,
            Eta = incident.Eta
        };
        ViewBag.Incident = incident;
        return View(model);
    }

    // POST /Incidents/AddUpdate/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUpdate(int id, IncidentUpdate update)
    {
        var incident = await db.Incidents.FindAsync(id);
        if (incident == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.Incident = incident;
            return View(update);
        }

        update.Id = 0; // prevent route {id} from being bound as the entity PK
        update.IncidentId = id;
        update.EntryType = UpdateEntryType.Update;
        update.CreatedAt = DateTime.UtcNow;
        db.IncidentUpdates.Add(update);

        incident.Status = update.Status;
        if (!string.IsNullOrWhiteSpace(update.ResourcesWorkingOn))
            incident.ResourcesWorkingOn = update.ResourcesWorkingOn;
        if (!string.IsNullOrWhiteSpace(update.Eta))
            incident.Eta = update.Eta;
        incident.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var updateCount = await db.IncidentUpdates.CountAsync(u => u.IncidentId == id && u.EntryType == UpdateEntryType.Update);
        var email = composer.ComposeUpdate(incident, update, updateCount);
        db.IncidentEmails.Add(email);
        await db.SaveChangesAsync();

        logger.LogInformation("Update #{Count} added to Incident {Id}", updateCount, id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    // GET /Incidents/Resolve/5
    public async Task<IActionResult> Resolve(int id)
    {
        var incident = await db.Incidents.FindAsync(id);
        if (incident == null) return NotFound();
        if (incident.ResolvedTime == null) incident.ResolvedTime = DateTime.Now;
        return View(incident);
    }

    // POST /Incidents/Resolve/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, Incident form)
    {
        var incident = await db.Incidents.FindAsync(id);
        if (incident == null) return NotFound();

        incident.Status = IncidentStatus.Resolved;
        incident.ResolvedTime = form.ResolvedTime ?? DateTime.Now;
        incident.RootCause = form.RootCause;
        incident.ResolutionAction = form.ResolutionAction;
        incident.ImpactedApps = form.ImpactedApps;
        if (!string.IsNullOrWhiteSpace(form.SenderTeam))
            incident.SenderTeam = form.SenderTeam;
        incident.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Remove any old resolution draft so we always keep the latest
        var oldResolution = await db.IncidentEmails
            .Where(e => e.IncidentId == id && e.Type == EmailType.Resolution)
            .ToListAsync();
        db.IncidentEmails.RemoveRange(oldResolution);

        var email = composer.ComposeResolution(incident);
        db.IncidentEmails.Add(email);

        db.IncidentUpdates.Add(new IncidentUpdate
        {
            IncidentId = id,
            EntryType = UpdateEntryType.Resolved,
            Status = IncidentStatus.Resolved,
            Note = "Incident marked as resolved.",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        logger.LogInformation("Incident {Id} resolved at {Time}", id, incident.ResolvedTime);
        return RedirectToAction(nameof(Detail), new { id });
    }

    // POST /Incidents/DraftResolution/5 â€” generate/refresh draft without marking resolved
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DraftResolution(int id, Incident form)
    {
        var incident = await db.Incidents.FindAsync(id);
        if (incident == null) return NotFound();

        incident.RootCause = form.RootCause;
        incident.ResolutionAction = form.ResolutionAction;
        incident.ImpactedApps = form.ImpactedApps;
        incident.ResolvedTime = form.ResolvedTime ?? DateTime.Now;
        if (!string.IsNullOrWhiteSpace(form.SenderTeam))
            incident.SenderTeam = form.SenderTeam;
        incident.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var oldResolution = await db.IncidentEmails
            .Where(e => e.IncidentId == id && e.Type == EmailType.Resolution)
            .ToListAsync();
        db.IncidentEmails.RemoveRange(oldResolution);

        var email = composer.ComposeResolution(incident);
        db.IncidentEmails.Add(email);
        await db.SaveChangesAsync();

        logger.LogInformation("Resolution draft refreshed for Incident {Id}", id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    // POST /Incidents/EditEmail/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEmail(int id, string subject, string body)
    {
        var email = await db.IncidentEmails.FindAsync(id);
        if (email == null) return NotFound();

        email.Subject = subject;
        email.Body = body;
        await db.SaveChangesAsync();

        logger.LogInformation("Email {EmailId} manually edited on Incident {IncidentId}", id, email.IncidentId);
        return Ok();
    }

    // POST /Incidents/Reopen/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id)
    {
        var incident = await db.Incidents.FindAsync(id);
        if (incident == null) return NotFound();

        incident.Status = IncidentStatus.InProgress;
        incident.ResolvedTime = null;
        incident.UpdatedAt = DateTime.UtcNow;

        // Keep the resolution email as a historical record in the timeline.
        // The next Resolve or DraftResolution will replace it when re-resolved.

        db.IncidentUpdates.Add(new IncidentUpdate
        {
            IncidentId = id,
            EntryType = UpdateEntryType.Reopened,
            Status = IncidentStatus.InProgress,
            Note = "Incident reopened â€” investigation continuing.",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        logger.LogInformation("Incident {Id} reopened", id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    // POST /Incidents/SendEmail/5  (emailId, not incidentId)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(int id)
    {
        var email = await db.IncidentEmails
            .Include(e => e.Incident)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (email == null) return NotFound();

        var windowsIdentity = User.Identity?.Name ?? User.FindFirst("Email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "";

        try
        {
            logger.LogInformation("SendEmail request for email {EmailId} from identity '{Identity}'", id, windowsIdentity);
            await emailSender.SendAsync(email, windowsIdentity, email.Incident.Recipients);
            logger.LogInformation("Email {EmailId} dispatched by {User} for Incident {IncidentId}",
                id, windowsIdentity, email.IncidentId);
            return Ok(new { sent = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email {EmailId} for {User}", id, windowsIdentity);
            return BadRequest(new { error = ex.Message });
        }
    }
}

