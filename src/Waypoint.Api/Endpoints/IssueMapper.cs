using Waypoint.Contracts;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Endpoints;

/// <summary>Shared Issue -> IssueDto mapping. Requires State and IssueType to be included.</summary>
public static class IssueMapper
{
    public static IssueDto ToDto(Issue i) => new(
        i.Id, i.SequenceId, i.Title, i.DescriptionMd,
        i.StateId, i.State.Name,
        i.IssueTypeId, i.IssueType.Name,
        (int)i.Priority,
        i.EpicId, i.Epic?.Title,
        i.CreatedAt, i.UpdatedAt);
}
