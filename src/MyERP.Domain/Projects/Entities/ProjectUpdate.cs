using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Projects.Entities;

/// <summary>
/// Project Update — checkpoint progress update, status log, and milestone report for a project.
/// Maps to ERPNext projects/doctype/project_update.
/// </summary>
public class ProjectUpdate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ProjectId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan? Time { get; set; }
    public decimal PercentComplete { get; set; }
    public string? Summary { get; set; }
    public string? Notes { get; set; }
    public bool Sent { get; set; }

    protected ProjectUpdate() { }

    public ProjectUpdate(
        Guid id,
        Guid projectId,
        DateTime date,
        decimal percentComplete = 0,
        string? summary = null,
        string? notes = null,
        TimeSpan? time = null,
        Guid? tenantId = null)
        : base(id)
    {
        ProjectId = projectId;
        Date = date;
        PercentComplete = percentComplete;
        Summary = summary;
        Notes = notes;
        Time = time ?? DateTime.Now.TimeOfDay;
        TenantId = tenantId;
    }

    /// <summary>
    /// Resolves the author email of a Project Update reply communication.
    /// Per ERPNext commit 5d66d483c3 (get_reply_author):
    /// Credits only a direct reply to a per-user sent copy, from the address the copy was sent to.
    /// Legacy updates sent before per-user copies (when <paramref name="sentCopies"/> is empty)
    /// fall back to <c>reply.Sender</c>.
    /// </summary>
    public static string? ResolveReplyAuthor(
        ProjectUpdateReplyMessage reply,
        IReadOnlyDictionary<string, string>? sentCopies)
    {
        if (sentCopies == null || sentCopies.Count == 0)
        {
            return reply.Sender;
        }

        if (string.IsNullOrWhiteSpace(reply.InReplyTo)
            || !sentCopies.TryGetValue(reply.InReplyTo, out var recipient)
            || string.IsNullOrWhiteSpace(recipient))
        {
            return null;
        }

        if (string.Equals(recipient.Trim(), (reply.Sender ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return recipient;
        }

        return null;
    }

    /// <summary>
    /// Filters and returns uncollected project status replies from current project members.
    /// Per ERPNext commit 5d66d483c3 (get_project_update_replies / get_uncollected_replies):
    /// - Validates that the reply is a direct reply to a sent copy from that copy's recipient.
    /// - Validates that the resolved user is currently assigned to the project (<paramref name="projectMemberUserIds"/>).
    /// - Skips replies whose <c>(UserId, ProjectStatus)</c> count has already been collected.
    /// </summary>
    public static List<ProjectUpdateCollectedReply> GetUncollectedMemberReplies(
        IEnumerable<ProjectUpdateReplyMessage> replies,
        IReadOnlyDictionary<string, string>? sentCopies,
        IReadOnlyDictionary<string, Guid> userByEmail,
        IReadOnlySet<Guid> projectMemberUserIds,
        IEnumerable<ProjectUpdateCollectedReply>? alreadyCollected = null)
    {
        var collectedCounts = new Dictionary<(Guid UserId, string Status), int>();
        if (alreadyCollected != null)
        {
            foreach (var row in alreadyCollected)
            {
                var key = (row.UserId, row.ProjectStatus ?? string.Empty);
                collectedCounts[key] = collectedCounts.GetValueOrDefault(key, 0) + 1;
            }
        }

        var uncollected = new List<ProjectUpdateCollectedReply>();
        foreach (var reply in replies)
        {
            var authorEmail = ResolveReplyAuthor(reply, sentCopies);
            if (string.IsNullOrWhiteSpace(authorEmail))
                continue;

            if (!userByEmail.TryGetValue(authorEmail.Trim(), out var userId))
            {
                var match = userByEmail.FirstOrDefault(kvp =>
                    string.Equals(kvp.Key.Trim(), authorEmail.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match.Value == Guid.Empty)
                    continue;
                userId = match.Value;
            }

            if (!projectMemberUserIds.Contains(userId))
                continue;

            var statusText = (reply.TextContent ?? reply.Content ?? string.Empty).Trim();
            var key = (userId, statusText);
            if (collectedCounts.TryGetValue(key, out var existingCount) && existingCount > 0)
            {
                collectedCounts[key] = existingCount - 1;
                continue;
            }

            uncollected.Add(new ProjectUpdateCollectedReply(userId, authorEmail.Trim(), statusText));
        }

        return uncollected;
    }
}

/// <summary>Incoming communication reply for a Project Update (ERPNext commit 5d66d483c3).</summary>
public record ProjectUpdateReplyMessage(
    string? Sender,
    string? InReplyTo,
    string? Content,
    string? TextContent = null);

/// <summary>Validated project status reply credited to a project member.</summary>
public record ProjectUpdateCollectedReply(
    Guid UserId,
    string UserEmail,
    string ProjectStatus);
