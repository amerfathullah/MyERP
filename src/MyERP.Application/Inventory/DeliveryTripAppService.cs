using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.DeliveryTrips.Default)]
public class DeliveryTripAppService :
    CrudAppService<
        DeliveryTrip,
        DeliveryTripDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateDeliveryTripDto>,
    IDeliveryTripAppService
{
    public DeliveryTripAppService(IRepository<DeliveryTrip, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.DeliveryTrips.Default;
        GetListPolicyName = MyERPPermissions.DeliveryTrips.Default;
        CreatePolicyName = MyERPPermissions.DeliveryTrips.Create;
        UpdatePolicyName = MyERPPermissions.DeliveryTrips.Edit;
        DeletePolicyName = MyERPPermissions.DeliveryTrips.Delete;
    }

    protected override async Task<IQueryable<DeliveryTrip>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input)
    {
        return await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
    }

    public override async Task<DeliveryTripDto> GetAsync(Guid id)
    {
        await CheckGetPolicyAsync();
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }
        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Create)]
    public override async Task<DeliveryTripDto> CreateAsync(CreateUpdateDeliveryTripDto input)
    {
        var entity = new DeliveryTrip(
            GuidGenerator.Create(),
            input.CompanyId,
            input.TripNumber,
            input.Driver,
            input.Vehicle,
            input.DepartureTime,
            input.NamingSeries)
        {
            DriverName = input.DriverName,
            DriverEmail = input.DriverEmail,
            DriverAddress = input.DriverAddress,
            EmployeeId = input.EmployeeId,
            Uom = input.Uom,
        };

        if (input.DeliveryStops != null)
        {
            foreach (var stopDto in input.DeliveryStops)
            {
                entity.AddStop(
                    stopDto.Address,
                    stopDto.CustomerId,
                    stopDto.CustomerName,
                    stopDto.DeliveryNoteId,
                    stopDto.DeliveryNoteNumber,
                    stopDto.GrandTotal,
                    stopDto.EstimatedArrival,
                    stopDto.Distance,
                    stopDto.Uom,
                    stopDto.Latitude,
                    stopDto.Longitude,
                    stopDto.Details);
            }
        }

        await Repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Edit)]
    public override async Task<DeliveryTripDto> UpdateAsync(Guid id, CreateUpdateDeliveryTripDto input)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        entity.NamingSeries = input.NamingSeries;
        entity.Driver = input.Driver;
        entity.DriverName = input.DriverName;
        entity.DriverEmail = input.DriverEmail;
        entity.DriverAddress = input.DriverAddress;
        entity.Vehicle = input.Vehicle;
        entity.DepartureTime = input.DepartureTime;
        entity.EmployeeId = input.EmployeeId;
        entity.Uom = input.Uom;

        // Sync delivery stops
        var existingStopIds = entity.DeliveryStops.Select(s => s.Id).ToList();
        var incomingStopIds = input.DeliveryStops.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToList();

        // Remove stops not in incoming
        foreach (var stopId in existingStopIds)
        {
            if (!incomingStopIds.Contains(stopId))
            {
                entity.RemoveStop(stopId);
            }
        }

        // Update or add stops
        foreach (var stopDto in input.DeliveryStops)
        {
            if (stopDto.Id.HasValue)
            {
                var stop = entity.DeliveryStops.FirstOrDefault(s => s.Id == stopDto.Id.Value);
                if (stop != null)
                {
                    stop.Address = stopDto.Address;
                    stop.CustomerId = stopDto.CustomerId;
                    stop.CustomerName = stopDto.CustomerName;
                    stop.CustomerAddress = stopDto.CustomerAddress;
                    stop.DeliveryNoteId = stopDto.DeliveryNoteId;
                    stop.DeliveryNoteNumber = stopDto.DeliveryNoteNumber;
                    stop.GrandTotal = stopDto.GrandTotal;
                    stop.ContactName = stopDto.ContactName;
                    stop.EmailSentTo = stopDto.EmailSentTo;
                    stop.CustomerContact = stopDto.CustomerContact;
                    stop.Distance = stopDto.Distance;
                    stop.Uom = stopDto.Uom;
                    stop.EstimatedArrival = stopDto.EstimatedArrival;
                    stop.Latitude = stopDto.Latitude;
                    stop.Longitude = stopDto.Longitude;
                    stop.Details = stopDto.Details;
                    stop.Locked = stopDto.Locked;
                    stop.Visited = stopDto.Visited;
                }
            }
            else
            {
                entity.AddStop(
                    stopDto.Address,
                    stopDto.CustomerId,
                    stopDto.CustomerName,
                    stopDto.DeliveryNoteId,
                    stopDto.DeliveryNoteNumber,
                    stopDto.GrandTotal,
                    stopDto.EstimatedArrival,
                    stopDto.Distance,
                    stopDto.Uom,
                    stopDto.Latitude,
                    stopDto.Longitude,
                    stopDto.Details);
            }
        }

        entity.RecalculateTotalDistance();
        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Schedule)]
    public async Task<DeliveryTripDto> ScheduleAsync(Guid id)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        entity.Schedule();
        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Transit)]
    public async Task<DeliveryTripDto> StartTransitAsync(Guid id)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        entity.StartTransit();
        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Complete)]
    public async Task<DeliveryTripDto> CompleteAsync(Guid id)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        entity.Complete();
        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }

    [Authorize(MyERPPermissions.DeliveryTrips.Cancel)]
    public async Task<DeliveryTripDto> CancelAsync(Guid id)
    {
        var query = await Repository.WithDetailsAsync(dt => dt.DeliveryStops);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(dt => dt.Id == id));
        if (entity == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        }

        entity.Cancel();
        await Repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<DeliveryTrip, DeliveryTripDto>(entity);
    }
}
