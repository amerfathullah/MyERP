using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using MyERP.Maintenance.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Maintenance;

[Authorize(MyERPPermissions.WarrantyClaims.Default)]
public class WarrantyClaimAppService : ApplicationService, IWarrantyClaimAppService
{
    private readonly IRepository<WarrantyClaim, Guid> _repository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public WarrantyClaimAppService(
        IRepository<WarrantyClaim, Guid> repository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
    }

    public async Task<PagedResultDto<WarrantyClaimDto>> GetListAsync(GetWarrantyClaimListDto input)
    {
        var queryable = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            queryable = queryable.Where(x => x.CompanyId == input.CompanyId.Value);

        if (input.Status.HasValue)
            queryable = queryable.Where(x => (int)x.Status == input.Status.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
            queryable = queryable.Where(x =>
                x.ClaimNumber.Contains(input.Filter) ||
                (x.Complaint != null && x.Complaint.Contains(input.Filter)));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(x => x.ComplaintDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        // Batch resolve customer + item names
        var customerIds = items.Select(x => x.CustomerId).Distinct().ToList();
        var itemIds = items.Select(x => x.ItemId).Distinct().ToList();

        var customerQ = await _customerRepository.GetQueryableAsync();
        var customerNames = customerQ.Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name }).ToList()
            .ToDictionary(c => c.Id, c => c.Name);

        var itemQ = await _itemRepository.GetQueryableAsync();
        var itemNames = itemQ.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemName }).ToList()
            .ToDictionary(i => i.Id, i => i.ItemName);

        return new PagedResultDto<WarrantyClaimDto>(
            totalCount,
            items.Select(e => MapToDto(e, customerNames, itemNames)).ToList());
    }

    public async Task<WarrantyClaimDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);

        var customer = await _customerRepository.FindAsync(entity.CustomerId);
        var item = await _itemRepository.FindAsync(entity.ItemId);

        var customerNames = new System.Collections.Generic.Dictionary<Guid, string>();
        var itemNames = new System.Collections.Generic.Dictionary<Guid, string>();
        if (customer != null) customerNames[customer.Id] = customer.Name;
        if (item != null) itemNames[item.Id] = item.ItemName;

        return MapToDto(entity, customerNames, itemNames);
    }

    [Authorize(MyERPPermissions.WarrantyClaims.Create)]
    public async Task<WarrantyClaimDto> CreateAsync(CreateWarrantyClaimDto input)
    {
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemAsync(input.ItemId);

        var entity = new WarrantyClaim(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            input.ItemId,
            input.ComplaintDate,
            CurrentTenant.Id);

        entity.ClaimNumber = $"WC-{DateTime.UtcNow:yyyyMMdd}-{GuidGenerator.Create().ToString()[..6].ToUpperInvariant()}";
        entity.SerialNoId = input.SerialNoId;
        entity.SalesInvoiceId = input.SalesInvoiceId;
        entity.WarrantyExpiryDate = input.WarrantyExpiryDate;
        entity.AmcExpiryDate = input.AmcExpiryDate;
        entity.Complaint = input.Complaint;

        await _repository.InsertAsync(entity);

        return MapToDto(entity,
            new System.Collections.Generic.Dictionary<Guid, string>(),
            new System.Collections.Generic.Dictionary<Guid, string>());
    }

    [Authorize(MyERPPermissions.WarrantyClaims.Edit)]
    public async Task StartWorkAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.StartWork();
        await _repository.UpdateAsync(entity);
    }

    [Authorize(MyERPPermissions.WarrantyClaims.Edit)]
    public async Task CloseAsync(Guid id, string? resolution)
    {
        var entity = await _repository.GetAsync(id);
        entity.Close(resolution);
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "WarrantyClaim", entity.Id,
            "Closed", entity.CompanyId,
            entity.ClaimNumber, "WorkInProgress", "Closed", CurrentUser.Id,
            $"Warranty Claim {entity.ClaimNumber} closed. Resolution: {resolution}", CurrentTenant.Id));
    }

    [Authorize(MyERPPermissions.WarrantyClaims.Edit)]
    public async Task CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "WarrantyClaim", entity.Id,
            "Cancelled", entity.CompanyId,
            entity.ClaimNumber, entity.Status.ToString(), "Cancelled", CurrentUser.Id,
            $"Warranty Claim {entity.ClaimNumber} cancelled", CurrentTenant.Id));
    }

    private static WarrantyClaimDto MapToDto(
        WarrantyClaim e,
        System.Collections.Generic.Dictionary<Guid, string> customerNames,
        System.Collections.Generic.Dictionary<Guid, string> itemNames) => new()
    {
        Id = e.Id,
        CompanyId = e.CompanyId,
        ClaimNumber = e.ClaimNumber,
        CustomerId = e.CustomerId,
        CustomerName = customerNames.TryGetValue(e.CustomerId, out var cn) ? cn : null,
        ItemId = e.ItemId,
        ItemName = itemNames.TryGetValue(e.ItemId, out var itn) ? itn : null,
        SerialNoId = e.SerialNoId,
        SalesInvoiceId = e.SalesInvoiceId,
        WarrantyExpiryDate = e.WarrantyExpiryDate,
        AmcExpiryDate = e.AmcExpiryDate,
        ComplaintDate = e.ComplaintDate,
        Complaint = e.Complaint,
        Resolution = e.Resolution,
        ResolutionDate = e.ResolutionDate,
        Status = (int)e.Status,
        IsUnderWarranty = e.IsUnderWarranty()
    };
}
