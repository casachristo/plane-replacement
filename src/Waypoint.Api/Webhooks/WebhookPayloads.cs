using Waypoint.Domain.Entities;

namespace Waypoint.Api.Webhooks;

/// <summary>
/// WAY-6: every state-bearing payload includes state id, name AND group together,
/// so subscribers never round-trip back to Waypoint just to render a transition.
/// </summary>
public sealed record StateRef(Guid Id, string Name, string Group);

public sealed record IssueRef(Guid Id, int Sequence, string Title);

public static class WebhookPayloads
{
    public static StateRef From(State s) => new(s.Id, s.Name, s.Group.ToString());
    public static IssueRef From(Issue i) => new(i.Id, i.SequenceId, i.Title);

    public static object IssueTransitioned(Issue issue, State previous, State next) => new
    {
        issue = From(issue),
        previous_state = From(previous),
        new_state = From(next),
    };

    public static object IssueCreated(Issue issue, State state) => new
    {
        issue = From(issue),
        state = From(state),
    };

    public static object AcceptanceCriterion(AcceptanceCriterion ac, IssueRef issue) => new
    {
        issue,
        acceptance_criterion = new
        {
            id = ac.Id,
            position = ac.Position,
            text = ac.Text,
            @checked = ac.Checked,
            checked_at = ac.CheckedAt,
            checked_by_actor_type = ac.CheckedByActorType?.ToString(),
            checked_by_actor_label = ac.CheckedByActorLabel,
        },
    };

    public static object GateOverride(GateOverrideEvent g, IssueRef issue) => new
    {
        issue,
        gate_name = g.GateName,
        reason = g.Reason,
        actor_type = g.ActorType.ToString(),
        actor_id = g.ActorId,
        actor_label = g.ActorLabel,
        at = g.At,
    };
}
