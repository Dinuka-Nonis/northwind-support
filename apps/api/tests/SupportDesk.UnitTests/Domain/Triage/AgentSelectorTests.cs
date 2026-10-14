using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class AgentSelectorTests
{
    private const int General = 1;
    private const int Billing = 2;

    private static AgentCandidate Candidate(
        int id,
        int open,
        int max = 5,
        bool active = true,
        int[]? categories = null) =>
        new(id, $"Agent {id}", active, max, open, categories ?? []);

    [Fact]
    public void Select_PicksTheAgentWithTheFewestOpenTickets()
    {
        var decision = AgentSelector.Select(
            [Candidate(1, open: 3), Candidate(2, open: 1), Candidate(3, open: 2)],
            General,
            requiresSpecialist: false);

        Assert.Equal(2, decision.AgentId);
        Assert.Contains("Agent 2", decision.Reason);
    }

    [Fact]
    public void Select_BreaksTiesWithTheLowestAgentId()
    {
        // Listed in reverse on purpose, so the result cannot depend on input order.
        var decision = AgentSelector.Select(
            [Candidate(7, open: 2), Candidate(3, open: 2), Candidate(5, open: 2)],
            General,
            requiresSpecialist: false);

        Assert.Equal(3, decision.AgentId);
    }

    [Fact]
    public void Select_DoesNotDependOnInputOrder()
    {
        AgentCandidate[] candidates = [Candidate(4, open: 1), Candidate(2, open: 1)];

        var first = AgentSelector.Select(candidates, General, requiresSpecialist: false);
        var second = AgentSelector.Select(candidates.OrderBy(a => a.Id), General, requiresSpecialist: false);

        Assert.Equal(first.AgentId, second.AgentId);
    }

    [Fact]
    public void Select_WhenASpecialistIsRequired_OnlyQualifiedAgentsAreChosen()
    {
        // Agent 1 is far less busy but is not a billing specialist.
        var decision = AgentSelector.Select(
            [Candidate(1, open: 0), Candidate(2, open: 4, categories: [Billing])],
            Billing,
            requiresSpecialist: true);

        Assert.Equal(2, decision.AgentId);
        Assert.Contains("specialist", decision.Reason);
    }

    [Fact]
    public void Select_WhenNoSpecialistIsRequired_SpecializationsAreIgnored()
    {
        var decision = AgentSelector.Select(
            [Candidate(1, open: 0), Candidate(2, open: 4, categories: [Billing])],
            Billing,
            requiresSpecialist: false);

        Assert.Equal(1, decision.AgentId);
    }

    [Fact]
    public void Select_SpecialisationInAnotherCategoryDoesNotQualify()
    {
        var decision = AgentSelector.Select(
            [Candidate(1, open: 0, categories: [General])],
            Billing,
            requiresSpecialist: true);

        Assert.Null(decision.AgentId);
    }

    [Fact]
    public void Select_AnAgentAtTheirLimitIsNotEligible()
    {
        var decision = AgentSelector.Select(
            [Candidate(1, open: 5, max: 5), Candidate(2, open: 4, max: 5)],
            General,
            requiresSpecialist: false);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Select_AnAgentOneBelowTheirLimitIsEligible()
    {
        var decision = AgentSelector.Select([Candidate(1, open: 4, max: 5)], General, requiresSpecialist: false);

        Assert.Equal(1, decision.AgentId);
    }

    [Fact]
    public void Select_AnAgentAboveTheirLimitIsNotEligible()
    {
        var decision = AgentSelector.Select([Candidate(1, open: 6, max: 5)], General, requiresSpecialist: false);

        Assert.Null(decision.AgentId);
    }

    [Fact]
    public void Select_InactiveAgentsAreNeverChosen()
    {
        var decision = AgentSelector.Select(
            [Candidate(1, open: 0, active: false), Candidate(2, open: 3)],
            General,
            requiresSpecialist: false);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Select_WhenNobodyIsEligible_LeavesTheTicketUnassignedWithAReason()
    {
        var decision = AgentSelector.Select(
            [Candidate(1, open: 5, max: 5), Candidate(2, open: 0, active: false)],
            General,
            requiresSpecialist: false);

        Assert.Null(decision.Agent);
        Assert.Null(decision.AgentId);
        Assert.StartsWith("Left unassigned", decision.Reason);
    }

    [Fact]
    public void Select_WhenNoSpecialistIsEligible_TheReasonSaysSo()
    {
        var decision = AgentSelector.Select(
            [Candidate(1, open: 5, max: 5, categories: [Billing])],
            Billing,
            requiresSpecialist: true);

        Assert.Null(decision.AgentId);
        Assert.Contains("specialist", decision.Reason);
    }

    [Fact]
    public void Select_WithNoAgentsAtAll_LeavesTheTicketUnassigned()
    {
        var decision = AgentSelector.Select([], General, requiresSpecialist: false);

        Assert.Null(decision.AgentId);
    }

    [Fact]
    public void SelectForEscalation_KeepsTheCurrentAgentWhileTheyAreActiveAndQualified()
    {
        var decision = AgentSelector.SelectForEscalation(
            currentAgentId: 2,
            [Candidate(1, open: 0, categories: [Billing]), Candidate(2, open: 3, categories: [Billing])],
            Billing,
            requiresSpecialist: true);

        Assert.Equal(2, decision.AgentId);
        Assert.StartsWith("Kept", decision.Reason);
    }

    [Fact]
    public void SelectForEscalation_KeepsTheCurrentAgentEvenWhenTheyAreAtTheirLimit()
    {
        // The ticket already counts towards their load, so being full must not cost them it.
        var decision = AgentSelector.SelectForEscalation(
            currentAgentId: 2,
            [Candidate(1, open: 0), Candidate(2, open: 5, max: 5)],
            General,
            requiresSpecialist: false);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void SelectForEscalation_ReassignsWhenTheCurrentAgentIsInactive()
    {
        var decision = AgentSelector.SelectForEscalation(
            currentAgentId: 1,
            [Candidate(1, open: 0, active: false), Candidate(2, open: 3), Candidate(3, open: 1)],
            General,
            requiresSpecialist: false);

        Assert.Equal(3, decision.AgentId);
        Assert.Contains("previous agent", decision.Reason);
    }

    [Fact]
    public void SelectForEscalation_ReassignsWhenTheCurrentAgentLacksTheRequiredSpecialisation()
    {
        var decision = AgentSelector.SelectForEscalation(
            currentAgentId: 1,
            [Candidate(1, open: 0), Candidate(2, open: 4, categories: [Billing])],
            Billing,
            requiresSpecialist: true);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void SelectForEscalation_KeepsAnUnqualifiedAgentWhenNoSpecialistIsRequired()
    {
        var decision = AgentSelector.SelectForEscalation(
            currentAgentId: 1,
            [Candidate(1, open: 2), Candidate(2, open: 0, categories: [Billing])],
            Billing,
            requiresSpecialist: false);

        Assert.Equal(1, decision.AgentId);
    }

    [Fact]
    public void SelectForEscalation_AssignsAnUnassignedTicketAsOnCreation()
    {
        var decision = AgentSelector.SelectForEscalation(
            currentAgentId: null,
            [Candidate(1, open: 4), Candidate(2, open: 1)],
            General,
            requiresSpecialist: false);

        Assert.Equal(2, decision.AgentId);
        Assert.DoesNotContain("previous agent", decision.Reason);
    }

    [Fact]
    public void SelectForEscalation_WhenTheCurrentAgentIsNotInTheListItReselects()
    {
        var decision = AgentSelector.SelectForEscalation(
            currentAgentId: 99,
            [Candidate(1, open: 2)],
            General,
            requiresSpecialist: false);

        Assert.Equal(1, decision.AgentId);
    }

    [Fact]
    public void SelectForEscalation_WhenNobodyIsEligible_ReturnsUnassigned()
    {
        var decision = AgentSelector.SelectForEscalation(
            currentAgentId: 1,
            [Candidate(1, open: 0, active: false), Candidate(2, open: 5, max: 5)],
            General,
            requiresSpecialist: false);

        Assert.Null(decision.AgentId);
        Assert.Contains("Left unassigned", decision.Reason);
    }
}
