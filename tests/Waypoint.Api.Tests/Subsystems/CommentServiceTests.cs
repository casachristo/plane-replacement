using FluentAssertions;
using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Issues.Comments;
using Waypoint.Domain.Entities;
using Xunit;

namespace Waypoint.Api.Tests.Subsystems;

// Reference test for the architecture standard: a feature Service is tested in isolation
// (no DB, no HTTP) by faking its Manager. This is the payoff of the Manager/Service split.
public class CommentServiceTests
{
    private sealed class FakeManager : ICommentManager
    {
        public Guid? LastAuthor;
        public Task<Comment> CreateAsync(Guid issueId, string bodyMd, Guid? authorUserId, CancellationToken ct)
        {
            LastAuthor = authorUserId;
            return Task.FromResult(new Comment { Id = Guid.NewGuid(), IssueId = issueId, BodyMd = bodyMd, AuthorUserId = authorUserId });
        }
        public Task<IReadOnlyList<Comment>> ListByIssueAsync(Guid issueId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Comment>>(new[] { new Comment { Id = Guid.NewGuid(), IssueId = issueId, BodyMd = "x" } });
    }

    [Fact]
    public async Task Human_principal_resolves_to_an_author_id_and_maps_the_dto()
    {
        var fake = new FakeManager();
        var uid = Guid.NewGuid();
        var dto = await new CommentService(fake).AddAsync(Guid.NewGuid(), "hi",
            new Principal(PrincipalKind.Human, uid.ToString(), "u", []), CancellationToken.None);
        fake.LastAuthor.Should().Be(uid);
        dto.BodyMd.Should().Be("hi");
    }

    [Fact]
    public async Task Service_principal_has_no_author_id()
    {
        var fake = new FakeManager();
        await new CommentService(fake).AddAsync(Guid.NewGuid(), "hi",
            new Principal(PrincipalKind.InternalService, Guid.NewGuid().ToString(), "svc", []), CancellationToken.None);
        fake.LastAuthor.Should().BeNull();
    }
}
