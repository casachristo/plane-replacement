using Waypoint.Api.Endpoints;
using Waypoint.Api.Pagination;
using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Api.Webhooks;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Subsystems.Issues.IssueCrud;

// Service — stateless interface to the issue-crud feature. Resolves the project, validates
// input, delegates state changes to the IssueManager, emits this feature's webhooks, and maps
// to wire DTOs. Holds no state. (Project resolution goes through the Projects subsystem's
// IProjectService facade — WAY-42.)
public interface IIssueService
{
    Task<IssueDto> CreateAsync(string slug, CreateIssueRequest req, CancellationToken ct);
    Task<IssueDto> UpdateAsync(string slug, int seq, UpdateIssueRequest req, CancellationToken ct);
    Task<PagedResponse<IssueDto>> ListAsync(string slug, int? limit, string? cursor, string? category, CancellationToken ct);
    Task<Issue> ResolveAsync(string slug, int seq, CancellationToken ct);
}

public sealed class IssueService(IIssueManager manager, IProjectService projects, IWebhookPublisher publisher) : IIssueService
{
    // WAY-28: single source of truth for list-page bounds (endpoint + repo can't drift).
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    private async Task<Project> ProjectAsync(string slug, CancellationToken ct) =>
        await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");

    public async Task<IssueDto> CreateAsync(string slug, CreateIssueRequest req, CancellationToken ct)
    {
        var project = await ProjectAsync(slug, ct);
        var category = TicketCategory.Feature;
        if (!string.IsNullOrWhiteSpace(req.Category) && !TicketCategories.TryParse(req.Category, out category))
            throw new ValidationException("invalid_category", $"Unknown ticket category '{req.Category}'.");

        if (project.DefaultStateId is null)
            throw new ConflictException("project_has_no_default_state", "Project has no default state.");
        var typeId = req.IssueTypeId ?? await manager.ResolveDefaultIssueTypeAsync(project.Id, ct)
            ?? throw new ConflictException("project_has_no_default_issue_type", "Project has no default issue type.");
        if (req.EpicId is { } eid && !await manager.EpicExistsAsync(project.Id, eid, ct))
            throw new NotFoundException("epic_not_found", "Epic not found in this project.");
        if (req.CycleId is { } cid && !await manager.CycleExistsAsync(project.Id, cid, ct))
            throw new NotFoundException("cycle_not_found", "Cycle not found in this project.");

        var issue = new Issue
        {
            ProjectId = project.Id,
            SequenceId = await manager.NextSequenceAsync(project.Id, ct),
            Title = req.Title,
            DescriptionMd = req.DescriptionMd,
            StateId = project.DefaultStateId.Value,
            IssueTypeId = typeId,
            EpicId = req.EpicId,
            CycleId = req.CycleId,
            Category = category,
        };
        await manager.PersistNewAsync(issue, ct);

        var state = await manager.GetStateAsync(issue.StateId, ct)!;
        await publisher.PublishAsync(WebhookEvent.IssueCreated, project.Id,
            WebhookPayloads.IssueCreated(issue, state!), ct);
        await manager.SaveAsync(ct);

        return IssueMapper.ToDto((await manager.GetBySequenceAsync(project.Id, issue.SequenceId, ct))!);
    }

    public async Task<IssueDto> UpdateAsync(string slug, int seq, UpdateIssueRequest req, CancellationToken ct)
    {
        var project = await ProjectAsync(slug, ct);
        var issue = await manager.GetTrackedAsync(project.Id, seq, ct)
            ?? throw new NotFoundException("issue_not_found", "Issue not found.");
        if (req.Title is not null) issue.Title = req.Title;
        if (req.DescriptionMd is not null) issue.DescriptionMd = req.DescriptionMd;
        if (req.Priority is not null) issue.Priority = (Priority)req.Priority.Value;
        await manager.PersistFieldUpdateAsync(issue, ct);

        await publisher.PublishAsync(WebhookEvent.IssueUpdated, project.Id,
            new { issue = WebhookPayloads.From(issue), state = WebhookPayloads.From(issue.State) }, ct);
        await manager.SaveAsync(ct);

        return IssueMapper.ToDto((await manager.GetBySequenceAsync(project.Id, seq, ct))!);
    }

    public async Task<PagedResponse<IssueDto>> ListAsync(string slug, int? limit, string? cursor, string? category, CancellationToken ct)
    {
        var project = await ProjectAsync(slug, ct);
        TicketCategory? categoryFilter = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!TicketCategories.TryParse(category, out var cf))
                throw new ValidationException("invalid_category", $"Unknown ticket category '{category}'.");
            categoryFilter = cf;
        }
        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var (items, total) = await manager.ListAsync(project.Id, pageSize, cursor, categoryFilter, ct);
        string? nextCursor = items.Count == pageSize ? Cursor.Encode(items[^1].CreatedAt, items[^1].Id) : null;
        return new PagedResponse<IssueDto>(items.Select(IssueMapper.ToDto).ToList(), nextCursor, total);
    }

    public async Task<Issue> ResolveAsync(string slug, int seq, CancellationToken ct)
    {
        var project = await ProjectAsync(slug, ct);
        return await manager.GetBySequenceAsync(project.Id, seq, ct)
            ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
    }
}
