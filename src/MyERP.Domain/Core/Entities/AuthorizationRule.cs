using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Authorization Rule — defines approval thresholds for transactions.
/// When a transaction exceeds the threshold, only users with the specified
/// approving role/user can submit it.
/// 
/// Per ERPNext:
/// - 3-tier evaluation: user-specific → role-specific → global
/// - Company-specific rules take priority over blank-company (global) rules
/// - Cannot self-approve (system_user ≠ approving_user)
/// - Purchase docs cannot use discount-based rules
/// - Itemwise and ItemGroupWise are checked at ALL tiers
/// 
/// Source: erpnext/setup/doctype/authorization_rule/authorization_rule.py
/// </summary>
public class AuthorizationRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Company scope (null = global, applies to all companies).</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Transaction type this rule applies to (e.g., "SalesInvoice", "PurchaseOrder").</summary>
    public string TransactionType { get; set; } = null!;

    /// <summary>What metric to check (GrandTotal, AverageDiscount, etc.).</summary>
    public AuthorizationBasedOn BasedOn { get; set; }

    /// <summary>Threshold value. Transactions exceeding this need approval.</summary>
    public decimal ThresholdValue { get; set; }

    /// <summary>Specific user this rule applies to (Tier 1). Null = not user-specific.</summary>
    public Guid? SystemUserId { get; set; }

    /// <summary>Role this rule applies to (Tier 2). Null = not role-specific.</summary>
    public string? SystemRole { get; set; }

    /// <summary>Role that can approve transactions matching this rule.</summary>
    public string? ApprovingRole { get; set; }

    /// <summary>Specific user that can approve transactions matching this rule.</summary>
    public Guid? ApprovingUserId { get; set; }

    /// <summary>Specific customer for Customerwise Discount rules.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Specific employee for Expense Claim rules.</summary>
    public Guid? EmployeeId { get; set; }

    /// <summary>Employee designation for Expense Claim rules.</summary>
    public string? Designation { get; set; }

    protected AuthorizationRule() { }

    public AuthorizationRule(
        Guid id,
        string transactionType,
        AuthorizationBasedOn basedOn,
        decimal thresholdValue,
        Guid? companyId = null,
        Guid? tenantId = null) : base(id)
    {
        TransactionType = Check.NotNullOrWhiteSpace(transactionType, nameof(transactionType));
        BasedOn = basedOn;
        ThresholdValue = thresholdValue;
        CompanyId = companyId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Validate rule configuration. Must have at least one approving role or user.
    /// </summary>
    public void Validate()
    {
        // Must have at least one approving authority
        if (string.IsNullOrEmpty(ApprovingRole) && !ApprovingUserId.HasValue)
        {
            throw new BusinessException("MyERP:01013")
                .WithData("rule", TransactionType);
        }

        // Self-approval blocked
        if (SystemUserId.HasValue && ApprovingUserId.HasValue && SystemUserId == ApprovingUserId)
        {
            throw new BusinessException("MyERP:01014")
                .WithData("userId", SystemUserId);
        }

        // Same role cannot be both system and approving
        if (!string.IsNullOrEmpty(SystemRole) && !string.IsNullOrEmpty(ApprovingRole)
            && string.Equals(SystemRole, ApprovingRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("MyERP:01014")
                .WithData("role", SystemRole);
        }

        // Average discount cannot exceed 100%
        if ((BasedOn == AuthorizationBasedOn.AverageDiscount
            || BasedOn == AuthorizationBasedOn.CustomerwiseDiscount)
            && ThresholdValue > 100)
        {
            throw new BusinessException("MyERP:01015")
                .WithData("value", ThresholdValue);
        }

        // Customerwise discount requires customer
        if (BasedOn == AuthorizationBasedOn.CustomerwiseDiscount && !CustomerId.HasValue)
        {
            throw new BusinessException("MyERP:01016")
                .WithData("basedOn", BasedOn);
        }
    }

    /// <summary>
    /// Check if a transaction value exceeds this rule's threshold.
    /// </summary>
    public bool IsExceeded(decimal transactionValue)
    {
        return transactionValue > ThresholdValue;
    }

    /// <summary>
    /// Check if a user is authorized to approve under this rule.
    /// </summary>
    public bool IsAuthorizedApprover(Guid userId, string[] userRoles)
    {
        if (ApprovingUserId.HasValue && ApprovingUserId == userId)
            return true;

        if (!string.IsNullOrEmpty(ApprovingRole))
        {
            foreach (var role in userRoles)
            {
                if (string.Equals(role, ApprovingRole, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determine the tier of this rule for evaluation ordering.
    /// Tier 1: User-specific, Tier 2: Role-specific, Tier 3: Global.
    /// </summary>
    public int GetTier()
    {
        if (SystemUserId.HasValue) return 1;
        if (!string.IsNullOrEmpty(SystemRole)) return 2;
        return 3;
    }
}
