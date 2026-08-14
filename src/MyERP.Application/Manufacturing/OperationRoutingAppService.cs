using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class OperationAppService : ApplicationService, IOperationAppService
{
    private readonly IRepository<Operation, Guid> _repository;
    public OperationAppService(IRepository<Operation, Guid> repository) => _repository = repository;

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
        };
        await _repository.InsertAsync(op);
        return ObjectMapper.Map<Operation, OperationDto>(op);
    }
}

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class RoutingAppService : ApplicationService, IRoutingAppService
{
    private readonly IRepository<Routing, Guid> _repository;
    public RoutingAppService(IRepository<Routing, Guid> repository) => _repository = repository;

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
        var routing = new Routing(GuidGenerator.Create(), input.Name, CurrentTenant.Id);
        foreach (var op in input.Operations)
            routing.AddOperation(op.OperationId, op.SequenceId, op.TimeInMins, op.WorkstationId);
        await _repository.InsertAsync(routing);
        return ObjectMapper.Map<Routing, RoutingDto>(routing);
    }
}
