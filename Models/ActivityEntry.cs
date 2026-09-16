namespace Gradual.Models;

public class ActivityEntry
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>Created | Updated | Deleted | StatusChanged</summary>
    public string Action      { get; set; } = string.Empty;

    public Guid   ProjectId   { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Client      { get; set; } = string.Empty;

    /// <summary>Free-text detail, e.g. "Status → Active", "Priority → High"</summary>
    public string Detail      { get; set; } = string.Empty;
}
