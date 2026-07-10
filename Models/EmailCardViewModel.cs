namespace SFSWebForm.Models;

public class EmailCardViewModel(IncidentEmail email, string label, string color, bool isDark)
{
    public IncidentEmail Email { get; } = email;
    public string Label { get; } = label;
    public string Color { get; } = color;
    public bool IsDark { get; } = isDark;
}
