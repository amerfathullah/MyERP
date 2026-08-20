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
    private readonly IRepository<WorkstationType, Guid> _typeRepository;
    public OperationAppService(IRepository<Operation, Guid> repository, IRepository<WorkstationType, Guid> typeRepository)
    {
        _repository = repository;
        _typeRepository = typeRepository;
    }

    private async Task<string?> ResolveWorkstationTypeNameAsync(Guid? workstationTypeId)
    {
        if (!workstationTypeId.HasValue) return null;
        var type = await _typeRepository.FindAsync(workstationTypeId.Value);
        return type?.Name;
    }

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
        if (input.BatchSize < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "BatchSize");
        }

        var op = new Operation(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            Description = input.Description,
            WorkstationId = input.WorkstationId,
            WorkstationTypeId = input.WorkstationTypeId,
            WorkstationType = input.WorkstationTypeId.HasValue
                ? await ResolveWorkstationTypeNameAsync(input.WorkstationTypeId)
                : input.WorkstationType,
            CreateJobCardBasedOnBatchSize = input.CreateJobCardBasedOnBatchSize,
            BatchSize = input.BatchSize,
            IsCorrectiveOperation = input.IsCorrectiveOperation,
            IsActive = input.IsActive,
        };
        op.SetSubOperations(input.SubOperations.Select(s => (s.OperationId, s.TimeInMins, s.Description)));
        await _repository.InsertAsync(op);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Operation", op.Id,
            "Created", Guid.Empty,
            op.Name, "Draft", "Active", CurrentUser.Id,
            $"Operation '{op.Name}' created", CurrentTenant.Id));

        return ObjectMapper.Map<Operation, OperationDto>(op);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<OperationDto> UpdateAsync(Guid id, CreateOperationDto input)
    {
        if (input.BatchSize < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "BatchSize");
        }

        var op = await _repository.GetAsync(id, includeDetails: true);
        op.Description = input.Description;
        op.WorkstationId = input.WorkstationId;
        op.WorkstationTypeId = input.WorkstationTypeId;
        op.WorkstationType = input.WorkstationTypeId.HasValue
            ? await ResolveWorkstationTypeNameAsync(input.WorkstationTypeId)
            : input.WorkstationType;
        op.CreateJobCardBasedOnBatchSize = input.CreateJobCardBasedOnBatchSize;
        op.BatchSize = input.BatchSize;
        op.IsCorrectiveOperation = input.IsCorrectiveOperation;
        op.IsActive = input.IsActive;
        op.SetSubOperations(input.SubOperations.Select(s => (s.OperationId, s.TimeInMins, s.Description)));
        await _repository.UpdateAsync(op);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Operation", op.Id,
            "Updated", Guid.Empty,
            op.Name, "Active", "Active", CurrentUser.Id,
            $"Operation '{op.Name}' updated", CurrentTenant.Id));

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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Routing", routing.Id,
            "Created", Guid.Empty,
            routing.Name, "Draft", "Active", CurrentUser.Id,
            $"Routing '{routing.Name}' created with {routing.Operations.Count} operations", CurrentTenant.Id));

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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Routing", routing.Id,
            "Updated", Guid.Empty,
            routing.Name, "Active", "Active", CurrentUser.Id,
            $"Routing '{routing.Name}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<Routing, RoutingDto>(routing);
    }

    [Authorize(MyERPPermissions.Manufacturing.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
