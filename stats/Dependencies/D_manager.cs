namespace Gradual.BusinessLogic.Dependencies;

/// <summary>
/// Manages project dependencies and relationships
/// </summary>
public class D_manager
{
    private readonly List<ProjectDependency> _dependencies = new();
    private readonly List<ProjectRelationship> _relationships = new();

    /// <summary>
    /// Adds a dependency between projects
    /// </summary>
    public DependencyResult AddDependency(
        Guid dependentProjectId,
        Guid prerequisiteProjectId,
        DependencyType type,
        string description = "")
    {
        // Prevent self-dependency
        if (dependentProjectId == prerequisiteProjectId)
        {
            return DependencyResult.Failure("A project cannot depend on itself");
        }

        // Check for existing dependency
        var existing = _dependencies.FirstOrDefault(d =>
            d.DependentProjectId == dependentProjectId &&
            d.PrerequisiteProjectId == prerequisiteProjectId);

        if (existing != null)
        {
            return DependencyResult.Failure("Dependency already exists");
        }

        // Check for circular dependencies
        if (WouldCreateCircularDependency(dependentProjectId, prerequisiteProjectId))
        {
            return DependencyResult.Failure("This would create a circular dependency");
        }

        var dependency = new ProjectDependency
        {
            DependencyId = Guid.NewGuid(),
            DependentProjectId = dependentProjectId,
            PrerequisiteProjectId = prerequisiteProjectId,
            Type = type,
            Description = description,
            Status = DependencyStatus.Active,
            CreatedDate = DateTime.Now
        };

        _dependencies.Add(dependency);

        return new DependencyResult
        {
            IsSuccess = true,
            Message = "Dependency added successfully",
            Dependency = dependency
        };
    }

    /// <summary>
    /// Checks if adding a dependency would create a circular reference
    /// </summary>
    private bool WouldCreateCircularDependency(Guid fromProject, Guid toProject)
    {
        var visited = new HashSet<Guid>();
        return HasPath(toProject, fromProject, visited);
    }

    /// <summary>
    /// Checks if there's a dependency path from source to target
    /// </summary>
    private bool HasPath(Guid sourceProject, Guid targetProject, HashSet<Guid> visited)
    {
        if (sourceProject == targetProject)
            return true;

        if (visited.Contains(sourceProject))
            return false;

        visited.Add(sourceProject);

        var dependencies = _dependencies
            .Where(d => d.DependentProjectId == sourceProject && d.Status == DependencyStatus.Active)
            .Select(d => d.PrerequisiteProjectId);

        foreach (var prerequisite in dependencies)
        {
            if (HasPath(prerequisite, targetProject, visited))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all dependencies for a project
    /// </summary>
    public List<ProjectDependency> GetDependencies(Guid projectId)
    {
        return _dependencies
            .Where(d => d.DependentProjectId == projectId && d.Status == DependencyStatus.Active)
            .ToList();
    }

    /// <summary>
    /// Gets all projects that depend on this project
    /// </summary>
    public List<ProjectDependency> GetDependents(Guid projectId)
    {
        return _dependencies
            .Where(d => d.PrerequisiteProjectId == projectId && d.Status == DependencyStatus.Active)
            .ToList();
    }

    /// <summary>
    /// Checks if a project can start based on dependencies
    /// </summary>
    public DependencyCheckResult CanProjectStart(Guid projectId, Dictionary<Guid, string> projectStatuses)
    {
        var dependencies = GetDependencies(projectId);
        var result = new DependencyCheckResult { ProjectId = projectId };

        if (!dependencies.Any())
        {
            result.CanStart = true;
            result.Message = "No dependencies - can start";
            return result;
        }

        foreach (var dependency in dependencies)
        {
            var prerequisiteId = dependency.PrerequisiteProjectId;
            
            if (!projectStatuses.ContainsKey(prerequisiteId))
            {
                result.BlockingDependencies.Add(dependency);
                result.Reasons.Add($"Prerequisite project {prerequisiteId} not found");
                continue;
            }

            var status = projectStatuses[prerequisiteId];
            
            if (dependency.Type == DependencyType.FinishToStart && status != "Completed")
            {
                result.BlockingDependencies.Add(dependency);
                result.Reasons.Add($"Prerequisite project must be completed (current: {status})");
            }
            else if (dependency.Type == DependencyType.StartToStart && status == "Not Started")
            {
                result.BlockingDependencies.Add(dependency);
                result.Reasons.Add($"Prerequisite project must be started first");
            }
        }

        result.CanStart = !result.BlockingDependencies.Any();
        result.Message = result.CanStart
            ? "All dependencies satisfied"
            : $"{result.BlockingDependencies.Count} blocking dependencies";

        return result;
    }

    /// <summary>
    /// Gets the critical path through project dependencies
    /// </summary>
    public List<Guid> GetCriticalPath(Guid startProjectId, Dictionary<Guid, int> projectDurations)
    {
        var criticalPath = new List<Guid>();
        var longestPath = 0;

        void FindLongestPath(Guid currentProject, List<Guid> currentPath, int currentDuration)
        {
            currentPath.Add(currentProject);
            
            var duration = projectDurations.ContainsKey(currentProject)
                ? projectDurations[currentProject]
                : 0;
            
            currentDuration += duration;

            var dependents = GetDependents(currentProject);
            
            if (!dependents.Any())
            {
                if (currentDuration > longestPath)
                {
                    longestPath = currentDuration;
                    criticalPath = new List<Guid>(currentPath);
                }
            }
            else
            {
                foreach (var dependent in dependents)
                {
                    FindLongestPath(dependent.DependentProjectId, new List<Guid>(currentPath), currentDuration);
                }
            }
        }

        FindLongestPath(startProjectId, new List<Guid>(), 0);
        return criticalPath;
    }

    /// <summary>
    /// Adds a relationship between projects
    /// </summary>
    public void AddRelationship(Guid project1Id, Guid project2Id, RelationshipType type, string description = "")
    {
        var relationship = new ProjectRelationship
        {
            RelationshipId = Guid.NewGuid(),
            Project1Id = project1Id,
            Project2Id = project2Id,
            Type = type,
            Description = description,
            CreatedDate = DateTime.Now
        };

        _relationships.Add(relationship);
    }

    /// <summary>
    /// Gets related projects
    /// </summary>
    public List<ProjectRelationship> GetRelatedProjects(Guid projectId)
    {
        return _relationships
            .Where(r => r.Project1Id == projectId || r.Project2Id == projectId)
            .ToList();
    }

    /// <summary>
    /// Removes a dependency
    /// </summary>
    public bool RemoveDependency(Guid dependencyId)
    {
        var dependency = _dependencies.FirstOrDefault(d => d.DependencyId == dependencyId);
        if (dependency == null)
            return false;

        dependency.Status = DependencyStatus.Removed;
        dependency.RemovedDate = DateTime.Now;
        return true;
    }

    /// <summary>
    /// Gets dependency tree visualization data
    /// </summary>
    public DependencyTree GetDependencyTree(Guid rootProjectId)
    {
        var tree = new DependencyTree { RootProjectId = rootProjectId };
        BuildDependencyTree(rootProjectId, tree, new HashSet<Guid>(), 0);
        return tree;
    }

    private void BuildDependencyTree(Guid projectId, DependencyTree tree, HashSet<Guid> visited, int level)
    {
        if (visited.Contains(projectId))
            return;

        visited.Add(projectId);
        tree.Nodes.Add(new DependencyNode
        {
            ProjectId = projectId,
            Level = level
        });

        var dependencies = GetDependencies(projectId);
        foreach (var dep in dependencies)
        {
            tree.Edges.Add(new DependencyEdge
            {
                FromProject = projectId,
                ToProject = dep.PrerequisiteProjectId,
                Type = dep.Type
            });
            BuildDependencyTree(dep.PrerequisiteProjectId, tree, visited, level + 1);
        }
    }
}

/// <summary>
/// Project dependency
/// </summary>
public class ProjectDependency
{
    public Guid DependencyId { get; set; }
    public Guid DependentProjectId { get; set; }
    public Guid PrerequisiteProjectId { get; set; }
    public DependencyType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DependencyStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? RemovedDate { get; set; }
}

/// <summary>
/// Project relationship (non-dependency)
/// </summary>
public class ProjectRelationship
{
    public Guid RelationshipId { get; set; }
    public Guid Project1Id { get; set; }
    public Guid Project2Id { get; set; }
    public RelationshipType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

public enum DependencyType
{
    FinishToStart,      // Prerequisite must finish before dependent can start
    StartToStart,       // Both can start at the same time
    FinishToFinish,     // Both must finish at the same time
    StartToFinish       // Dependent can't finish until prerequisite starts
}

public enum DependencyStatus
{
    Active,
    Satisfied,
    Removed,
    Blocked
}

public enum RelationshipType
{
    Related,
    ParentChild,
    SharedResources,
    SameClient,
    Competing
}

public class DependencyResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public ProjectDependency? Dependency { get; set; }

    public static DependencyResult Failure(string message)
        => new() { IsSuccess = false, Message = message };
}

public class DependencyCheckResult
{
    public Guid ProjectId { get; set; }
    public bool CanStart { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ProjectDependency> BlockingDependencies { get; set; } = new();
    public List<string> Reasons { get; set; } = new();
}

public class DependencyTree
{
    public Guid RootProjectId { get; set; }
    public List<DependencyNode> Nodes { get; set; } = new();
    public List<DependencyEdge> Edges { get; set; } = new();
}

public class DependencyNode
{
    public Guid ProjectId { get; set; }
    public int Level { get; set; }
}

public class DependencyEdge
{
    public Guid FromProject { get; set; }
    public Guid ToProject { get; set; }
    public DependencyType Type { get; set; }
}
