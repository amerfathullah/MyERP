using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;

namespace MyERP.Core.EventHandlers;

/// <summary>
/// Wildcard validate hook for company restrictions.
/// Checks any entity containing a CompanyId for references to restricted masters (Item, Customer, Supplier).
/// Implements ERPNext PR #57352 and #57356: global enforcement without manual allowlists.
/// </summary>
public class CompanyRestrictionEventHandler : 
    ILocalEventHandler<EntityCreatedEventData<Entity<Guid>>>, 
    ILocalEventHandler<EntityUpdatedEventData<Entity<Guid>>>, 
    ITransientDependency
{
    private readonly CompanyRestrictionValidationService _validationService;

    // Cache reflection metadata to avoid performance hits
    private static readonly ConcurrentDictionary<Type, EntityRestrictionMetadata> MetadataCache = new();

    public CompanyRestrictionEventHandler(CompanyRestrictionValidationService validationService)
    {
        _validationService = validationService;
    }

    public Task HandleEventAsync(EntityCreatedEventData<Entity<Guid>> eventData)
    {
        return ProcessEventAsync(eventData.Entity);
    }

    public Task HandleEventAsync(EntityUpdatedEventData<Entity<Guid>> eventData)
    {
        return ProcessEventAsync(eventData.Entity);
    }

    private async Task ProcessEventAsync(Entity<Guid> entity)
    {
        var type = entity.GetType();

        // System exempt doctypes are ignored inside validation service, but we skip extracting
        // if it doesn't even have a CompanyId.
        var metadata = MetadataCache.GetOrAdd(type, t => BuildMetadata(t));

        if (metadata.CompanyIdProperty == null)
            return;

        // Exempt documents are checked by name inside ValidateTransactionCompanyAsync,
        // so we just pass the entity type name.
        var documentType = type.Name;
        if (CompanyRestrictionValidationService.IsExemptDocumentType(documentType))
            return;

        var companyIdObj = metadata.CompanyIdProperty.GetValue(entity);
        if (companyIdObj is not Guid companyId || companyId == Guid.Empty)
            return;

        var itemIds = new HashSet<Guid>();
        var customerIds = new HashSet<Guid>();
        var supplierIds = new HashSet<Guid>();

        // Extract from main entity
        ExtractMasterIds(entity, metadata, itemIds, customerIds, supplierIds);

        // Extract from child collections (e.g., Items, Lines, Details)
        foreach (var collectionProp in metadata.ChildCollections)
        {
            var collection = collectionProp.GetValue(entity) as IEnumerable<object>;
            if (collection == null) continue;

            foreach (var child in collection)
            {
                if (child == null) continue;
                var childType = child.GetType();
                var childMetadata = MetadataCache.GetOrAdd(childType, t => BuildMetadata(t));
                ExtractMasterIds(child, childMetadata, itemIds, customerIds, supplierIds);
            }
        }

        if (itemIds.Count > 0 || customerIds.Count > 0 || supplierIds.Count > 0)
        {
            await _validationService.ValidateTransactionCompanyAsync(
                documentType, companyId, itemIds, customerIds, supplierIds);
        }
    }

    private static void ExtractMasterIds(
        object instance, 
        EntityRestrictionMetadata metadata,
        HashSet<Guid> itemIds, 
        HashSet<Guid> customerIds, 
        HashSet<Guid> supplierIds)
    {
        foreach (var prop in metadata.ItemIdProperties)
        {
            if (prop.GetValue(instance) is Guid id && id != Guid.Empty)
                itemIds.Add(id);
        }

        foreach (var prop in metadata.CustomerIdProperties)
        {
            if (prop.GetValue(instance) is Guid id && id != Guid.Empty)
                customerIds.Add(id);
        }

        foreach (var prop in metadata.SupplierIdProperties)
        {
            if (prop.GetValue(instance) is Guid id && id != Guid.Empty)
                supplierIds.Add(id);
        }
    }

    private static EntityRestrictionMetadata BuildMetadata(Type type)
    {
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var metadata = new EntityRestrictionMetadata();

        foreach (var prop in props)
        {
            if (prop.Name == "CompanyId" && prop.PropertyType == typeof(Guid))
            {
                metadata.CompanyIdProperty = prop;
            }
            else if (prop.Name == "ItemId" && (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?)))
            {
                metadata.ItemIdProperties.Add(prop);
            }
            else if (prop.Name == "CustomerId" && (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?)))
            {
                metadata.CustomerIdProperties.Add(prop);
            }
            else if (prop.Name == "SupplierId" && (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?)))
            {
                metadata.SupplierIdProperties.Add(prop);
            }
            else if (typeof(IEnumerable<object>).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
            {
                metadata.ChildCollections.Add(prop);
            }
        }

        return metadata;
    }

    private class EntityRestrictionMetadata
    {
        public PropertyInfo? CompanyIdProperty { get; set; }
        public List<PropertyInfo> ItemIdProperties { get; } = new();
        public List<PropertyInfo> CustomerIdProperties { get; } = new();
        public List<PropertyInfo> SupplierIdProperties { get; } = new();
        public List<PropertyInfo> ChildCollections { get; } = new();
    }
}
