namespace Gradual.BusinessLogic.Financial;

/// <summary>
/// Manages project budgets and financial tracking
/// </summary>
public class Budget_Manager
{
    /// <summary>
    /// Calculates budget utilization percentage
    /// </summary>
    public decimal CalculateBudgetUtilization(ProjectBudget budget)
    {
        if (budget.AllocatedBudget <= 0)
            return 0;

        return (budget.SpentAmount / budget.AllocatedBudget) * 100;
    }

    /// <summary>
    /// Checks if project is over budget
    /// </summary>
    public BudgetStatus GetBudgetStatus(ProjectBudget budget)
    {
        var utilization = CalculateBudgetUtilization(budget);

        if (utilization >= 100)
            return BudgetStatus.OverBudget;
        if (utilization >= 90)
            return BudgetStatus.NearLimit;
        if (utilization >= 75)
            return BudgetStatus.Warning;
        
        return BudgetStatus.OnTrack;
    }

    /// <summary>
    /// Validates budget allocation
    /// </summary>
    public BudgetValidationResult ValidateBudgetAllocation(ProjectBudget budget)
    {
        var result = new BudgetValidationResult { IsValid = true };

        if (budget.AllocatedBudget < 0)
        {
            result.IsValid = false;
            result.Errors.Add("Allocated budget cannot be negative");
        }

        if (budget.SpentAmount < 0)
        {
            result.IsValid = false;
            result.Errors.Add("Spent amount cannot be negative");
        }

        if (budget.SpentAmount > budget.AllocatedBudget)
        {
            result.Warnings.Add($"Project is over budget by {budget.Currency}{budget.SpentAmount - budget.AllocatedBudget:N2}");
        }

        var utilization = CalculateBudgetUtilization(budget);
        if (utilization >= 90 && utilization < 100)
        {
            result.Warnings.Add($"Budget utilization is at {utilization:F1}% - approaching limit");
        }

        return result;
    }

    /// <summary>
    /// Records an expense against the budget
    /// </summary>
    public ExpenseResult RecordExpense(ProjectBudget budget, decimal amount, string category, string description)
    {
        if (amount <= 0)
        {
            return ExpenseResult.Failure("Expense amount must be positive");
        }

        var newSpent = budget.SpentAmount + amount;
        var newUtilization = (newSpent / budget.AllocatedBudget) * 100;

        var expense = new ProjectExpense
        {
            ExpenseId = Guid.NewGuid(),
            Amount = amount,
            Category = category,
            Description = description,
            Date = DateTime.Now
        };

        budget.Expenses.Add(expense);
        budget.SpentAmount = newSpent;
        budget.RemainingBudget = budget.AllocatedBudget - newSpent;

        var result = new ExpenseResult
        {
            IsSuccess = true,
            Expense = expense,
            NewSpentAmount = newSpent,
            RemainingBudget = budget.RemainingBudget,
            Utilization = newUtilization
        };

        // Add warnings
        if (newSpent > budget.AllocatedBudget)
        {
            result.Warnings.Add($"OVER BUDGET: Exceeded by {budget.Currency}{newSpent - budget.AllocatedBudget:N2}");
        }
        else if (newUtilization >= 90)
        {
            result.Warnings.Add($"WARNING: Budget at {newUtilization:F1}% - approaching limit");
        }

        return result;
    }

    /// <summary>
    /// Forecasts budget burn rate
    /// </summary>
    public BudgetForecast ForecastBudget(ProjectBudget budget, DateTime projectStartDate, DateTime projectEndDate)
    {
        var totalDays = (projectEndDate - projectStartDate).Days;
        var elapsedDays = (DateTime.Now - projectStartDate).Days;
        var remainingDays = (projectEndDate - DateTime.Now).Days;

        if (totalDays <= 0 || elapsedDays <= 0)
        {
            return new BudgetForecast { IsValid = false, Message = "Invalid project dates" };
        }

        var dailyBurnRate = budget.SpentAmount / elapsedDays;
        var projectedTotalSpend = dailyBurnRate * totalDays;
        var projectedOverrun = projectedTotalSpend - budget.AllocatedBudget;

        return new BudgetForecast
        {
            IsValid = true,
            DailyBurnRate = dailyBurnRate,
            ProjectedTotalSpend = projectedTotalSpend,
            ProjectedOverrun = projectedOverrun,
            ProjectedCompletionDate = projectEndDate,
            WillExceedBudget = projectedOverrun > 0,
            DaysRemaining = remainingDays,
            BudgetRemaining = budget.RemainingBudget,
            Message = projectedOverrun > 0 
                ? $"ALERT: Projected to exceed budget by {budget.Currency}{projectedOverrun:N2}"
                : $"On track: {budget.Currency}{-projectedOverrun:N2} under budget projected"
        };
    }
}

/// <summary>
/// Project budget information
/// </summary>
public class ProjectBudget
{
    public Guid ProjectId { get; set; }
    public decimal AllocatedBudget { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingBudget { get; set; }
    public string Currency { get; set; } = "USD";
    public List<ProjectExpense> Expenses { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

/// <summary>
/// Individual project expense
/// </summary>
public class ProjectExpense
{
    public Guid ExpenseId { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

/// <summary>
/// Budget status indicator
/// </summary>
public enum BudgetStatus
{
    OnTrack,
    Warning,
    NearLimit,
    OverBudget
}

/// <summary>
/// Result of budget validation
/// </summary>
public class BudgetValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of recording an expense
/// </summary>
public class ExpenseResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public ProjectExpense? Expense { get; set; }
    public decimal NewSpentAmount { get; set; }
    public decimal RemainingBudget { get; set; }
    public decimal Utilization { get; set; }
    public List<string> Warnings { get; set; } = new();

    public static ExpenseResult Failure(string message) 
        => new() { IsSuccess = false, Message = message };
}

/// <summary>
/// Budget forecast information
/// </summary>
public class BudgetForecast
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal DailyBurnRate { get; set; }
    public decimal ProjectedTotalSpend { get; set; }
    public decimal ProjectedOverrun { get; set; }
    public DateTime ProjectedCompletionDate { get; set; }
    public bool WillExceedBudget { get; set; }
    public int DaysRemaining { get; set; }
    public decimal BudgetRemaining { get; set; }
}
