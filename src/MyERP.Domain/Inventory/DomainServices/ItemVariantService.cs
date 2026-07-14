using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Creates item variants from a template item with specified attribute values.
/// 
/// Per ERPNext:
/// - Template items (has_variants=true) cannot be used directly in transactions
/// - Variant naming: template_code-ATTR1_ABBR-ATTR2_ABBR
/// - Max 600 variants per batch request
/// - Numeric attributes validated: (value - from_range) % increment == 0
/// - Exact match required: all template attributes must be provided
/// - Duplicate detection: same attribute combination = same variant
/// 
/// Source: erpnext/stock/doctype/item/item.py + item_variant.py
/// </summary>
public class ItemVariantService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<ItemAttribute, Guid> _attributeRepository;

    public ItemVariantService(
        IRepository<Item, Guid> itemRepository,
        IRepository<ItemAttribute, Guid> attributeRepository)
    {
        _itemRepository = itemRepository;
        _attributeRepository = attributeRepository;
    }

    /// <summary>
    /// Create a variant from a template item with specific attribute values.
    /// </summary>
    public async Task<Item> CreateVariantAsync(
        Guid templateItemId,
        List<VariantAttributeInput> attributes)
    {
        var template = await _itemRepository.GetAsync(templateItemId);

        if (!template.HasVariants)
            throw new BusinessException("MyERP:05023")
                .WithData("item", template.ItemCode);

        // Generate variant code from template + attribute abbreviations
        var variantCode = GenerateVariantCode(template.ItemCode, attributes);

        // Check if variant already exists (exact attribute match)
        var existingVariant = await FindExistingVariantAsync(templateItemId, attributes);
        if (existingVariant != null)
            throw new BusinessException("MyERP:05024")
                .WithData("variantCode", existingVariant.ItemCode);

        // Create the variant item
        var variant = new Item(
            Guid.NewGuid(),
            template.CompanyId,
            variantCode,
            $"{template.ItemName} - {string.Join(" ", attributes.Select(a => a.Value))}",
            template.ItemType,
            template.TenantId);

        variant.VariantOfId = templateItemId;
        variant.Uom = template.Uom;
        variant.ValuationMethod = template.ValuationMethod;
        variant.StandardSellingPrice = template.StandardSellingPrice;
        variant.StandardBuyingPrice = template.StandardBuyingPrice;
        variant.TaxCategoryId = template.TaxCategoryId;
        variant.MaintainStock = template.MaintainStock;
        variant.ItemGroupId = template.ItemGroupId;

        // Add attribute values
        foreach (var attr in attributes)
        {
            variant.VariantAttributes.Add(new ItemVariantAttribute(
                Guid.NewGuid(), variant.Id, attr.AttributeId, attr.Value));
        }

        await _itemRepository.InsertAsync(variant);
        return variant;
    }

    /// <summary>
    /// Find an existing variant with the exact same attribute combination.
    /// Returns null if no match (exact match only, never partial).
    /// </summary>
    public async Task<Item?> FindExistingVariantAsync(
        Guid templateItemId,
        List<VariantAttributeInput> attributes)
    {
        var query = await _itemRepository.GetQueryableAsync();
        var variants = query
            .Where(i => i.VariantOfId == templateItemId)
            .ToList();

        foreach (var variant in variants)
        {
            if (variant.VariantAttributes.Count != attributes.Count)
                continue;

            var allMatch = attributes.All(input =>
                variant.VariantAttributes.Any(va =>
                    va.ItemAttributeId == input.AttributeId
                    && string.Equals(va.AttributeValue, input.Value, StringComparison.OrdinalIgnoreCase)));

            if (allMatch) return variant;
        }

        return null;
    }

    /// <summary>
    /// Validate attribute values against their definitions (numeric range/increment check).
    /// </summary>
    public async Task ValidateAttributeValuesAsync(List<VariantAttributeInput> attributes)
    {
        foreach (var attr in attributes)
        {
            var definition = await _attributeRepository.GetAsync(attr.AttributeId);

            if (definition.IsNumeric)
            {
                if (!decimal.TryParse(attr.Value, out var numericValue))
                    throw new BusinessException("MyERP:05025")
                        .WithData("attribute", definition.AttributeName)
                        .WithData("value", attr.Value);

                if (!definition.IsValidNumericValue(numericValue))
                    throw new BusinessException("MyERP:05026")
                        .WithData("attribute", definition.AttributeName)
                        .WithData("value", attr.Value)
                        .WithData("from", definition.FromRange)
                        .WithData("to", definition.ToRange)
                        .WithData("increment", definition.Increment);
            }
            else
            {
                // Text attribute: value must be in the defined set
                var validValues = definition.Values.Select(v => v.AttributeValue).ToList();
                if (validValues.Any() && !validValues.Contains(attr.Value, StringComparer.OrdinalIgnoreCase))
                    throw new BusinessException("MyERP:05027")
                        .WithData("attribute", definition.AttributeName)
                        .WithData("value", attr.Value)
                        .WithData("validValues", string.Join(", ", validValues));
            }
        }
    }

    /// <summary>
    /// Generate variant item code from template code + attribute abbreviations.
    /// Pattern: TEMPLATE-ABBR1-ABBR2 (e.g., "TSHIRT-RED-XL")
    /// </summary>
    private static string GenerateVariantCode(string templateCode, List<VariantAttributeInput> attributes)
    {
        var abbrs = attributes.Select(a => a.Abbreviation ?? a.Value.ToUpperInvariant());
        return $"{templateCode}-{string.Join("-", abbrs)}";
    }
}

/// <summary>
/// Input for specifying a variant attribute value.
/// </summary>
public class VariantAttributeInput
{
    public Guid AttributeId { get; set; }
    public string Value { get; set; } = null!;
    public string? Abbreviation { get; set; }
}
