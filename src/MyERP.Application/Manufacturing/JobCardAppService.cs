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

public class JobCardDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? WorkstationId { get; set; }
    public decimal ForQuantity { get; set; }
    public decimal CompletedQty { get; set; }
    public decimal TotalTimeInMins { get; set; }
    public decimal PlannedTimeInMins { get; set; }
    public int SequenceId { get; set; }
    public int Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public JobCardTimeLogDto[] TimeLogs { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class JobCardTimeLogDto
{
    public Guid Id { get; set; }
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal TimeInMins { get; set; }
    public decimal CompletedQty { get; set; }
}

public class CreateJobCardDto
{
    public Guid CompanyId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? WorkstationId { get; set; }
    public decimal ForQuantity { get; set; }
    public int SequenceId { get; set; }
    public decimal PlannedTimeInMins { get; set; }
}

public class AddTimeLogDto
{
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal CompletedQty { get; set; }
}

public class GetJobCardListDto : PagedAndSortedResultRequestDto
{
    public Guid? WorkOrderId { get; set; }
    public Guid? CompanyId { get; set; }
    public JobCardStatus? Status { get; set; }
    public string? Filter { get; set; }
}

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class JobCardAppService : ApplicationService
{
    private readonly IRepository<JobCard, Guid> _repository;

    public JobCardAppService(IRepository<JobCard, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<JobCardDto>> GetListAsync(GetJobCardListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.WorkOrderId.HasValue)
            query = query.Where(j => j.WorkOrderId == input.WorkOrderId.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(j => j.CompanyId == input.CompanyId.Value);
        if (input.Status.HasValue)
            query = query.Where(j => j.Status == input.Status.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(j => j.WorkstationType != null && j.WorkstationType.ToLower().Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(j => j.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<JobCardDto>(totalCount, items.Select(x => ObjectMapper.Map<JobCard, JobCardDto>(x)).ToList());
    }

    public async Task<JobCardDto> GetAsync(Guid id)
    {
        var jc = (await _repository.WithDetailsAsync()).First(j => j.Id == id);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<JobCardDto> CreateAsync(CreateJobCardDto input)
    {
        var jc = new JobCard(GuidGenerator.Create(), input.CompanyId, input.WorkOrderId,
            input.OperationId, input.ForQuantity, input.SequenceId, CurrentTenant.Id)
        {
            WorkstationId = input.WorkstationId,
            PlannedTimeInMins = input.PlannedTimeInMins,
        };
        await _repository.InsertAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> StartAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Start();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> AddTimeLogAsync(Guid id, AddTimeLogDto input)
    {
        var jc = (await _repository.WithDetailsAsync()).First(j => j.Id == id);
        jc.AddTimeLog(input.FromTime, input.ToTime, input.CompletedQty);
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> CompleteAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Complete();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> CancelAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Cancel();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> HoldAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Hold();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<JobCardDto> ResumeAsync(Guid id)
    {
        var jc = await _repository.GetAsync(id);
        jc.Resume();
        await _repository.UpdateAsync(jc);
        return ObjectMapper.Map<JobCard, JobCardDto>(jc);
    }
}

