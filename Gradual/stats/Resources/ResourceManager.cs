namespace Gradual.BusinessLogic.Resources;

/// <summary>
/// Manages resource allocation and capacity planning
/// </summary>
public class ResourceManager
{
    private readonly List<Resource> _resources = new();
    private readonly List<ResourceAllocation> _allocations = new();

    /// <summary>
    /// Adds a resource to the pool
    /// </summary>
    public void AddResource(Resource resource)
    {
        _resources.Add(resource);
    }

    /// <summary>
    /// Allocates a resource to a project
    /// </summary>
    public AllocationResult AllocateResource(Guid resourceId, Guid projectId, decimal hoursPerWeek, DateTime startDate, DateTime endDate)
    {
        var resource = _resources.FirstOrDefault(r => r.ResourceId == resourceId);
        if (resource == null)
        {
            return AllocationResult.Failure("Resource not found");
        }

        // Check if resource is available
        var availability = CheckAvailability(resourceId, startDate, endDate);
        if (!availability.IsAvailable)
        {
            return AllocationResult.Failure($"Resource not available: {availability.Reason}");
        }

        // Check capacity
        var capacityCheck = CheckCapacity(resourceId, hoursPerWeek, startDate, endDate);
        if (!capacityCheck.IsSuccess)
        {
            return AllocationResult.Failure(capacityCheck.Message);
        }

        var allocation = new ResourceAllocation
        {
            AllocationId = Guid.NewGuid(),
            ResourceId = resourceId,
            ProjectId = projectId,
            HoursPerWeek = hoursPerWeek,
            StartDate = startDate,
            EndDate = endDate,
            Status = AllocationStatus.Active
        };

        _allocations.Add(allocation);

        return new AllocationResult
        {
            IsSuccess = true,
            Message = $"Resource '{resource.Name}' allocated successfully",
            Allocation = allocation,
            RemainingCapacity = resource.MaxHoursPerWeek - GetCurrentUtilization(resourceId, DateTime.Now)
        };
    }

    /// <summary>
    /// Checks if resource is available for allocation
    /// </summary>
    public AvailabilityResult CheckAvailability(Guid resourceId, DateTime startDate, DateTime endDate)
    {
        var resource = _resources.FirstOrDefault(r => r.ResourceId == resourceId);
        if (resource == null)
        {
            return new AvailabilityResult
            {
                IsAvailable = false,
                Reason = "Resource not found"
            };
        }

        if (!resource.IsActive)
        {
            return new AvailabilityResult
            {
                IsAvailable = false,
                Reason = "Resource is not active"
            };
        }

        // Check for conflicting allocations
        var conflicts = _allocations.Where(a =>
            a.ResourceId == resourceId &&
            a.Status == AllocationStatus.Active &&
            !(endDate < a.StartDate || startDate > a.EndDate)
        ).ToList();

        if (conflicts.Any())
        {
            return new AvailabilityResult
            {
                IsAvailable = false,
                Reason = $"Resource has {conflicts.Count} conflicting allocation(s)",
                ConflictingAllocations = conflicts
            };
        }

        return new AvailabilityResult
        {
            IsAvailable = true,
            Reason = "Resource is available"
        };
    }

    /// <summary>
    /// Checks if resource has capacity for additional allocation
    /// </summary>
    public CapacityResult CheckCapacity(Guid resourceId, decimal requestedHours, DateTime startDate, DateTime endDate)
    {
        var resource = _resources.FirstOrDefault(r => r.ResourceId == resourceId);
        if (resource == null)
        {
            return CapacityResult.Failure("Resource not found");
        }

        var currentUtilization = GetCurrentUtilization(resourceId, startDate);
        var availableCapacity = resource.MaxHoursPerWeek - currentUtilization;

        if (requestedHours > availableCapacity)
        {
            return new CapacityResult
            {
                IsSuccess = false,
                Message = $"Insufficient capacity. Requested: {requestedHours}h, Available: {availableCapacity}h",
                CurrentUtilization = currentUtilization,
                AvailableCapacity = availableCapacity,
                UtilizationPercentage = (currentUtilization / resource.MaxHoursPerWeek) * 100
            };
        }

        var newUtilization = currentUtilization + requestedHours;
        var utilizationPercentage = (newUtilization / resource.MaxHoursPerWeek) * 100;

        return new CapacityResult
        {
            IsSuccess = true,
            Message = "Capacity available",
            CurrentUtilization = currentUtilization,
            AvailableCapacity = availableCapacity,
            NewUtilization = newUtilization,
            UtilizationPercentage = utilizationPercentage,
            IsOverCapacity = utilizationPercentage > 100
        };
    }

    /// <summary>
    /// Gets current utilization of a resource
    /// </summary>
    public decimal GetCurrentUtilization(Guid resourceId, DateTime date)
    {
        return _allocations
            .Where(a =>
                a.ResourceId == resourceId &&
                a.Status == AllocationStatus.Active &&
                date >= a.StartDate &&
                date <= a.EndDate)
            .Sum(a => a.HoursPerWeek);
    }

    /// <summary>
    /// Gets resource capacity report
    /// </summary>
    public ResourceCapacityReport GetCapacityReport(DateTime startDate, DateTime endDate)
    {
        var report = new ResourceCapacityReport
        {
            StartDate = startDate,
            EndDate = endDate,
            GeneratedDate = DateTime.Now
        };

        foreach (var resource in _resources.Where(r => r.IsActive))
        {
            var avgUtilization = GetAverageUtilization(resource.ResourceId, startDate, endDate);
            var utilizationPercentage = (avgUtilization / resource.MaxHoursPerWeek) * 100;

            report.ResourceCapacities.Add(new ResourceCapacityInfo
            {
                ResourceId = resource.ResourceId,
                ResourceName = resource.Name,
                Role = resource.Role,
                MaxHoursPerWeek = resource.MaxHoursPerWeek,
                AverageUtilization = avgUtilization,
                UtilizationPercentage = utilizationPercentage,
                CapacityStatus = GetCapacityStatus(utilizationPercentage),
                AvailableCapacity = resource.MaxHoursPerWeek - avgUtilization
            });
        }

        return report;
    }

    private decimal GetAverageUtilization(Guid resourceId, DateTime startDate, DateTime endDate)
    {
        var days = (endDate - startDate).Days;
        if (days <= 0) return 0;

        var totalUtilization = 0m;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            totalUtilization += GetCurrentUtilization(resourceId, date);
        }

        return totalUtilization / days;
    }

    private CapacityStatusType GetCapacityStatus(decimal utilizationPercentage)
    {
        if (utilizationPercentage >= 100) return CapacityStatusType.OverCapacity;
        if (utilizationPercentage >= 90) return CapacityStatusType.NearCapacity;
        if (utilizationPercentage >= 75) return CapacityStatusType.HighUtilization;
        if (utilizationPercentage >= 50) return CapacityStatusType.MediumUtilization;
        return CapacityStatusType.LowUtilization;
    }
}

/// <summary>
/// Represents a resource (team member, equipment, etc.)
/// </summary>
public class Resource
{
    public Guid ResourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public ResourceType Type { get; set; }
    public decimal MaxHoursPerWeek { get; set; }
    public decimal HourlyRate { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Skills { get; set; } = new();
}

/// <summary>
/// Resource allocation to a project
/// </summary>
public class ResourceAllocation
{
    public Guid AllocationId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid ProjectId { get; set; }
    public decimal HoursPerWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public AllocationStatus Status { get; set; }
}

public enum ResourceType
{
    Person,
    Equipment,
    Facility,
    Software
}

public enum AllocationStatus
{
    Planned,
    Active,
    Completed,
    Cancelled
}

public enum CapacityStatusType
{
    LowUtilization,
    MediumUtilization,
    HighUtilization,
    NearCapacity,
    OverCapacity
}

public class AllocationResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public ResourceAllocation? Allocation { get; set; }
    public decimal RemainingCapacity { get; set; }

    public static AllocationResult Failure(string message)
        => new() { IsSuccess = false, Message = message };
}

public class AvailabilityResult
{
    public bool IsAvailable { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<ResourceAllocation> ConflictingAllocations { get; set; } = new();
}

public class CapacityResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal CurrentUtilization { get; set; }
    public decimal AvailableCapacity { get; set; }
    public decimal NewUtilization { get; set; }
    public decimal UtilizationPercentage { get; set; }
    public bool IsOverCapacity { get; set; }

    public static CapacityResult Failure(string message)
        => new() { IsSuccess = false, Message = message };
}

public class ResourceCapacityReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<ResourceCapacityInfo> ResourceCapacities { get; set; } = new();
}

public class ResourceCapacityInfo
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public decimal MaxHoursPerWeek { get; set; }
    public decimal AverageUtilization { get; set; }
    public decimal UtilizationPercentage { get; set; }
    public CapacityStatusType CapacityStatus { get; set; }
    public decimal AvailableCapacity { get; set; }
}
