using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Item Attribute — defines a configurable dimension for item variants (e.g., Color, Size, Weight).
/// Attributes can be text-based (discrete values) or numeric (range with increment).
/// 
/// Per ERPNext:
/// - Numeric: from_range, to_range, increment, validation = (value - from) % increment == 0
/// - Text: discrete set of values with abbreviations
/// - Abbreviation changes cascade rename to ALL variant item_codes
/// 
/// Source: erpnext/stock/doctype/item_attribute/item_attribute.py
/// </summary>
public class ItemAttribute : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Attribute name (e.g., "Color", "Size", "Weight").</summary>
    public string AttributeName { get; set; } = null!;

    /// <summary>True if this is a numeric range attribute (weight, length, etc.).</summary>
    public bool IsNumeric { get; set; }

    /// <summary>Numeric: start of valid range.</summary>
    public decimal? FromRange { get; set; }

    /// <summary>Numeric: end of valid range.</summary>
    public decimal? ToRange { get; set; }

    /// <summary>Numeric: valid increment step.</summary>
    public decimal? Increment { get; set; }

    /// <summary>Text attribute values (for non-numeric attributes).</summary>
    public ICollection<ItemAttributeValue> Values { get; private set; }
        = new List<ItemAttributeValue>();

    protected ItemAttribute() { }

    public ItemAttribute(Guid id, string attributeName, bool isNumeric = false, Guid? tenantId = null)
        : base(id)
    {
        AttributeName = Check.NotNullOrWhiteSpace(attributeName, nameof(attributeName));
        IsNumeric = isNumeric;
        TenantId = tenantId;
    }

    /// <summary>
    /// Configure as a numeric attribute with range and increment.
    /// </summary>
    public void SetNumericRange(decimal fromRange, decimal toRange, decimal increment)
    {
        if (fromRange >= toRange)
            throw new BusinessException("MyERP:05019")
                .WithData("from", fromRange).WithData("to", toRange);

        if (increment <= 0)
            throw new BusinessException("MyERP:05020")
                .WithData("increment", increment);

        IsNumeric = true;
        FromRange = fromRange;
        ToRange = toRange;
        Increment = increment;
    }

    /// <summary>
    /// Add a discrete text value to the attribute.
    /// </summary>
    public void AddValue(string value, string abbreviation)
    {
        if (IsNumeric)
            throw new BusinessException("MyERP:05021");

        if (Values.Any(v => string.Equals(v.AttributeValue, value, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessException("MyERP:05022")
                .WithData("value", value);

        Values.Add(new ItemAttributeValue(Guid.NewGuid(), Id, value, abbreviation));
    }

    /// <summary>
    /// Validate a numeric attribute value is within range and on increment boundary.
    /// Per ERPNext: (value - from_range) % increment == 0
    /// </summary>
    public bool IsValidNumericValue(decimal value)
    {
        if (!IsNumeric || !FromRange.HasValue || !ToRange.HasValue || !Increment.HasValue)
            return false;

        if (value < FromRange.Value || value > ToRange.Value)
            return false;

        // Check increment alignment: (value - from) % increment == 0
        var remainder = (value - FromRange.Value) % Increment.Value;
        return Math.Abs(remainder) < 0.0001m; // Precision-aware comparison
    }
}

/// <summary>
/// A discrete value for a text-based item attribute (e.g., "Red" with abbreviation "RED").
/// </summary>
public class ItemAttributeValue : Entity<Guid>
{
    public Guid ItemAttributeId { get; set; }
    public string AttributeValue { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;

    protected ItemAttributeValue() { }

    public ItemAttributeValue(Guid id, Guid attributeId, string value, string abbreviation)
        : base(id)
    {
        ItemAttributeId = attributeId;
        AttributeValue = value;
        Abbreviation = Check.NotNullOrWhiteSpace(abbreviation, nameof(abbreviation));
    }
}

/// <summary>
/// Links a specific attribute value to a variant item.
/// Each variant has one entry per template attribute.
/// </summary>
public class ItemVariantAttribute : Entity<Guid>
{
    public Guid ItemId { get; set; }
    public Guid ItemAttributeId { get; set; }

    /// <summary>The specific value for this variant (e.g., "Red", "XL", "2.5").</summary>
    public string AttributeValue { get; set; } = null!;

    protected ItemVariantAttribute() { }

    public ItemVariantAttribute(Guid id, Guid itemId, Guid attributeId, string attributeValue)
        : base(id)
    {
        ItemId = itemId;
        ItemAttributeId = attributeId;
        AttributeValue = attributeValue;
    }
}
