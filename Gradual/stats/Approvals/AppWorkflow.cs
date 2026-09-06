namespace Gradual.BusinessLogic.Approvals;

/// <summary>
/// Manages approval workflows for project changes
/// </summary>
public class AppWorkflow
{
    private readonly List<ApprovalRequest> _approvalRequests = new();
    private readonly Dictionary<string, List<string>> _approverHierarchy = new();

    public AppWorkflow()
    {
        // Define approval hierarchy
        _approverHierarchy["StatusChange"] = new List<string> { "ProjectManager", "Director" };
        _approverHierarchy["BudgetChange"] = new List<string> { "ProjectManager", "FinanceManager", "Director" };
        _approverHierarchy["ResourceAllocation"] = new List<string> { "ProjectManager", "ResourceManager" };
        _approverHierarchy["ProjectClosure"] = new List<string> { "ProjectManager", "Director", "ExecutiveOfficer" };
    }

    /// <summary>
    /// Creates an approval request
    /// </summary>
    public ApprovalRequest CreateApprovalRequest(
        Guid projectId,
        ApprovalType approvalType,
        string requestedBy,
        string description,
        Dictionary<string, object> changeDetails)
    {
        var requiredApprovers = GetRequiredApprovers(approvalType);
        
        var request = new ApprovalRequest
        {
            RequestId = Guid.NewGuid(),
            ProjectId = projectId,
            ApprovalType = approvalType,
            RequestedBy = requestedBy,
            RequestedDate = DateTime.Now,
            Description = description,
            ChangeDetails = changeDetails,
            Status = ApprovalStatus.Pending,
            RequiredApprovers = requiredApprovers,
            ApprovalChain = requiredApprovers.Select(role => new ApprovalStep
            {
                ApproverRole = role,
                Status = ApprovalStepStatus.Pending,
                Order = requiredApprovers.IndexOf(role) + 1
            }).ToList()
        };

        _approvalRequests.Add(request);
        return request;
    }

    /// <summary>
    /// Processes an approval decision
    /// </summary>
    public ApprovalResult ProcessApproval(
        Guid requestId,
        string approverName,
        string approverRole,
        ApprovalDecision decision,
        string comments)
    {
        var request = _approvalRequests.FirstOrDefault(r => r.RequestId == requestId);
        if (request == null)
        {
            return ApprovalResult.Failure("Approval request not found");
        }

        if (request.Status != ApprovalStatus.Pending)
        {
            return ApprovalResult.Failure($"Request is already {request.Status}");
        }

        // Find the current approval step
        var currentStep = request.ApprovalChain
            .FirstOrDefault(s => s.Status == ApprovalStepStatus.Pending && s.ApproverRole == approverRole);

        if (currentStep == null)
        {
            return ApprovalResult.Failure($"No pending approval step for role '{approverRole}'");
        }

        // Record the approval
        var approval = new Approval
        {
            ApprovalId = Guid.NewGuid(),
            ApproverName = approverName,
            ApproverRole = approverRole,
            Decision = decision,
            Comments = comments,
            ApprovedDate = DateTime.Now
        };

        request.Approvals.Add(approval);
        currentStep.Status = decision == ApprovalDecision.Approved 
            ? ApprovalStepStatus.Approved 
            : ApprovalStepStatus.Rejected;
        currentStep.ApprovedBy = approverName;
        currentStep.ApprovedDate = DateTime.Now;
        currentStep.Comments = comments;

        // Handle rejection
        if (decision == ApprovalDecision.Rejected)
        {
            request.Status = ApprovalStatus.Rejected;
            request.RejectionReason = comments;
            request.CompletedDate = DateTime.Now;

            return new ApprovalResult
            {
                IsSuccess = true,
                Message = $"Request rejected by {approverName} ({approverRole})",
                Request = request,
                FinalStatus = ApprovalStatus.Rejected
            };
        }

        // Check if all approvals are complete
        if (request.ApprovalChain.All(s => s.Status == ApprovalStepStatus.Approved))
        {
            request.Status = ApprovalStatus.Approved;
            request.CompletedDate = DateTime.Now;

            return new ApprovalResult
            {
                IsSuccess = true,
                Message = "All approvals completed. Request approved.",
                Request = request,
                FinalStatus = ApprovalStatus.Approved,
                IsFullyApproved = true
            };
        }

        // More approvals needed
        var nextStep = request.ApprovalChain
            .FirstOrDefault(s => s.Status == ApprovalStepStatus.Pending);

        return new ApprovalResult
        {
            IsSuccess = true,
            Message = $"Approved by {approverName}. Waiting for approval from {nextStep?.ApproverRole}",
            Request = request,
            FinalStatus = ApprovalStatus.Pending,
            NextApprover = nextStep?.ApproverRole
        };
    }

    /// <summary>
    /// Cancels an approval request
    /// </summary>
    public ApprovalResult CancelApprovalRequest(Guid requestId, string reason)
    {
        var request = _approvalRequests.FirstOrDefault(r => r.RequestId == requestId);
        if (request == null)
        {
            return ApprovalResult.Failure("Approval request not found");
        }

        if (request.Status != ApprovalStatus.Pending)
        {
            return ApprovalResult.Failure($"Cannot cancel request with status {request.Status}");
        }

        request.Status = ApprovalStatus.Cancelled;
        request.RejectionReason = reason;
        request.CompletedDate = DateTime.Now;

        return new ApprovalResult
        {
            IsSuccess = true,
            Message = "Approval request cancelled",
            Request = request,
            FinalStatus = ApprovalStatus.Cancelled
        };
    }

    /// <summary>
    /// Gets pending approvals for a specific approver role
    /// </summary>
    public List<ApprovalRequest> GetPendingApprovals(string approverRole)
    {
        return _approvalRequests
            .Where(r => r.Status == ApprovalStatus.Pending &&
                       r.ApprovalChain.Any(s => s.ApproverRole == approverRole && s.Status == ApprovalStepStatus.Pending))
            .OrderBy(r => r.RequestedDate)
            .ToList();
    }

    /// <summary>
    /// Gets approval history for a project
    /// </summary>
    public List<ApprovalRequest> GetApprovalHistory(Guid projectId)
    {
        return _approvalRequests
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.RequestedDate)
            .ToList();
    }

    /// <summary>
    /// Checks if a change requires approval
    /// </summary>
    public RequiresApprovalResult CheckIfRequiresApproval(ApprovalType changeType, Dictionary<string, object> changeDetails)
    {
        var result = new RequiresApprovalResult { ChangeType = changeType };

        switch (changeType)
        {
            case ApprovalType.StatusChange:
                if (changeDetails.ContainsKey("NewStatus"))
                {
                    var newStatus = changeDetails["NewStatus"].ToString();
                    result.RequiresApproval = newStatus == "Completed";
                    result.Reason = "Project completion requires approval";
                }
                break;

            case ApprovalType.BudgetChange:
                if (changeDetails.ContainsKey("Amount"))
                {
                    var amount = Convert.ToDecimal(changeDetails["Amount"]);
                    result.RequiresApproval = amount > 10000; // Threshold
                    result.Reason = $"Budget changes over $10,000 require approval";
                }
                break;

            case ApprovalType.ResourceAllocation:
                result.RequiresApproval = true;
                result.Reason = "All resource allocations require approval";
                break;

            case ApprovalType.ProjectClosure:
                result.RequiresApproval = true;
                result.Reason = "Project closure always requires approval";
                break;

            case ApprovalType.ScopeChange:
                result.RequiresApproval = true;
                result.Reason = "Scope changes require approval";
                break;
        }

        if (result.RequiresApproval)
        {
            result.RequiredApprovers = GetRequiredApprovers(changeType);
        }

        return result;
    }

    private List<string> GetRequiredApprovers(ApprovalType approvalType)
    {
        var key = approvalType.ToString();
        return _approverHierarchy.ContainsKey(key)
            ? _approverHierarchy[key]
            : new List<string> { "ProjectManager" };
    }
}

/// <summary>
/// Approval request
/// </summary>
public class ApprovalRequest
{
    public Guid RequestId { get; set; }
    public Guid ProjectId { get; set; }
    public ApprovalType ApprovalType { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> ChangeDetails { get; set; } = new();
    public ApprovalStatus Status { get; set; }
    public List<string> RequiredApprovers { get; set; } = new();
    public List<ApprovalStep> ApprovalChain { get; set; } = new();
    public List<Approval> Approvals { get; set; } = new();
    public string RejectionReason { get; set; } = string.Empty;
    public DateTime? CompletedDate { get; set; }
}

/// <summary>
/// Individual approval step in the chain
/// </summary>
public class ApprovalStep
{
    public int Order { get; set; }
    public string ApproverRole { get; set; } = string.Empty;
    public ApprovalStepStatus Status { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime? ApprovedDate { get; set; }
    public string Comments { get; set; } = string.Empty;
}

/// <summary>
/// Individual approval record
/// </summary>
public class Approval
{
    public Guid ApprovalId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverRole { get; set; } = string.Empty;
    public ApprovalDecision Decision { get; set; }
    public string Comments { get; set; } = string.Empty;
    public DateTime ApprovedDate { get; set; }
}

public enum ApprovalType
{
    StatusChange,
    BudgetChange,
    ResourceAllocation,
    ProjectClosure,
    ScopeChange,
    TimelineChange,
    RiskAcceptance
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

public enum ApprovalStepStatus
{
    Pending,
    Approved,
    Rejected,
    Skipped
}

public enum ApprovalDecision
{
    Approved,
    Rejected,
    RequestMoreInfo
}

public class ApprovalResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public ApprovalRequest? Request { get; set; }
    public ApprovalStatus FinalStatus { get; set; }
    public bool IsFullyApproved { get; set; }
    public string? NextApprover { get; set; }

    public static ApprovalResult Failure(string message)
        => new() { IsSuccess = false, Message = message };
}

public class RequiresApprovalResult
{
    public ApprovalType ChangeType { get; set; }
    public bool RequiresApproval { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> RequiredApprovers { get; set; } = new();
}
