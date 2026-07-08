using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Automation.Entities;
using MyERP.Notification.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Automation.DomainServices;

/// <summary>
/// Evaluates automation rules against events and executes matching actions.
/// Called from domain event handlers when documents transition states.
/// </summary>
public class AutomationEngine : DomainService
{
    private readonly IRepository<AutomationRule, Guid> _ruleRepository;
    private readonly IRepository<AutomationExecutionLog, Guid> _logRepository;
    private readonly IRepository<AppNotification, Guid> _notificationRepository;

    public AutomationEngine(
        IRepository<AutomationRule, Guid> ruleRepository,
        IRepository<AutomationExecutionLog, Guid> logRepository,
        IRepository<AppNotification, Guid> notificationRepository)
    {
        _ruleRepository = ruleRepository;
        _logRepository = logRepository;
        _notificationRepository = notificationRepository;
    }

    /// <summary>
    /// Fire all matching rules for a given trigger + document context.
    /// </summary>
    public async Task<List<AutomationExecutionLog>> ExecuteAsync(
        AutomationTrigger trigger,
        string? documentType,
        Guid? documentId,
        Guid? companyId = null,
        Guid? tenantId = null,
        IDictionary<string, object>? contextData = null)
    {
        var rules = await GetMatchingRulesAsync(trigger, documentType, companyId);
        var results = new List<AutomationExecutionLog>();

        foreach (var rule in rules)
        {
            if (!EvaluateCondition(rule.ConditionExpression, contextData))
                continue;

            var log = new AutomationExecutionLog(GuidGenerator.Create(), rule.Id, tenantId)
            {
                SourceDocumentId = documentId,
                SourceDocumentType = documentType,
            };

            var sw = Stopwatch.StartNew();
            try
            {
                await ExecuteActionAsync(rule, documentId, documentType, tenantId, contextData);
                log.IsSuccess = true;
            }
            catch (Exception ex)
            {
                log.IsSuccess = false;
                log.ErrorMessage = ex.Message;
            }
            sw.Stop();
            log.ExecutionDurationMs = (int)sw.ElapsedMilliseconds;

            await _logRepository.InsertAsync(log);
            results.Add(log);
        }

        return results;
    }

    private async Task<List<AutomationRule>> GetMatchingRulesAsync(
        AutomationTrigger trigger, string? documentType, Guid? companyId)
    {
        var rules = await _ruleRepository.GetListAsync(
            r => r.IsActive && r.Trigger == trigger);

        return rules
            .Where(r => r.DocumentType == null || r.DocumentType == documentType)
            .Where(r => r.CompanyId == null || r.CompanyId == companyId)
            .OrderBy(r => r.Priority)
            .ToList();
    }

    private static bool EvaluateCondition(string? expression, IDictionary<string, object>? context)
    {
        if (string.IsNullOrWhiteSpace(expression) || context == null)
            return true; // No condition = always match

        // Simple expression evaluator: "Field > Value" or "Field == Value"
        var parts = expression.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return true;

        var field = parts[0];
        var op = parts[1];
        var expected = parts[2];

        if (!context.TryGetValue(field, out var actual))
            return false;

        return op switch
        {
            "==" => string.Equals(actual?.ToString(), expected, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(actual?.ToString(), expected, StringComparison.OrdinalIgnoreCase),
            ">" when decimal.TryParse(actual?.ToString(), out var a) && decimal.TryParse(expected, out var b) => a > b,
            ">=" when decimal.TryParse(actual?.ToString(), out var a) && decimal.TryParse(expected, out var b) => a >= b,
            "<" when decimal.TryParse(actual?.ToString(), out var a) && decimal.TryParse(expected, out var b) => a < b,
            "<=" when decimal.TryParse(actual?.ToString(), out var a) && decimal.TryParse(expected, out var b) => a <= b,
            _ => true,
        };
    }

    private async Task ExecuteActionAsync(
        AutomationRule rule, Guid? documentId, string? documentType,
        Guid? tenantId, IDictionary<string, object>? contextData)
    {
        switch (rule.Action)
        {
            case AutomationAction.SendNotification:
                await ExecuteNotificationAsync(rule, documentId, documentType, tenantId, contextData);
                break;

            case AutomationAction.SendEmail:
                // Email integration placeholder — would use ABP EmailSender
                break;

            case AutomationAction.SubmitToLhdn:
                // Would call EInvoiceService.SubmitAsync — wired via event handler
                break;

            case AutomationAction.CreateApprovalRequest:
                // Would call ApprovalWorkflowManager.InitiateApprovalAsync
                break;

            case AutomationAction.UpdateField:
                // Field update logic based on ActionConfig JSON
                break;

            case AutomationAction.CreateFollowUpTask:
                // Task creation placeholder
                break;

            case AutomationAction.PostToAccounting:
                // Would call AccountingRuleEngine.PostDocumentAsync
                break;
        }
    }

    private async Task ExecuteNotificationAsync(
        AutomationRule rule, Guid? documentId, string? documentType,
        Guid? tenantId, IDictionary<string, object>? contextData)
    {
        // Parse ActionConfig for notification settings
        var subject = $"[Automation] {rule.Name}";
        if (contextData?.TryGetValue("_userId", out var userIdObj) == true && userIdObj is Guid userId)
        {
            var notification = new AppNotification(GuidGenerator.Create(), userId, subject, tenantId)
            {
                Body = rule.Description ?? $"Rule '{rule.Name}' was triggered for {documentType}",
                Severity = Notification.NotificationSeverity.Info,
                SourceDocumentType = documentType,
                SourceDocumentId = documentId,
                ActionUrl = documentType != null && documentId != null
                    ? $"/{documentType.ToLower()}s/{documentId}"
                    : null,
            };
            await _notificationRepository.InsertAsync(notification);
        }
    }
}
