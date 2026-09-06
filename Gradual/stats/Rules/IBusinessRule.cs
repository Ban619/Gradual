namespace Gradual.BusinessLogic.Rules;

/// <summary>
/// Interface for business rules that can be evaluated
/// </summary>
public interface IBusinessRule<in T>
{
    string RuleName { get; }
    string Description { get; }
    RuleSeverity Severity { get; }
    
    /// <summary>
    /// Evaluates the business rule
    /// </summary>
    RuleResult Evaluate(T entity);
}

/// <summary>
/// Result of a business rule evaluation
/// </summary>
public class RuleResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public RuleSeverity Severity { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static RuleResult Success() => new() { IsValid = true };
    
    public static RuleResult Failure(string message, RuleSeverity severity = RuleSeverity.Error)
        => new() { IsValid = false, Message = message, Severity = severity };
        
    public static RuleResult Warning(string message)
        => new() { IsValid = true, Message = message, Severity = RuleSeverity.Warning };
}

/// <summary>
/// Severity levels for business rules
/// </summary>
public enum RuleSeverity
{
    Information,
    Warning,
    Error,
    Critical
}
