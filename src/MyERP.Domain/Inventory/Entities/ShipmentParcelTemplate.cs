using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Shipment Parcel Template — standard parcel package dimensions and weight.
/// Maps to ERPNext stock/doctype/shipment_parcel_template.
/// </summary>
public class ShipmentParcelTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string ParcelTemplateName { get; set; } = null!;
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Weight { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected ShipmentParcelTemplate() { }

    public ShipmentParcelTemplate(
        Guid id,
        string parcelTemplateName,
        decimal length,
        decimal width,
        decimal height,
        decimal weight,
        string? description = null,
        Guid? tenantId = null)
        : base(id)
    {
        ParcelTemplateName = Check.NotNullOrWhiteSpace(parcelTemplateName, nameof(parcelTemplateName), maxLength: ShipmentParcelTemplateConsts.MaxParcelTemplateNameLength);
        Length = length;
        Width = width;
        Height = height;
        Weight = weight;
        Description = description;
        TenantId = tenantId;
        ValidateDimensionsAndWeight();
    }

    /// <summary>
    /// Validates that Length, Width, Height, and Weight are all strictly positive (> 0).
    /// Maps to ERPNext stock/doctype/shipment_parcel_template/shipment_parcel_template.py (PR #60132 / commit 2b3fadb4e9).
    /// </summary>
    public void ValidateDimensionsAndWeight()
    {
        if (Length <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.ParcelTemplateDimensionsMustBePositive)
                .WithData("field", nameof(Length));
        if (Width <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.ParcelTemplateDimensionsMustBePositive)
                .WithData("field", nameof(Width));
        if (Height <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.ParcelTemplateDimensionsMustBePositive)
                .WithData("field", nameof(Height));
        if (Weight <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.ParcelTemplateDimensionsMustBePositive)
                .WithData("field", nameof(Weight));
    }
}
