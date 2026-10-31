namespace QuickDock.Core.Modules;

/// <summary>
/// Represents the lifecycle states of a QuickDock module.
/// </summary>
public enum ModuleState
{
    Uninitialized,
    Initializing,
    Initialized,
    Starting,
    Running,
    Stopping,
    Stopped,
    Faulted
}

/// <summary>
/// Logical categorization for QuickDock modules.
/// </summary>
public enum ModuleCategory
{
    System,
    Productivity,
    Developer,
    Utilities,
    Media,
    AI,
    Other
}
