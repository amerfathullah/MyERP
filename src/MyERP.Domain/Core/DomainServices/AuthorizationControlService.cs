using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Authorization Control — validates transactions against configurable threshold rules.
/// Blocks submission if the current user doesn't have sufficient approval authority.
/// 
/// 3-Tier evaluation (strict priority order):
/// 1. User-specific rules (SystemUserId = current user)
/// 2. Role-specific rules (SystemRole in user's roles)  
/// 3. Global rules (no SystemUserId, no SystemRole)
/// 
/// Per ERPNext: Itemwise and Item Group wise Discount are checked at ALL tiers.
/// Company-specific rules take priority over blank-company (global) rules.
/// 
/// Source: erpnext/setup/doctype/authorization_control/authorization_control.py
/// </summary>
public class AuthorizationControlService : DomainService
{
    private readonly IRepository<AuthorizationRule, Guid> _ruleRepository;

    public AuthorizationControlService(IRepository<AuthorizationRule, Guid> ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    /// <summary>
    /// Validate whether the current user can submit a transaction.
    /// Throws BusinessException if the transaction exceeds thresholds and user lacks authority.
    /// </summary>
    /// <param name="transactionType">Document type (e.g., "SalesInvoice")</param>
    /// <param name="companyId">Company of the transaction</param>
    /// <param name="currentUserId">Current user attempting to submit</param>
    /// <param name="currentUserRoles">Roles of the current user</param>
    /// <param name="grandTotal">Transaction grand total in base currency</param>
    /// <param name="averageDiscount">Average discount percentage (0-100)</param>
    /// <param name="itemDiscounts">Per-item discount percentages (for itemwise checks)</param>
    /// <param name="customerId">Customer ID (for customerwise discount rules)</param>
    public async Task ValidateApprovingAuthorityAsync(
        string transactionType,
        Guid companyId,
        Guid currentUserId,
        string[] currentUserRoles,
        decimal grandTotal,
        decimal averageDiscount = 0,
        List<ItemDiscountInfo>? itemDiscounts = null,
        Guid? customerId = null)
    {
        var allRules = await GetRulesForTransactionAsync(transactionType, companyId);
        if (!allRules.Any()) return; // No rules configured = no restrictions

        // Evaluate each Based-On type
        EvaluateRules(allRules, AuthorizationBasedOn.GrandTotal, grandTotal,
            currentUserId, currentUserRoles, customerId);

        EvaluateRules(allRules, AuthorizationBasedOn.AverageDiscount, averageDiscount,
            currentUserId, currentUserRoles, customerId);

        if (customerId.HasValue)
        {
            EvaluateRules(allRules, AuthorizationBasedOn.CustomerwiseDiscount, averageDiscount,
                currentUserId, currentUserRoles, customerId);
        }

        // Itemwise checks are ALWAYS evaluated at all tiers
        if (itemDiscounts != null)
        {
            foreach (var item in itemDiscounts)
            {
                EvaluateRules(allRules, AuthorizationBasedOn.ItemwiseDiscount, item.DiscountPercentage,
                    currentUserId, currentUserRoles, customerId);

                EvaluateRules(allRules, AuthorizationBasedOn.ItemGroupWiseDiscount, item.DiscountPercentage,
                    currentUserId, currentUserRoles, customerId);
            }
        }
    }

    private void EvaluateRules(
        List<AuthorizationRule> allRules,
        AuthorizationBasedOn basedOn,
        decimal value,
        Guid currentUserId,
        string[] currentUserRoles,
        Guid? customerId)
    {
        var rules = allRules
            .Where(r => r.BasedOn == basedOn)
            .ToList();

        if (!rules.Any()) return;

        // For customerwise: filter by customer
        if (basedOn == AuthorizationBasedOn.CustomerwiseDiscount && customerId.HasValue)
        {
            rules = rules.Where(r => !r.CustomerId.HasValue || r.CustomerId == customerId).ToList();
        }

        // Find rules where value exceeds threshold (ordered by tier then threshold descending)
        var exceededRules = rules
            .Where(r => r.IsExceeded(value))
            .OrderBy(r => r.GetTier())
            .ThenByDescending(r => r.ThresholdValue)
            .ToList();

        if (!exceededRules.Any()) return;

        // Apply tier filtering per ERPNext algorithm
        foreach (var rule in exceededRules)
        {
            var tier = rule.GetTier();

            // Tier 1: user-specific — only applies if SystemUserId matches current user
            if (tier == 1 && rule.SystemUserId != currentUserId) continue;

            // Tier 2: role-specific — only applies if user has the SystemRole
            if (tier == 2 && !string.IsNullOrEmpty(rule.SystemRole)
                && !currentUserRoles.Contains(rule.SystemRole, StringComparer.OrdinalIgnoreCase))
                continue;

            // Check if current user is authorized to approve
            if (!rule.IsAuthorizedApprover(currentUserId, currentUserRoles))
            {
                throw new BusinessException("MyERP:01017")
                    .WithData("transactionType", rule.TransactionType)
                    .WithData("basedOn", rule.BasedOn.ToString())
                    .WithData("threshold", rule.ThresholdValue)
                    .WithData("value", value)
                    .WithData("approvingRole", rule.ApprovingRole ?? "N/A");
            }
        }
    }

    private async Task<List<AuthorizationRule>> GetRulesForTransactionAsync(
        string transactionType, Guid companyId)
    {
        var query = await _ruleRepository.GetQueryableAsync();

        // Company-specific rules + global (blank company) rules
        var rules = query
            .Where(r => r.TransactionType == transactionType
                && (!r.CompanyId.HasValue || r.CompanyId == companyId))
            .OrderByDescending(r => r.CompanyId.HasValue) // Company-specific first
            .ThenBy(r => r.ThresholdValue)
            .ToList();

        return rules;
    }
}

/// <summary>
/// Per-item discount information for itemwise authorization checks.
/// </summary>
public class ItemDiscountInfo
{
    public Guid ItemId { get; set; }
    public string? ItemGroup { get; set; }
    public decimal DiscountPercentage { get; set; }
}
