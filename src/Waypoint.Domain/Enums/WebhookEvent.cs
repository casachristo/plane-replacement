namespace Waypoint.Domain.Enums;

/// <summary>
/// Bit-flag enum stored in WebhookSubscription.EventMask. Subscribers subscribe to
/// the OR of every event class they care about. Wire format is the lowercase
/// event name (e.g. "issue.transitioned"); the Event column on WebhookDelivery
/// carries that string, not the bit value, so subscribers don't need to know the
/// enum mapping.
/// </summary>
[Flags]
public enum WebhookEvent : long
{
    None                          = 0,
    IssueCreated                  = 1L << 0,
    IssueUpdated                  = 1L << 1,
    IssueTransitioned             = 1L << 2,
    IssueDeleted                  = 1L << 3,
    AcceptanceCriterionCreated    = 1L << 4,
    AcceptanceCriterionUpdated    = 1L << 5,
    AcceptanceCriterionChecked    = 1L << 6,
    AcceptanceCriterionUnchecked  = 1L << 7,
    AcceptanceCriterionDeleted    = 1L << 8,
    GateOverrideFired             = 1L << 9,
    CommentCreated                = 1L << 10,
    WorklistCurrentAdvanced       = 1L << 11,
    All                           = ~0L,
}

public static class WebhookEventNames
{
    public static string Wire(WebhookEvent e) => e switch
    {
        WebhookEvent.IssueCreated                 => "issue.created",
        WebhookEvent.IssueUpdated                 => "issue.updated",
        WebhookEvent.IssueTransitioned            => "issue.transitioned",
        WebhookEvent.IssueDeleted                 => "issue.deleted",
        WebhookEvent.AcceptanceCriterionCreated   => "issue.acceptance_criterion.created",
        WebhookEvent.AcceptanceCriterionUpdated   => "issue.acceptance_criterion.updated",
        WebhookEvent.AcceptanceCriterionChecked   => "issue.acceptance_criterion.checked",
        WebhookEvent.AcceptanceCriterionUnchecked => "issue.acceptance_criterion.unchecked",
        WebhookEvent.AcceptanceCriterionDeleted   => "issue.acceptance_criterion.deleted",
        WebhookEvent.GateOverrideFired            => "gate.override_fired",
        WebhookEvent.CommentCreated               => "comment.created",
        WebhookEvent.WorklistCurrentAdvanced      => "worklist.current_advanced",
        _ => "unknown",
    };
}
