using Gradual.Exceptions;

namespace Gradual.BusinessLogic.Rules;

/// <summary>
/// Engine for evaluating business rules
/// </summary>
public class BusinessRuleEngine<T>
{
    private readonly List<IBusinessRule<T>> _rules = new();

    public void AddRule(IBusinessRule<T> rule)
    {
        _rules.Add(rule);
    }

    public void AddRules(IEnumerable<IBusinessRule<T>> rules)
    {
        _rules.AddRange(rules);
    }

    /// <summary>
    /// Evaluates all rules and returns results
    /// </summary>
    public BusinessRuleEvaluationResult Evaluate(T entity)
    {
        var result = new BusinessRuleEvaluationResult();

        foreach (var rule in _rules)
        {
            var ruleResult = rule.Evaluate(entity);
            result.RuleResults.Add(new RuleEvaluation
            {
                RuleName = rule.RuleName,
                Result = ruleResult,
                Severity = ruleResult.Severity
            });

            if (!ruleResult.IsValid && ruleResult.Severity == RuleSeverity.Critical)
            {
                result.HasCriticalFailures = true;
            }

            if (!ruleResult.IsValid && ruleResult.Severity == RuleSeverity.Error)
            {
                result.HasErrors = true;
            }

            if (ruleResult.Severity == RuleSeverity.Warning)
            {
                result.HasWarnings = true;
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluates rules and throws exception if critical failures
    /// </summary>
    public void ValidateOrThrow(T entity)
    {
        var result = Evaluate(entity);
        
        if (result.HasCriticalFailures)
        {
            var criticalMessages = result.RuleResults
                .Where(r => !r.Result.IsValid && r.Severity == RuleSeverity.Critical)
                .Select(r => r.Result.Message);
                
            throw new BusinessRuleException($"Critical business rule violations: {string.Join("; ", criticalMessages)}");
        }

        if (result.HasErrors)
        {
            var errorMessages = result.RuleResults
                .Where(r => !r.Result.IsValid && r.Severity == RuleSeverity.Error)
                .Select(r => r.Result.Message);
                
            throw new BusinessRuleException($"Business rule violations: {string.Join("; ", errorMessages)}");
        }
    }
}

/// <summary>
/// Result of evaluating all business rules
/// </summary>
public class BusinessRuleEvaluationResult
{
    public List<RuleEvaluation> RuleResults { get; set; } = new();
    public bool HasCriticalFailures { get; set; }
    public bool HasErrors { get; set; }
    public bool HasWarnings { get; set; }
    
    public bool IsValid => !HasCriticalFailures && !HasErrors;
    
    public List<string> GetAllMessages()
    {
        return RuleResults
            .Where(r => !string.IsNullOrEmpty(r.Result.Message))
            .Select(r => $"[{r.Severity}] {r.RuleName}: {r.Result.Message}")
            .ToList();
    }

    public List<string> GetErrorMessages()
    {
        return RuleResults
            .Where(r => !r.Result.IsValid && (r.Severity == RuleSeverity.Error || r.Severity == RuleSeverity.Critical))
            .Select(r => r.Result.Message)
            .ToList();
    }

    public List<string> GetWarningMessages()
    {
        return RuleResults
            .Where(r => r.Severity == RuleSeverity.Warning)
            .Select(r => r.Result.Message)
            .ToList();
    }
}

/// <summary>
/// Single rule evaluation result
/// </summary>
public class RuleEvaluation
{
    public string RuleName { get; set; } = string.Empty;
    public RuleResult Result { get; set; } = new();
    public RuleSeverity Severity { get; set; }
}

/// <summary>
/// Exception for business rule violations
/// </summary>
public class BusinessRuleException : Exception_
{
    public BusinessRuleException(string message) : base(message) { }
    public BusinessRuleException(string message, Exception innerException) : base(message, innerException) { }
}
