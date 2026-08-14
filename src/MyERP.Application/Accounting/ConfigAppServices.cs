using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

// --- Accounting Period ---
[Authorize(MyERPPermissions.Accounts.Default)]
public class AccountingPeriodAppService : ApplicationService, IAccountingPeriodAppService
{
    private readonly IRepository<AccountingPeriod, Guid> _repository;
    public AccountingPeriodAppService(IRepository<AccountingPeriod, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<AccountingPeriodDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(a => a.StartDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<AccountingPeriodDto>(totalCount, items.Select(ObjectMapper.Map<AccountingPeriod, AccountingPeriodDto>).ToList());
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<AccountingPeriodDto> CloseAsync(Guid id)
    {
        var ap = await _repository.GetAsync(id);
        ap.Close();
        await _repository.UpdateAsync(ap);
        return ObjectMapper.Map<AccountingPeriod, AccountingPeriodDto>(ap);
    }
}

// --- Mode of Payment ---
[Authorize(MyERPPermissions.Accounts.Default)]
public class ModeOfPaymentAppService : ApplicationService, IModeOfPaymentAppService
{
    private readonly IRepository<ModeOfPayment, Guid> _repository;
    public ModeOfPaymentAppService(IRepository<ModeOfPayment, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ModeOfPaymentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(m => m.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ModeOfPaymentDto>(totalCount, items.Select(ObjectMapper.Map<ModeOfPayment, ModeOfPaymentDto>).ToList());
    }
}

// --- UOM Conversion ---
[Authorize(MyERPPermissions.Items.Default)]
public class UomConversionAppService : ApplicationService, IUomConversionAppService
{
    private readonly IRepository<Inventory.Entities.UomConversion, Guid> _repository;
    public UomConversionAppService(IRepository<Inventory.Entities.UomConversion, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<UomConversionDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(u => u.FromUom).ThenBy(u => u.ToUom)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<UomConversionDto>(totalCount, items.Select(ObjectMapper.Map<Inventory.Entities.UomConversion, UomConversionDto>).ToList());
    }
}
