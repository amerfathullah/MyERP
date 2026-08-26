using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.EDI.Entities;

/// <summary>
/// Common Code — standard code entry under an EDI Code List (e.g. currency, country, unit, invoice type).
/// Maps to ERPNext edi/doctype/common_code.
/// </summary>
public class CommonCode : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CodeListId { get; set; }
    public string Title { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public string? AdditionalDataJson { get; set; }
    public bool IsActive { get; set; } = true;

    protected CommonCode() { }

    public CommonCode(
        Guid id,
        Guid codeListId,
        string title,
        string code,
        string? description = null,
        string? additionalDataJson = null,
        bool isActive = true,
        Guid? tenantId = null)
        : base(id)
    {
        CodeListId = Check.NotDefaultOrNull<Guid>(codeListId, nameof(codeListId));
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), EDIConsts.MaxTitleLength);
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), EDIConsts.MaxCodeLength);
        Description = description;
        AdditionalDataJson = additionalDataJson;
        IsActive = isActive;
        TenantId = tenantId;
    }
}
