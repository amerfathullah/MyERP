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
    /// Fields that cannot be copied from a template item to its variants.
    /// Maps to ERPNext stock/doctype/item_variant_settings/item_variant_settings.py:
    /// invalid_fields_for_copy_fields_in_variants (PR #60128 / commit ccfecedb81).
    /// </summary>
    public static readonly IReadOnlySet<string> InvalidFieldsForCopyFieldsInVariants =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "barcodes",
            "attributes",
            "has_variants",
            "variant_of",
            nameof(Item.Barcodes),
            nameof(Item.VariantAttributes),
            nameof(Item.HasVariants),
            nameof(Item.VariantOfId)
        };

    /// <summary>
    /// Validates that configured copy fields do not include forbidden variant/template fields.
    /// Maps to ERPNext stock/doctype/item_variant_settings/item_variant_settings.py: validate (PR #60128 / commit ccfecedb81).
    /// </summary>
    public void ValidateCopyFields(IEnumerable<string> fieldsToCopy)
    {
        if (fieldsToCopy == null) return;

        foreach (var field in fieldsToCopy)
        {
            if (!string.IsNullOrWhiteSpace(field) && InvalidFieldsForCopyFieldsInVariants.Contains(field.Trim()))
            {
                throw new BusinessException(MyERPDomainErrorCodes.InvalidVariantCopyField)
                    .WithData("field", field.Trim());
            }
        }
    }

    /// <summary>
    /// Copies template attributes to a variant item.
    /// When updating an existing variant (!isNewVariant) and allowDifferentUom is enabled,
    /// preserves the variant's existing Stock UOM, Sales UOM, and Purchase UOM instead of overwriting
    /// with the template's UOMs (ERPNext PR #60128 / commit ccfecedb81).
    /// </summary>
    public void CopyAttributesToVariant(Item template, Item variant, bool isNewVariant, bool allowDifferentUom = false)
    {
        variant.VariantOfId = template.Id;
        if (isNewVariant || !allowDifferentUom)
        {
            variant.Uom = template.Uom;
            variant.SalesUom = template.SalesUom;
            variant.PurchaseUom = template.PurchaseUom;
        }
        variant.ValuationMethod = template.ValuationMethod;
        variant.StandardSellingPrice = template.StandardSellingPrice;
        variant.StandardBuyingPrice = template.StandardBuyingPrice;
        variant.TaxCategoryId = template.TaxCategoryId;
        variant.MaintainStock = template.MaintainStock;
        variant.ItemGroupId = template.ItemGroupId;
        variant.ItemGroup = template.ItemGroup;
        variant.Brand = template.Brand;
        variant.DefaultIncomeAccountId = template.DefaultIncomeAccountId;
        variant.DefaultExpenseAccountId = template.DefaultExpenseAccountId;
        variant.DefaultInventoryAccountId = template.DefaultInventoryAccountId;
    }

    /// <summary>
    /// Synchronizes template attributes to all existing variants of the template.
    /// Preserves variant UOMs when allowDifferentUom is true (ERPNext PR #60128 / commit ccfecedb81).
    /// </summary>
    public async Task<List<Item>> SyncTemplateToVariantsAsync(Guid templateItemId, bool allowDifferentUom = false)
    {
        var template = await _itemRepository.GetAsync(templateItemId);
        if (!template.HasVariants)
            throw new BusinessException("MyERP:05023")
                .WithData("item", template.ItemCode);

        var query = await _itemRepository.GetQueryableAsync();
        var variants = query.Where(i => i.VariantOfId == templateItemId).ToList();

        foreach (var variant in variants)
        {
            CopyAttributesToVariant(template, variant, isNewVariant: false, allowDifferentUom: allowDifferentUom);
            await _itemRepository.UpdateAsync(variant);
        }

        return variants;
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

        CopyAttributesToVariant(template, variant, isNewVariant: true, allowDifferentUom: false);

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
                        .WithData("value", attr.Value ?? "")
                        .WithData("from", definition.FromRange ?? 0m)
                        .WithData("to", definition.ToRange ?? 0m)
                        .WithData("increment", definition.Increment ?? 0m);
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
