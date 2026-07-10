using SFSWebForm.Models;
using System.Text;

namespace SFSWebForm.Services;

public class EmailComposerService
{
    private const string Greeting = "Dear All Employees,";

    private static string Footer(string senderTeam) =>
        $"\nPlease reach out to the {senderTeam} if you have any questions or concerns.\n\nThank you for your patience.\n\n{senderTeam}";

    public IncidentEmail ComposeInitial(Incident incident)
    {
        var subject = $"[OUTAGE NOTIFICATION] {incident.Application} – Service Disruption";
        var reason = string.IsNullOrWhiteSpace(incident.RootCause)
            ? "under investigation"
            : incident.RootCause;

        var sb = new StringBuilder();
        sb.AppendLine(Greeting);
        sb.AppendLine();
        sb.AppendLine($"We are writing to inform you that {incident.Application} is currently experiencing a service disruption.");
        sb.AppendLine();
        sb.AppendLine($"  Time of Outage:     {incident.FailureTime:MM/dd/yyyy hh:mm tt}");
        sb.AppendLine($"  System Affected:    {incident.Application}");
        if (!string.IsNullOrWhiteSpace(incident.ImpactedApps))
            sb.AppendLine($"  Also Impacted:      {incident.ImpactedApps}");
        sb.AppendLine($"  Reason for Outage:  {reason}");
        if (!string.IsNullOrWhiteSpace(incident.ResourcesWorkingOn))
            sb.AppendLine($"  Team Engaged:       {incident.ResourcesWorkingOn}");
        if (!string.IsNullOrWhiteSpace(incident.Eta))
            sb.AppendLine($"  Estimated ETA:      {incident.Eta}");
        sb.AppendLine();
        sb.AppendLine("Our team is actively investigating and working to restore service as quickly as possible. We will provide further updates as more information becomes available.");
        if (!string.IsNullOrWhiteSpace(incident.NextSteps))
        {
            sb.AppendLine();
            sb.AppendLine("NEXT STEPS:");
            sb.AppendLine(incident.NextSteps);
        }
        sb.Append(Footer(incident.SenderTeam));

        return new IncidentEmail
        {
            IncidentId = incident.Id,
            Type = EmailType.InitialOutage,
            Sequence = 1,
            Subject = subject,
            Body = sb.ToString()
        };
    }

    public IncidentEmail ComposeUpdate(Incident incident, IncidentUpdate update, int updateSequence)
    {
        var subject = $"[OUTAGE UPDATE #{updateSequence}] {incident.Application} – Service Disruption Update";

        var sb = new StringBuilder();
        sb.AppendLine(Greeting);
        sb.AppendLine();
        sb.AppendLine($"This is update #{updateSequence} regarding the ongoing service disruption affecting {incident.Application}.");
        sb.AppendLine();
        sb.AppendLine($"  Time of Original Outage:  {incident.FailureTime:MM/dd/yyyy hh:mm tt}");
        sb.AppendLine($"  System Affected:           {incident.Application}");
        sb.AppendLine($"  Current Status:            {FormatStatus(update.Status)}");
        sb.AppendLine();
        sb.AppendLine("UPDATE:");
        sb.AppendLine(update.Note);
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(update.ResourcesWorkingOn))
            sb.AppendLine($"  Team Engaged:  {update.ResourcesWorkingOn}");
        if (!string.IsNullOrWhiteSpace(update.Eta))
            sb.AppendLine($"  Updated ETA:   {update.Eta}");
        if (update.NextExpectedUpdate.HasValue)
            sb.AppendLine($"  Next Update:   {update.NextExpectedUpdate.Value:MM/dd/yyyy hh:mm tt}");
        if (!string.IsNullOrWhiteSpace(update.NextSteps))
        {
            sb.AppendLine();
            sb.AppendLine("NEXT STEPS:");
            sb.AppendLine(update.NextSteps);
        }
        sb.AppendLine();
        sb.AppendLine("We appreciate your patience and will continue to provide updates as the situation evolves.");
        sb.Append(Footer(incident.SenderTeam));

        return new IncidentEmail
        {
            IncidentId = incident.Id,
            Type = EmailType.Update,
            Sequence = updateSequence + 1,
            Subject = subject,
            Body = sb.ToString()
        };
    }

    public IncidentEmail ComposeResolution(Incident incident)
    {
        var subject = $"[RESOLVED] {incident.Application} – Service Restored";
        var resolvedTime = incident.ResolvedTime ?? DateTime.Now;
        var duration = resolvedTime - incident.FailureTime;

        var sb = new StringBuilder();
        sb.AppendLine(Greeting);
        sb.AppendLine();
        sb.AppendLine($"We are pleased to inform you that {incident.Application} has been restored to normal operation.");
        sb.AppendLine();
        sb.AppendLine($"  Time of Outage:      {incident.FailureTime:MM/dd/yyyy hh:mm tt}");
        sb.AppendLine($"  Time of Resolution:  {resolvedTime:MM/dd/yyyy hh:mm tt}");
        sb.AppendLine($"  Total Duration:      {FormatDuration(duration)}");
        sb.AppendLine($"  System Affected:     {incident.Application}");
        if (!string.IsNullOrWhiteSpace(incident.ImpactedApps))
            sb.AppendLine($"  Also Impacted:       {incident.ImpactedApps}");
        if (!string.IsNullOrWhiteSpace(incident.RootCause))
        {
            sb.AppendLine();
            sb.AppendLine("ROOT CAUSE:");
            sb.AppendLine(incident.RootCause);
        }
        if (!string.IsNullOrWhiteSpace(incident.ResolutionAction))
        {
            sb.AppendLine();
            sb.AppendLine("RESOLUTION:");
            sb.AppendLine(incident.ResolutionAction);
        }
        sb.AppendLine();
        sb.AppendLine("We apologize for any inconvenience this disruption may have caused. Steps are being taken to prevent recurrence.");
        sb.Append(Footer(incident.SenderTeam));

        return new IncidentEmail
        {
            IncidentId = incident.Id,
            Type = EmailType.Resolution,
            Sequence = 999,
            Subject = subject,
            Body = sb.ToString()
        };
    }

    private static string FormatStatus(IncidentStatus status) => status switch
    {
        IncidentStatus.Open => "Open – Under Investigation",
        IncidentStatus.InProgress => "In Progress – Actively Working",
        IncidentStatus.Resolved => "Resolved",
        _ => status.ToString()
    };

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes} minute(s)";
        return $"{(int)span.TotalHours} hour(s) {span.Minutes} minute(s)";
    }
}
