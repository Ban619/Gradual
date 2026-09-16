namespace Gradual.Exceptions;

/// <summary>
/// Exception thrown when project validation fails
/// </summary>
public class PVE : Exception_
{
    public List<string> ValidationErrors { get; }

    public PVE(List<string> validationErrors) 
        : base("Project validation failed.")
    {
        ValidationErrors = validationErrors ?? new List<string>();
    }

    public PVE(string message, List<string> validationErrors) 
        : base(message)
    {
        ValidationErrors = validationErrors ?? new List<string>();
    }

    public override string Message => 
        $"{base.Message} Errors: {string.Join("; ", ValidationErrors)}";
}
