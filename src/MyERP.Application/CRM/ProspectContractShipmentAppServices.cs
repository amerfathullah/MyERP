using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Leads.Default)]
public class ProspectAppService : ApplicationService, IProspectAppService
{
    private readonly IRepository<Prospect, Guid> _repository;

    public ProspectAppService(IRepository<Prospect, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ProspectDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<ProspectDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            queryable = queryable.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            queryable = queryable.Where(x => x.ProspectName.Contains(input.Filter)
                || (x.CompanyName != null && x.CompanyName.Contains(input.Filter)));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();
        return new PagedResultDto<ProspectDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<ProspectDto> CreateAsync(CreateProspectDto input)
    {
        var entity = new Prospect(GuidGenerator.Create(), input.CompanyId, input.ProspectName, CurrentTenant.Id);
        entity.CompanyName = input.CompanyName;
        entity.Industry = input.Industry;
        entity.Website = input.Website;
        entity.Territory = input.Territory;
        entity.CustomerGroup = input.CustomerGroup;
        entity.AnnualRevenue = input.AnnualRevenue;
        entity.NumberOfEmployees = input.NumberOfEmployees;
        entity.Notes = input.Notes;

        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ProspectDto> ConvertToCustomerAsync(Guid id, Guid customerId)
    {
        var entity = await _repository.GetAsync(id);
        entity.ConvertToCustomer(customerId);
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ProspectDto> AddLeadAsync(Guid id, Guid leadId, string? leadName = null, string? email = null)
    {
        var entity = await _repository.GetAsync(id);
        entity.AddLead(GuidGenerator.Create(), leadId, leadName, email);
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<ProspectDto> UpdateAsync(Guid id, CreateProspectDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.ProspectName = input.ProspectName;
        entity.CompanyName = input.CompanyName;
        entity.Industry = input.Industry;
        entity.Website = input.Website;
        entity.Territory = input.Territory;
        entity.CustomerGroup = input.CustomerGroup;
        entity.AnnualRevenue = input.AnnualRevenue;
        entity.NumberOfEmployees = input.NumberOfEmployees;
        entity.Notes = input.Notes;
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    private static ProspectDto MapToDto(Prospect e) => new()
    {
        Id = e.Id,
        CompanyId = e.CompanyId,
        ProspectName = e.ProspectName,
        CompanyName = e.CompanyName,
        Industry = e.Industry,
        Website = e.Website,
        Territory = e.Territory,
        AnnualRevenue = e.AnnualRevenue,
        NumberOfEmployees = e.NumberOfEmployees,
        IsConverted = e.IsConverted,
        ConvertedCustomerId = e.ConvertedCustomerId,
        LeadCount = e.Leads.Count,
        OpportunityCount = e.Opportunities.Count,
        Notes = e.Notes
    };
}

[Authorize(MyERPPermissions.Leads.Default)]
public class ContractAppService : ApplicationService, IContractAppService
{
    private readonly IRepository<Contract, Guid> _repository;
    private readonly IRepository<ContractTemplate, Guid> _templateRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public ContractAppService(IRepository<Contract, Guid> repository,
        IRepository<ContractTemplate, Guid> templateRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _templateRepository = templateRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<ContractDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<ContractDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            queryable = queryable.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            queryable = queryable.Where(x => x.ContractNumber.Contains(input.Filter)
                || (x.ContractName != null && x.ContractName.Contains(input.Filter)));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(x => x.StartDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();
        return new PagedResultDto<ContractDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<ContractDto> CreateAsync(CreateContractDto input)
    {
        var number = await _numberGenerator.GenerateAsync("Contract", input.CompanyId);
        var entity = new Contract(GuidGenerator.Create(), input.CompanyId, number,
            input.PartyType, input.PartyId, input.StartDate, CurrentTenant.Id);
        entity.ContractName = input.ContractName;
        entity.EndDate = input.EndDate;
        entity.ContractTerms = input.ContractTerms;
        entity.RequiresFulfilment = input.RequiresFulfilment;

        if (input.ContractTemplateId.HasValue)
        {
            var template = await _templateRepository.GetAsync(input.ContractTemplateId.Value);
            entity.ContractTemplateId = template.Id;
            if (string.IsNullOrWhiteSpace(entity.ContractTerms))
                entity.ContractTerms = template.ContractTerms;
            if (!input.RequiresFulfilment)
                entity.RequiresFulfilment = template.RequiresFulfilment;
        }

        entity.ContractValue = input.ContractValue;
        entity.CurrencyCode = input.CurrencyCode;
        entity.IsAutoRenewal = input.IsAutoRenewal;
        entity.RenewalReminderDays = input.RenewalReminderDays;
        entity.Notes = input.Notes;

        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ContractDto> SignAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Sign(DateTime.UtcNow);
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ContractDto> RenewAsync(Guid id, DateTime newEndDate)
    {
        var entity = await _repository.GetAsync(id);
        entity.Renew(newEndDate);
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ContractDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<ContractDto> UpdateAsync(Guid id, CreateContractDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.ContractName = input.ContractName;
        entity.EndDate = input.EndDate;
        entity.ContractTerms = input.ContractTerms;
        entity.ContractValue = input.ContractValue;
        entity.CurrencyCode = input.CurrencyCode;
        entity.RequiresFulfilment = input.RequiresFulfilment;
        entity.IsAutoRenewal = input.IsAutoRenewal;
        entity.Notes = input.Notes;
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    private static ContractDto MapToDto(Contract e) => new()
    {
        Id = e.Id,
        CompanyId = e.CompanyId,
        ContractNumber = e.ContractNumber,
        ContractName = e.ContractName,
        PartyType = e.PartyType,
        PartyId = e.PartyId,
        PartyName = e.PartyName,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        SigningDate = e.SigningDate,
        Status = e.Status,
        ContractValue = e.ContractValue,
        CurrencyCode = e.CurrencyCode,
        RequiresFulfilment = e.RequiresFulfilment,
        IsAutoRenewal = e.IsAutoRenewal,
        Notes = e.Notes,
        ContractTemplateId = e.ContractTemplateId,
        ContractTerms = e.ContractTerms,
    };
}

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class ShipmentAppService : ApplicationService, IShipmentAppService
{
    private readonly IRepository<Shipment, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public ShipmentAppService(IRepository<Shipment, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<ShipmentDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<ShipmentDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            queryable = queryable.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            queryable = queryable.Where(x => x.ShipmentNumber.Contains(input.Filter) ||
                                            (x.Carrier != null && x.Carrier.Contains(input.Filter)) ||
                                            (x.TrackingNumber != null && x.TrackingNumber.Contains(input.Filter)));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();
        return new PagedResultDto<ShipmentDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.SalesOrders.Create)]
    public async Task<ShipmentDto> CreateAsync(CreateShipmentDto input)
    {
        var number = await _numberGenerator.GenerateAsync("SHIP", input.CompanyId);
        var entity = new Shipment(GuidGenerator.Create(), input.CompanyId, number, CurrentTenant.Id);
        entity.PickupFromType = input.PickupFromType;
        entity.PickupFromId = input.PickupFromId;
        entity.PickupAddressId = input.PickupAddressId;
        entity.DeliveryToType = input.DeliveryToType;
        entity.DeliveryToId = input.DeliveryToId;
        entity.DeliveryAddressId = input.DeliveryAddressId;
        entity.PickupDate = input.PickupDate;
        entity.Carrier = input.Carrier;
        entity.CarrierService = input.CarrierService;
        entity.TotalNetWeight = input.TotalNetWeight;
        entity.TotalGrossWeight = input.TotalGrossWeight;
        entity.WeightUom = input.WeightUom;
        entity.ValueOfGoods = input.ValueOfGoods;
        entity.CurrencyCode = input.CurrencyCode;
        entity.Notes = input.Notes;

        foreach (var dnId in input.DeliveryNoteIds ?? [])
            entity.AddDeliveryNote(GuidGenerator.Create(), dnId);

        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ShipmentDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Submit();
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ShipmentDto> MarkInTransitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.MarkInTransit();
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ShipmentDto> MarkDeliveredAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.MarkDelivered(DateTime.UtcNow);
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ShipmentDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.SalesOrders.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static ShipmentDto MapToDto(Shipment e) => new()
    {
        Id = e.Id,
        CompanyId = e.CompanyId,
        ShipmentNumber = e.ShipmentNumber,
        PickupFromName = e.PickupFromName,
        DeliveryToName = e.DeliveryToName,
        PickupDate = e.PickupDate,
        DeliveryDate = e.DeliveryDate,
        Carrier = e.Carrier,
        TrackingNumber = e.TrackingNumber,
        TrackingUrl = e.TrackingUrl,
        Status = e.Status,
        DeliveryNoteCount = e.DeliveryNotes.Count,
        TotalNetWeight = e.TotalNetWeight,
        ValueOfGoods = e.ValueOfGoods,
        CurrencyCode = e.CurrencyCode,
        Notes = e.Notes
    };
}
