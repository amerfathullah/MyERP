using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

public class OperationDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? WorkstationId { get; set; }
    public string? WorkstationType { get; set; }
    public bool CreateJobCardBasedOnBatchSize { get; set; }
    public int BatchSize { get; set; }
    public bool IsCorrectiveOperation { get; set; }
    public bool IsActive { get; set; }
}

public class RoutingDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public RoutingOperationDto[] Operations { get; set; } = [];
}

public class RoutingOperationDto
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public int SequenceId { get; set; }
    public decimal TimeInMins { get; set; }
    public Guid? WorkstationId { get; set; }
    public decimal OperatingCost { get; set; }
}

public class CreateOperationDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? WorkstationId { get; set; }
    public string? WorkstationType { get; set; }
    public bool CreateJobCardBasedOnBatchSize { get; set; }
    public int BatchSize { get; set; }
}

public class CreateRoutingDto
{
    public string Name { get; set; } = null!;
    public CreateRoutingOperationDto[] Operations { get; set; } = [];
}

public class CreateRoutingOperationDto
{
    public Guid OperationId { get; set; }
    public int SequenceId { get; set; }
    public decimal TimeInMins { get; set; }
    public Guid? WorkstationId { get; set; }
}

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class OperationAppService : ApplicationService
{
    private readonly IRepository<Operation, Guid> _repository;
    public OperationAppService(IRepository<Operation, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<OperationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(o => o.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<OperationDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<OperationDto> CreateAsync(CreateOperationDto input)
    {
        var op = new Operation(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            Description = input.Description, WorkstationId = input.WorkstationId,
            WorkstationType = input.WorkstationType,
            CreateJobCardBasedOnBatchSize = input.CreateJobCardBasedOnBatchSize,
            BatchSize = input.BatchSize,
        };
        await _repository.InsertAsync(op);
        return MapToDto(op);
    }

    private static OperationDto MapToDto(Operation o) => new()
    {
        Id = o.Id, Name = o.Name, Description = o.Description,
        WorkstationId = o.WorkstationId, WorkstationType = o.WorkstationType,
        CreateJobCardBasedOnBatchSize = o.CreateJobCardBasedOnBatchSize,
        BatchSize = o.BatchSize, IsCorrectiveOperation = o.IsCorrectiveOperation,
        IsActive = o.IsActive,
    };
}

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class RoutingAppService : ApplicationService
{
    private readonly IRepository<Routing, Guid> _repository;
    public RoutingAppService(IRepository<Routing, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<RoutingDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderBy(r => r.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<RoutingDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<RoutingDto> CreateAsync(CreateRoutingDto input)
    {
        var routing = new Routing(GuidGenerator.Create(), input.Name, CurrentTenant.Id);
        foreach (var op in input.Operations)
            routing.AddOperation(op.OperationId, op.SequenceId, op.TimeInMins, op.WorkstationId);
        await _repository.InsertAsync(routing);
        return MapToDto(routing);
    }

    private static RoutingDto MapToDto(Routing r) => new()
    {
        Id = r.Id, Name = r.Name, IsDisabled = r.IsDisabled,
        Operations = r.Operations.Select(o => new RoutingOperationDto
        {
            Id = o.Id, OperationId = o.OperationId, SequenceId = o.SequenceId,
            TimeInMins = o.TimeInMins, WorkstationId = o.WorkstationId,
            OperatingCost = o.OperatingCost,
        }).ToArray(),
    };
}
