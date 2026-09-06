namespace Gradual.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a project with a duplicate name
/// </summary>
public class Duplication : Exception_
{
    public string ProjectName { get; }

    public Duplication(string projectName) 
        : base($"A project with the name '{projectName}' already exists.")
    {
        ProjectName = projectName;
    }

    public Duplication(string projectName, string message) : base(message)
    {
        ProjectName = projectName;
    }
}
