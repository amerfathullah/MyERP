using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.ProcessDeferredAccountings.Default)]
public class ProcessDeferredAccountingAppService : MyERPAppService, IProcessDeferredAccountingAppService
{
    private readonly IRepository<ProcessDeferredAccounting, Guid> _repository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly DeferredAccountingService _deferredAccountingService;

    public ProcessDeferredAccountingAppService(
        IRepository<ProcessDeferredAccounting, Guid> repository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Account, Guid> accountRepository,
        DeferredAccountingService deferredAccountingService)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _accountRepository = accountRepository;
        _deferredAccountingService = deferredAccountingService;
    }

    public async Task<PagedResultDto<ProcessDeferredAccountingDto>> GetListAsync(ProcessDeferredAccountingGetListInput input)
    {
        var queryable = await _repository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            queryable = queryable.Where(x => x.ProcessNumber.Contains(input.Filter));
        }

        if (input.CompanyId.HasValue)
        {
            queryable = queryable.Where(x => x.CompanyId == input.CompanyId.Value);
        }

        if (input.Type.HasValue)
        {
            queryable = queryable.Where(x => x.Type == input.Type.Value);
        }

        if (input.FromDate.HasValue)
        {
            queryable = queryable.Where(x => x.PostingDate >= input.FromDate.Value.Date);
        }

        if (input.ToDate.HasValue)
        {
            queryable = queryable.Where(x => x.PostingDate <= input.ToDate.Value.Date);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? $"{nameof(ProcessDeferredAccounting.PostingDate)} desc, {nameof(ProcessDeferredAccounting.CreationTime)} desc" : input.Sorting;

        var items = await AsyncExecuter.ToListAsync(queryable
            .OrderBy(sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount));

        var companies = (await _companyRepository.GetListAsync()).ToDictionary(c => c.Id, c => c.Name);
        var accounts = (await _accountRepository.GetListAsync()).ToDictionary(a => a.Id, a => a.AccountName);

        var mapper = new ProcessDeferredAccountingMapper();
        var dtos = items.Select(x =>
        {
            var dto = mapper.Map(x);
            if (companies.TryGetValue(x.CompanyId, out var compName)) dto.CompanyName = compName;
            if (x.AccountId.HasValue && accounts.TryGetValue(x.AccountId.Value, out var accName)) dto.AccountName = accName;
            return dto;
        }).ToList();

        return new PagedResultDto<ProcessDeferredAccountingDto>(totalCount, dtos);
    }

    public async Task<ProcessDeferredAccountingDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var dto = new ProcessDeferredAccountingMapper().Map(entity);

        var company = await _companyRepository.FindAsync(entity.CompanyId);
        if (company != null) dto.CompanyName = company.Name;

        if (entity.AccountId.HasValue)
        {
            var account = await _accountRepository.FindAsync(entity.AccountId.Value);
            if (account != null) dto.AccountName = account.AccountName;
        }

        return dto;
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Create)]
    public async Task<ProcessDeferredAccountingDto> CreateAsync(CreateProcessDeferredAccountingDto input)
    {
        var processNumber = $"ACC-PDA-{DateTime.UtcNow:yyyyMMdd}-{GuidGenerator.Create().ToString()[..6].ToUpperInvariant()}";

        var entity = new ProcessDeferredAccounting(
            GuidGenerator.Create(),
            processNumber,
            input.CompanyId,
            input.Type,
            input.PostingDate,
            input.StartDate,
            input.EndDate,
            input.AccountId,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Edit)]
    public async Task<ProcessDeferredAccountingDto> UpdateAsync(Guid id, UpdateProcessDeferredAccountingDto input)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSubmitted)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:CannotEditSubmitted", "Cannot edit a submitted Process Deferred Accounting.");
        }

        entity.CompanyId = input.CompanyId;
        entity.Type = input.Type;
        entity.AccountId = input.AccountId;
        entity.PostingDate = input.PostingDate.Date;
        entity.StartDate = input.StartDate.Date;
        entity.EndDate = input.EndDate.Date;
        entity.ValidateDates();

        await _repository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSubmitted)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:CannotDeleteSubmitted", "Cannot delete a submitted Process Deferred Accounting.");
        }

        await _repository.DeleteAsync(entity);
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Submit)]
    public async Task<ProcessDeferredAccountingDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSubmitted)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:AlreadySubmitted", "Process Deferred Accounting is already submitted.");
        }

        int processed = 0;
        if (entity.Type == DeferredAccountingType.Income)
        {
            processed = await _deferredAccountingService.ProcessDeferredRevenueAsync(entity.CompanyId, entity.EndDate, CurrentTenant.Id);
        }
        else
        {
            processed = await _deferredAccountingService.ProcessDeferredExpenseAsync(entity.CompanyId, entity.EndDate, CurrentTenant.Id);
        }

        entity.Submit(processed);
        await _repository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Cancel)]
    public async Task<ProcessDeferredAccountingDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }
}
