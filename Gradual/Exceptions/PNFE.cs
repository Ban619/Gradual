namespace Gradual.Exceptions;

/// <summary>
/// Exception thrown when a project is not found
/// </summary>
public class PNFE : Exception_
{
    public Guid ProjectId { get; }

    public PNFE(Guid projectId) 
        : base($"Project with ID '{projectId}' was not found.")
    {
        ProjectId = projectId;
    }

    public PNFE(Guid projectId, string message) : base(message)
    {
        ProjectId = projectId;
    }
}
