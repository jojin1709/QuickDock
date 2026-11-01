namespace QuickDock.Core.StateMachine;

public enum NotchState
{
    Collapsed,
    HoverPending,
    Expanding,
    Expanded,
    CollapsePending,
    Collapsing
}

public enum NotchZone
{
    Notch,
    Dashboard
}

public sealed class NotchStateMachine
{
    private readonly object _lock = new();
    private bool _pointerInNotch;
    private bool _pointerInDashboard;

    public NotchState CurrentState { get; private set; } = NotchState.Collapsed;

    public int HoverDelayMs { get; set; } = 150;
    public int LeaveDelayMs { get; set; } = 200;

    public Action? OnTriggerExpand { get; set; }
    public Action? OnTriggerCollapse { get; set; }
    public Action? OnStartHoverTimer { get; set; }
    public Action? OnCancelHoverTimer { get; set; }
    public Action? OnStartLeaveTimer { get; set; }
    public Action? OnCancelLeaveTimer { get; set; }

    public bool IsPointerInInteractionZone => _pointerInNotch || _pointerInDashboard;

    public void ForceSetState(NotchState state)
    {
        lock (_lock)
        {
            CurrentState = state;
        }
    }

    public void PointerEntered(NotchZone zone)
    {
        lock (_lock)
        {
            if (zone == NotchZone.Notch) _pointerInNotch = true;
            if (zone == NotchZone.Dashboard) _pointerInDashboard = true;

            switch (CurrentState)
            {
                case NotchState.Collapsed:
                    CurrentState = NotchState.HoverPending;
                    OnStartHoverTimer?.Invoke();
                    break;

                case NotchState.CollapsePending:
                    // Returned during close delay: cancel collapse and remain expanded
                    CurrentState = NotchState.Expanded;
                    OnCancelLeaveTimer?.Invoke();
                    break;

                case NotchState.Collapsing:
                    // Re-entered during collapse animation: redirect smoothly to expand
                    CurrentState = NotchState.Expanding;
                    OnTriggerExpand?.Invoke();
                    break;

                case NotchState.HoverPending:
                case NotchState.Expanding:
                case NotchState.Expanded:
                    // Already in or moving toward expanded state
                    break;
            }
        }
    }

    public void PointerLeft(NotchZone zone)
    {
        lock (_lock)
        {
            if (zone == NotchZone.Notch) _pointerInNotch = false;
            if (zone == NotchZone.Dashboard) _pointerInDashboard = false;

            // Only trigger leave if pointer is outside ALL interaction zones
            if (IsPointerInInteractionZone) return;

            switch (CurrentState)
            {
                case NotchState.HoverPending:
                    // Left during hover delay: cancel expand and remain collapsed
                    CurrentState = NotchState.Collapsed;
                    OnCancelHoverTimer?.Invoke();
                    break;

                case NotchState.Expanded:
                    // Left interaction region: begin debounce close delay
                    CurrentState = NotchState.CollapsePending;
                    OnStartLeaveTimer?.Invoke();
                    break;

                case NotchState.Expanding:
                    // Left while expanding: redirect smoothly to collapse
                    CurrentState = NotchState.Collapsing;
                    OnTriggerCollapse?.Invoke();
                    break;

                case NotchState.Collapsed:
                case NotchState.CollapsePending:
                case NotchState.Collapsing:
                    break;
            }
        }
    }

    public void HoverDelayElapsed()
    {
        lock (_lock)
        {
            if (CurrentState == NotchState.HoverPending)
            {
                CurrentState = NotchState.Expanding;
                OnTriggerExpand?.Invoke();
            }
        }
    }

    public void LeaveDelayElapsed()
    {
        lock (_lock)
        {
            if (CurrentState == NotchState.CollapsePending)
            {
                CurrentState = NotchState.Collapsing;
                OnTriggerCollapse?.Invoke();
            }
        }
    }

    public void AnimationCompleted()
    {
        lock (_lock)
        {
            if (CurrentState == NotchState.Expanding)
            {
                CurrentState = NotchState.Expanded;
                // If pointer left while expanding was in-flight, initiate collapse pending
                if (!IsPointerInInteractionZone)
                {
                    CurrentState = NotchState.CollapsePending;
                    OnStartLeaveTimer?.Invoke();
                }
            }
            else if (CurrentState == NotchState.Collapsing)
            {
                CurrentState = NotchState.Collapsed;
                // If pointer re-entered while collapsing was in-flight, initiate hover pending
                if (IsPointerInInteractionZone)
                {
                    CurrentState = NotchState.HoverPending;
                    OnStartHoverTimer?.Invoke();
                }
            }
        }
    }

    public void RequestOpen()
    {
        lock (_lock)
        {
            OnCancelHoverTimer?.Invoke();
            OnCancelLeaveTimer?.Invoke();
            CurrentState = NotchState.Expanding;
            OnTriggerExpand?.Invoke();
        }
    }

    public void RequestClose()
    {
        lock (_lock)
        {
            OnCancelHoverTimer?.Invoke();
            OnCancelLeaveTimer?.Invoke();
            CurrentState = NotchState.Collapsing;
            OnTriggerCollapse?.Invoke();
        }
    }

    public void ForceState(NotchState state)
    {
        lock (_lock)
        {
            CurrentState = state;
            if (state == NotchState.Collapsed)
            {
                _pointerInNotch = false;
                _pointerInDashboard = false;
            }
        }
    }
}
