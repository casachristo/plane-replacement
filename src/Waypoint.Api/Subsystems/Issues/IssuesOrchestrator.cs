using Waypoint.Api.Auth;
using Waypoint.Api.Endpoints;
using Waypoint.Api.Repositories;
using Waypoint.Api.Subsystems.Issues.IssueCrud;
using Waypoint.Api.Webhooks;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Waypoint.Domain.Validation;

namespace Waypoint.Api.Subsystems.Issues;

// Orchestrator — coordinates the Issues subsystem's child features. The transition gate is the
// canonical case: it sequences issue-state (IssueManager), the acceptance-criteria gate,
// workflow validation, and the resulting activity + webhooks. Holds no state of its own.
public interface IIssuesOrchestrator
{
    Task<IssueDto> TransitionAsync(string slug, int seq, TransitionIssueRequest req, Principal? actor, CancellationToken ct);
}

public sealed class IssuesOrchestrator(IIssueManager manager, IProjectRepository projects, IWebhookPublisher publisher) : IIssuesOrchestrator
{
    public async Task<IssueDto> TransitionAsync(string slug, int seq, TransitionIssueRequest req, Principal? actor, CancellationToken ct)
    {
        if (req.Force && string.IsNullOrWhiteSpace(req.BypassReason))
            throw new ValidationException("bypass_reason_required", "force=true requires a non-empty BypassReason.");

        var project = await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
        var issue = await manager.GetTrackedAsync(project.Id, seq, ct)
            ?? throw new NotFoundException("issue_not_found", "Issue not found.");
        var newState = await manager.GetStateAsync(req.ToStateId, ct)
            ?? throw new NotFoundException("state_not_found", "Target state not found.");
        if (newState.ProjectId != project.Id)
            throw new ValidationException("state_wrong_project", "State does not belong to this project.");

        if (issue.StateId == req.ToStateId) return IssueMapper.ToDto(issue);

        var workflowId = issue.IssueType.DefaultWorkflowId
            ?? throw new ConflictException("issue_type_has_no_workflow", "Issue type has no default workflow.");
        var transitions = await manager.GetWorkflowTransitionsAsync(workflowId, ct);
        var validator = new WorkflowTransitionValidator(transitions.Select(t => (t.From, t.To)));
        if (!validator.CanTransition(issue.StateId, req.ToStateId))
            throw new ConflictException("transition_not_allowed",
                $"Transition from state '{issue.State.Name}' to '{newState.Name}' is not allowed by the workflow.");

        const string gateName = "acceptance_criteria_unchecked";
        GateOverrideEvent? overrideEvent = null;
        if (newState.Group == StateGroup.Completed)
        {
            var unchecked_ = await manager.GetUncheckedCriteriaAsync(issue.Id, ct);
            if (unchecked_.Count > 0)
            {
                if (!req.Force)
                    throw new PreconditionFailedException(gateName,
                        $"Cannot transition to '{newState.Name}' — {unchecked_.Count} acceptance criterion(s) are still unchecked.",
                        new Dictionary<string, object> { ["unchecked"] = unchecked_ });
                var (atype, aid, alabel) = ResolveActor(actor);
                overrideEvent = new GateOverrideEvent
                {
                    IssueId = issue.Id, GateName = gateName, Reason = req.BypassReason ?? string.Empty,
                    ActorType = atype, ActorId = aid, ActorLabel = alabel,
                };
            }
        }

        var previousState = issue.State;
        var beforeStateId = issue.StateId;
        await manager.PersistTransitionAsync(issue, beforeStateId, req.ToStateId, overrideEvent, ct);

        await publisher.PublishAsync(WebhookEvent.IssueTransitioned, project.Id,
            WebhookPayloads.IssueTransitioned(issue, previousState, newState), ct);
        if (overrideEvent is not null)
            await publisher.PublishAsync(WebhookEvent.GateOverrideFired, project.Id,
                WebhookPayloads.GateOverride(overrideEvent, WebhookPayloads.From(issue)), ct);

        await manager.SaveAsync(ct);
        return IssueMapper.ToDto((await manager.GetBySequenceAsync(project.Id, seq, ct))!);
    }

    private static (ActorType type, Guid? id, string? label) ResolveActor(Principal? p)
    {
        if (p is null) return (ActorType.System, null, null);
        if (p.PassthroughActorId is not null)
        {
            Guid? id = Guid.TryParse(p.PassthroughActorId, out var g) ? g : null;
            return (ActorType.Passthrough, id, p.PassthroughActorLabel);
        }
        var t = p.Kind == PrincipalKind.Human ? ActorType.User : ActorType.Service;
        return (t, Guid.TryParse(p.Id, out var pid) ? pid : null, p.DisplayName);
    }
}
