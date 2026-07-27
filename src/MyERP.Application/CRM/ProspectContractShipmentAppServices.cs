using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
using MyERP.Core.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Leads.Default)]
public class ProspectAppService : ApplicationService
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
            queryable = queryable.Where(x => x.ProspectName.Contains(input.Filter));

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

    [Authorize(MyERPPermissions.Leads.Convert)]
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
public class ContractAppService : ApplicationService
{
    private readonly IRepository<Contract, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public ContractAppService(IRepository<Contract, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
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
        entity.ContractValue = input.ContractValue;
        entity.CurrencyCode = input.CurrencyCode;
        entity.RequiresFulfilment = input.RequiresFulfilment;
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
        Notes = e.Notes
    };
}

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class ShipmentAppService : ApplicationService
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
            queryable = queryable.Where(x => x.ShipmentNumber.Contains(input.Filter));
        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<ShipmentStatus>(input.Status, true, out var status))
            queryable = queryable.Where(x => x.Status == status);

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
        var number = await _numberGenerator.GenerateAsync("Shipment", input.CompanyId);
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

        foreach (var dn in input.DeliveryNoteIds ?? [])
            entity.AddDeliveryNote(GuidGenerator.Create(), dn);

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

#region DTOs

public class ProspectDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ProspectName { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Territory { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int? NumberOfEmployees { get; set; }
    public bool IsConverted { get; set; }
    public Guid? ConvertedCustomerId { get; set; }
    public int LeadCount { get; set; }
    public int OpportunityCount { get; set; }
    public string? Notes { get; set; }
}

public class CreateProspectDto
{
    public Guid CompanyId { get; set; }
    public string ProspectName { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Territory { get; set; }
    public string? CustomerGroup { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int? NumberOfEmployees { get; set; }
    public string? Notes { get; set; }
}

public class ContractDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ContractNumber { get; set; } = null!;
    public string? ContractName { get; set; }
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? SigningDate { get; set; }
    public ContractStatus Status { get; set; }
    public decimal? ContractValue { get; set; }
    public string? CurrencyCode { get; set; }
    public bool RequiresFulfilment { get; set; }
    public bool IsAutoRenewal { get; set; }
    public string? Notes { get; set; }
}

public class CreateContractDto
{
    public Guid CompanyId { get; set; }
    public string? ContractName { get; set; }
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ContractTerms { get; set; }
    public decimal? ContractValue { get; set; }
    public string? CurrencyCode { get; set; }
    public bool RequiresFulfilment { get; set; }
    public bool IsAutoRenewal { get; set; }
    public int? RenewalReminderDays { get; set; }
    public string? Notes { get; set; }
}

public class ShipmentDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ShipmentNumber { get; set; } = null!;
    public string? PickupFromName { get; set; }
    public string? DeliveryToName { get; set; }
    public DateTime? PickupDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public ShipmentStatus Status { get; set; }
    public int DeliveryNoteCount { get; set; }
    public decimal? TotalNetWeight { get; set; }
    public decimal? ValueOfGoods { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
}

public class CreateShipmentDto
{
    public Guid CompanyId { get; set; }
    public string? PickupFromType { get; set; }
    public Guid? PickupFromId { get; set; }
    public Guid? PickupAddressId { get; set; }
    public string? DeliveryToType { get; set; }
    public Guid? DeliveryToId { get; set; }
    public Guid? DeliveryAddressId { get; set; }
    public DateTime? PickupDate { get; set; }
    public string? Carrier { get; set; }
    public string? CarrierService { get; set; }
    public decimal? TotalNetWeight { get; set; }
    public decimal? TotalGrossWeight { get; set; }
    public string? WeightUom { get; set; }
    public decimal? ValueOfGoods { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
    public List<Guid>? DeliveryNoteIds { get; set; }
}

#endregion
