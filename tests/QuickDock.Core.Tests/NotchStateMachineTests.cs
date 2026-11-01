using QuickDock.Core.StateMachine;
using Xunit;

namespace QuickDock.Core.Tests;

public class NotchStateMachineTests
{
    [Fact]
    public void InitialState_IsCollapsed()
    {
        var sm = new NotchStateMachine();
        Assert.Equal(NotchState.Collapsed, sm.CurrentState);
    }

    [Fact]
    public void PointerEnteredNotch_TransitionsToHoverPending_AndStartsTimer()
    {
        var sm = new NotchStateMachine();
        bool timerStarted = false;
        sm.OnStartHoverTimer = () => timerStarted = true;

        sm.PointerEntered(NotchZone.Notch);

        Assert.Equal(NotchState.HoverPending, sm.CurrentState);
        Assert.True(timerStarted);
    }

    [Fact]
    public void HoverDelayElapsed_TransitionsToExpanding_AndTriggersExpand()
    {
        var sm = new NotchStateMachine();
        bool expandTriggered = false;
        sm.OnTriggerExpand = () => expandTriggered = true;

        sm.PointerEntered(NotchZone.Notch);
        sm.HoverDelayElapsed();

        Assert.Equal(NotchState.Expanding, sm.CurrentState);
        Assert.True(expandTriggered);
    }

    [Fact]
    public void AnimationCompleted_WhenExpanding_TransitionsToExpanded()
    {
        var sm = new NotchStateMachine();
        sm.PointerEntered(NotchZone.Notch);
        sm.HoverDelayElapsed();
        sm.AnimationCompleted();

        Assert.Equal(NotchState.Expanded, sm.CurrentState);
    }

    [Fact]
    public void HoverPending_PointerLeft_CancelsTimer_AndReturnsToCollapsed()
    {
        var sm = new NotchStateMachine();
        bool timerCancelled = false;
        sm.OnCancelHoverTimer = () => timerCancelled = true;

        sm.PointerEntered(NotchZone.Notch);
        sm.PointerLeft(NotchZone.Notch);

        Assert.Equal(NotchState.Collapsed, sm.CurrentState);
        Assert.True(timerCancelled);
    }

    [Fact]
    public void Expanded_PointerLeft_TransitionsToCollapsePending_AndStartsLeaveTimer()
    {
        var sm = new NotchStateMachine();
        bool leaveTimerStarted = false;
        sm.OnStartLeaveTimer = () => leaveTimerStarted = true;

        sm.PointerEntered(NotchZone.Notch);
        sm.HoverDelayElapsed();
        sm.AnimationCompleted();

        // Pointer leaves notch (and dashboard)
        sm.PointerLeft(NotchZone.Notch);

        Assert.Equal(NotchState.CollapsePending, sm.CurrentState);
        Assert.True(leaveTimerStarted);
    }

    [Fact]
    public void CollapsePending_PointerEntered_CancelsLeaveTimer_AndReturnsToExpanded()
    {
        var sm = new NotchStateMachine();
        bool leaveTimerCancelled = false;
        sm.OnCancelLeaveTimer = () => leaveTimerCancelled = true;

        sm.PointerEntered(NotchZone.Notch);
        sm.HoverDelayElapsed();
        sm.AnimationCompleted();
        sm.PointerLeft(NotchZone.Notch);

        // Pointer re-enters dashboard before timer expires
        sm.PointerEntered(NotchZone.Dashboard);

        Assert.Equal(NotchState.Expanded, sm.CurrentState);
        Assert.True(leaveTimerCancelled);
    }

    [Fact]
    public void CollapsePending_LeaveDelayElapsed_TransitionsToCollapsing_AndTriggersCollapse()
    {
        var sm = new NotchStateMachine();
        bool collapseTriggered = false;
        sm.OnTriggerCollapse = () => collapseTriggered = true;

        sm.PointerEntered(NotchZone.Notch);
        sm.HoverDelayElapsed();
        sm.AnimationCompleted();
        sm.PointerLeft(NotchZone.Notch);

        sm.LeaveDelayElapsed();

        Assert.Equal(NotchState.Collapsing, sm.CurrentState);
        Assert.True(collapseTriggered);
    }

    [Fact]
    public void AnimationCompleted_WhenCollapsing_TransitionsToCollapsed()
    {
        var sm = new NotchStateMachine();
        sm.PointerEntered(NotchZone.Notch);
        sm.HoverDelayElapsed();
        sm.AnimationCompleted();
        sm.PointerLeft(NotchZone.Notch);
        sm.LeaveDelayElapsed();

        sm.AnimationCompleted();

        Assert.Equal(NotchState.Collapsed, sm.CurrentState);
    }

    [Fact]
    public void MovingBetweenNotchAndDashboard_DoesNotTriggerCollapse()
    {
        var sm = new NotchStateMachine();
        sm.PointerEntered(NotchZone.Notch);
        sm.HoverDelayElapsed();
        sm.AnimationCompleted();

        // User moves pointer into dashboard
        sm.PointerEntered(NotchZone.Dashboard);
        // Pointer leaves notch element
        sm.PointerLeft(NotchZone.Notch);

        // Still in interaction zone, must remain Expanded
        Assert.Equal(NotchState.Expanded, sm.CurrentState);
        Assert.True(sm.IsPointerInInteractionZone);
    }

    [Fact]
    public void UserLeavesWhileExpanding_RedirectsToCollapsing()
    {
        var sm = new NotchStateMachine();
        bool collapseTriggered = false;
        sm.OnTriggerCollapse = () => collapseTriggered = true;

        sm.PointerEntered(NotchZone.Notch);
        sm.HoverDelayElapsed(); // Now Expanding

        sm.PointerLeft(NotchZone.Notch); // Leaves while still expanding

        Assert.Equal(NotchState.Collapsing, sm.CurrentState);
        Assert.True(collapseTriggered);
    }

    [Fact]
    public void UserReEntersWhileCollapsing_RedirectsToExpanding()
    {
        var sm = new NotchStateMachine();
        bool expandTriggered = false;
        sm.OnTriggerExpand = () => expandTriggered = true;

        sm.RequestClose(); // Now Collapsing
        sm.PointerEntered(NotchZone.Notch); // Re-enters while collapsing

        Assert.Equal(NotchState.Expanding, sm.CurrentState);
        Assert.True(expandTriggered);
    }

    [Fact]
    public void RequestOpen_ImmediatelyExpands()
    {
        var sm = new NotchStateMachine();
        bool expandTriggered = false;
        sm.OnTriggerExpand = () => expandTriggered = true;

        sm.RequestOpen();

        Assert.Equal(NotchState.Expanding, sm.CurrentState);
        Assert.True(expandTriggered);
    }

    [Fact]
    public void RequestClose_ImmediatelyCollapses()
    {
        var sm = new NotchStateMachine();
        sm.RequestOpen();
        sm.AnimationCompleted();

        bool collapseTriggered = false;
        sm.OnTriggerCollapse = () => collapseTriggered = true;

        sm.RequestClose();

        Assert.Equal(NotchState.Collapsing, sm.CurrentState);
        Assert.True(collapseTriggered);
    }
}

