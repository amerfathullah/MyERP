using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Manages company restriction settings on master data (Item, Customer, Supplier).
/// Per ERPNext PR #57258/#57352/#57383.
/// GetAsync: any authenticated user (read restriction status).
/// SaveAsync: requires CompanyRestrictions.Manage permission (manager-level only, per permlevel 1).
/// </summary>
[Authorize]
public class CompanyRestrictionAppService : ApplicationService
{
    private readonly IRepository<CompanyRestrictionEntry, Guid> _restrictionRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;

    public CompanyRestrictionAppService(
        IRepository<CompanyRestrictionEntry, Guid> restrictionRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Supplier, Guid> supplierRepo)
    {
        _restrictionRepo = restrictionRepo;
        _itemRepo = itemRepo;
        _customerRepo = customerRepo;
        _supplierRepo = supplierRepo;
    }

    /// <summary>
    /// Gets the current company restriction settings for a master entity.
    /// </summary>
    public async Task<CompanyRestrictionDto> GetAsync(string parentType, Guid parentId)
    {
        bool restrictToCompanies = parentType switch
        {
            "Item" => (await _itemRepo.GetAsync(parentId)).RestrictToCompanies,
            "Customer" => (await _customerRepo.GetAsync(parentId)).RestrictToCompanies,
            "Supplier" => (await _supplierRepo.GetAsync(parentId)).RestrictToCompanies,
            _ => false
        };

        var entries = (await _restrictionRepo.GetQueryableAsync())
            .Where(r => r.ParentType == parentType && r.ParentId == parentId)
            .Select(r => new CompanyRestrictionEntryDto { Id = r.Id, CompanyId = r.CompanyId })
            .ToList();

        return new CompanyRestrictionDto
        {
            ParentType = parentType,
            ParentId = parentId,
            RestrictToCompanies = restrictToCompanies,
            AllowedCompanies = entries
        };
    }

    /// <summary>
    /// Enables or disables company restriction on a master entity and sets the allowed companies.
    /// When disabled, all existing restriction entries are cleared.
    /// Requires CompanyRestrictions.Manage permission (only master-manager roles per PR #57383).
    /// </summary>
    [Authorize(MyERPPermissions.CompanyRestrictions.Manage)]
    public async Task SaveAsync(SaveCompanyRestrictionDto input)
    {
        Check.NotNullOrWhiteSpace(input.ParentType, nameof(input.ParentType));
        Check.NotDefaultOrNull<Guid>(input.ParentId, nameof(input.ParentId));

        // Update the RestrictToCompanies flag on the entity
        switch (input.ParentType)
        {
            case "Item":
                var item = await _itemRepo.GetAsync(input.ParentId);
                item.RestrictToCompanies = input.RestrictToCompanies;
                await _itemRepo.UpdateAsync(item);
                break;
            case "Customer":
                var customer = await _customerRepo.GetAsync(input.ParentId);
                customer.RestrictToCompanies = input.RestrictToCompanies;
                await _customerRepo.UpdateAsync(customer);
                break;
            case "Supplier":
                var supplier = await _supplierRepo.GetAsync(input.ParentId);
                supplier.RestrictToCompanies = input.RestrictToCompanies;
                await _supplierRepo.UpdateAsync(supplier);
                break;
            default:
                throw new BusinessException("MyERP:00001")
                    .WithData("message", $"Unsupported parent type: {input.ParentType}");
        }

        // Delete existing entries
        var existingEntries = (await _restrictionRepo.GetQueryableAsync())
            .Where(r => r.ParentType == input.ParentType && r.ParentId == input.ParentId)
            .ToList();
        await _restrictionRepo.DeleteManyAsync(existingEntries);

        // If enabling restriction, create new allowed company entries
        if (input.RestrictToCompanies && input.AllowedCompanyIds is { Count: > 0 })
        {
            var newEntries = input.AllowedCompanyIds.Select(companyId =>
                new CompanyRestrictionEntry(
                    GuidGenerator.Create(),
                    input.ParentType,
                    input.ParentId,
                    companyId,
                    CurrentTenant.Id)
            ).ToList();

            await _restrictionRepo.InsertManyAsync(newEntries);
        }
        else if (input.RestrictToCompanies && (input.AllowedCompanyIds == null || input.AllowedCompanyIds.Count == 0))
        {
            // Per ERPNext: restrict_to_companies checked but no companies = error
            throw new BusinessException("MyERP:01007")
                .WithData("documentType", "Company Restriction")
                .WithData("message", "At least one company must be selected when restricting access");
        }
    }
}

// --- DTOs ---

public class CompanyRestrictionDto
{
    public string ParentType { get; set; } = null!;
    public Guid ParentId { get; set; }
    public bool RestrictToCompanies { get; set; }
    public List<CompanyRestrictionEntryDto> AllowedCompanies { get; set; } = new();
}

public class CompanyRestrictionEntryDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
}

public class SaveCompanyRestrictionDto
{
    public string ParentType { get; set; } = null!;
    public Guid ParentId { get; set; }
    public bool RestrictToCompanies { get; set; }
    public List<Guid>? AllowedCompanyIds { get; set; }
}
