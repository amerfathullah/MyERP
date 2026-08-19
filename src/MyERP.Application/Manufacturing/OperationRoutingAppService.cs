using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.Manufacturing.Default)]
[RemoteService(false)]
public class OperationAppService : ApplicationService, IOperationAppService
{
    private readonly IRepository<Operation, Guid> _repository;
    public OperationAppService(IRepository<Operation, Guid> repository) => _repository = repository;

    public async Task<OperationDto> GetAsync(Guid id)
    {
        var op = await _repository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<Operation, OperationDto>(op);
    }

    public async Task<PagedResultDto<OperationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(o => o.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<OperationDto>(totalCount, items.Select(x => ObjectMapper.Map<Operation, OperationDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<OperationDto> CreateAsync(CreateOperationDto input)
    {
        var op = new Operation(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            Description = input.Description,
            WorkstationId = input.WorkstationId,
            WorkstationType = input.WorkstationType,
            CreateJobCardBasedOnBatchSize = input.CreateJobCardBasedOnBatchSize,
            BatchSize = input.BatchSize,
            IsCorrectiveOperation = input.IsCorrectiveOperation,
            IsActive = input.IsActive,
        };
        op.SetSubOperations(input.SubOperations.Select(s => (s.OperationId, s.TimeInMins, s.Description)));
        await _repository.InsertAsync(op);
        return ObjectMapper.Map<Operation, OperationDto>(op);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<OperationDto> UpdateAsync(Guid id, CreateOperationDto input)
    {
        var op = await _repository.GetAsync(id, includeDetails: true);
        op.Description = input.Description;
        op.WorkstationId = input.WorkstationId;
        op.WorkstationType = input.WorkstationType;
        op.CreateJobCardBasedOnBatchSize = input.CreateJobCardBasedOnBatchSize;
        op.BatchSize = input.BatchSize;
        op.IsCorrectiveOperation = input.IsCorrectiveOperation;
        op.IsActive = input.IsActive;
        op.SetSubOperations(input.SubOperations.Select(s => (s.OperationId, s.TimeInMins, s.Description)));
        await _repository.UpdateAsync(op);
        return ObjectMapper.Map<Operation, OperationDto>(op);
    }

    [Authorize(MyERPPermissions.Manufacturing.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}

[Authorize(MyERPPermissions.Manufacturing.Default)]
[RemoteService(false)]
public class RoutingAppService : ApplicationService, IRoutingAppService
{
    private readonly IRepository<Routing, Guid> _repository;
    public RoutingAppService(IRepository<Routing, Guid> repository) => _repository = repository;

    public async Task<RoutingDto> GetAsync(Guid id)
    {
        var routing = await _repository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<Routing, RoutingDto>(routing);
    }

    public async Task<PagedResultDto<RoutingDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderBy(r => r.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<RoutingDto>(totalCount, items.Select(x => ObjectMapper.Map<Routing, RoutingDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<RoutingDto> CreateAsync(CreateRoutingDto input)
    {
        var routing = new Routing(GuidGenerator.Create(), input.Name, CurrentTenant.Id) { IsDisabled = input.IsDisabled };
        foreach (var op in input.Operations)
            routing.AddOperation(op.OperationId, op.SequenceId, op.TimeInMins, op.WorkstationId);
        await _repository.InsertAsync(routing);
        return ObjectMapper.Map<Routing, RoutingDto>(routing);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<RoutingDto> UpdateAsync(Guid id, CreateRoutingDto input)
    {
        var routing = await _repository.GetAsync(id, includeDetails: true);
        routing.Name = input.Name;
        routing.IsDisabled = input.IsDisabled;
        routing.ReplaceOperations(input.Operations
            .Select(o => (o.OperationId, o.SequenceId, o.TimeInMins, o.WorkstationId, (string?)null)));
        await _repository.UpdateAsync(routing);
        return ObjectMapper.Map<Routing, RoutingDto>(routing);
    }

    [Authorize(MyERPPermissions.Manufacturing.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
