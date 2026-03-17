using GalacticFleetCommand.Api.Domain;
using GalacticFleetCommand.Api.Domain.Exceptions;
using GalacticFleetCommand.Api.Domain.Models;

namespace GalacticFleetCommand.Tests.Domain;

public class FleetStateMachineTests
{
    [Theory]
    [InlineData(FleetState.Docked, FleetState.Preparing)]
    [InlineData(FleetState.Preparing, FleetState.Ready)]
    [InlineData(FleetState.Preparing, FleetState.FailedPreparation)]
    [InlineData(FleetState.Ready, FleetState.Deployed)]
    [InlineData(FleetState.Deployed, FleetState.InBattle)]
    [InlineData(FleetState.InBattle, FleetState.Victorious)]
    [InlineData(FleetState.InBattle, FleetState.Destroyed)]
    [InlineData(FleetState.FailedPreparation, FleetState.Docked)]
    public void ValidTransitions_ShouldSucceed(FleetState from, FleetState to)
    {
        Assert.True(FleetStateMachine.CanTransition(from, to));
        FleetStateMachine.AssertTransition(from, to); // should not throw
    }

    [Theory]
    [InlineData(FleetState.Docked, FleetState.Ready)]
    [InlineData(FleetState.Docked, FleetState.Deployed)]
    [InlineData(FleetState.Docked, FleetState.Victorious)]
    [InlineData(FleetState.Preparing, FleetState.Deployed)]
    [InlineData(FleetState.Preparing, FleetState.Docked)]
    [InlineData(FleetState.Ready, FleetState.Docked)]
    [InlineData(FleetState.Ready, FleetState.Preparing)]
    [InlineData(FleetState.Deployed, FleetState.Docked)]
    [InlineData(FleetState.Deployed, FleetState.Ready)]
    [InlineData(FleetState.InBattle, FleetState.Docked)]
    [InlineData(FleetState.InBattle, FleetState.Deployed)]
    public void InvalidTransitions_ShouldThrow(FleetState from, FleetState to)
    {
        Assert.False(FleetStateMachine.CanTransition(from, to));
        Assert.Throws<InvalidTransitionException>(() => FleetStateMachine.AssertTransition(from, to));
    }

    [Theory]
    [InlineData(FleetState.Victorious)]
    [InlineData(FleetState.Destroyed)]
    public void TerminalStates_RejectAllTransitions(FleetState terminalState)
    {
        Assert.True(FleetStateMachine.IsTerminal(terminalState));

        foreach (FleetState target in Enum.GetValues<FleetState>())
        {
            Assert.False(FleetStateMachine.CanTransition(terminalState, target));
        }
    }

    [Fact]
    public void NonTerminalStates_AreNotTerminal()
    {
        Assert.False(FleetStateMachine.IsTerminal(FleetState.Docked));
        Assert.False(FleetStateMachine.IsTerminal(FleetState.Preparing));
        Assert.False(FleetStateMachine.IsTerminal(FleetState.Ready));
        Assert.False(FleetStateMachine.IsTerminal(FleetState.Deployed));
        Assert.False(FleetStateMachine.IsTerminal(FleetState.InBattle));
    }

    [Fact]
    public void OnlyDocked_IsEditable()
    {
        Assert.True(FleetStateMachine.IsEditable(FleetState.Docked));
        FleetStateMachine.AssertEditable(FleetState.Docked); // should not throw

        foreach (FleetState state in Enum.GetValues<FleetState>())
        {
            if (state == FleetState.Docked) continue;

            Assert.False(FleetStateMachine.IsEditable(state));
            Assert.Throws<InvalidTransitionException>(() => FleetStateMachine.AssertEditable(state));
        }
    }
}
