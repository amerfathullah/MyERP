using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

// --- Accounting Period ---
public class AccountingPeriodDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string PeriodName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}

[Authorize(MyERPPermissions.Accounts.Default)]
public class AccountingPeriodAppService : ApplicationService
{
    private readonly IRepository<AccountingPeriod, Guid> _repository;
    public AccountingPeriodAppService(IRepository<AccountingPeriod, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<AccountingPeriodDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(a => a.StartDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<AccountingPeriodDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<AccountingPeriodDto> CloseAsync(Guid id)
    {
        var ap = await _repository.GetAsync(id);
        ap.Close();
        await _repository.UpdateAsync(ap);
        return MapToDto(ap);
    }

    private static AccountingPeriodDto MapToDto(AccountingPeriod a) => new()
    {
        Id = a.Id, CompanyId = a.CompanyId, PeriodName = a.PeriodName,
        StartDate = a.StartDate, EndDate = a.EndDate, IsClosed = a.IsClosed,
    };
}

// --- Mode of Payment ---
public class ModeOfPaymentDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
}

[Authorize(MyERPPermissions.Accounts.Default)]
public class ModeOfPaymentAppService : ApplicationService
{
    private readonly IRepository<ModeOfPayment, Guid> _repository;
    public ModeOfPaymentAppService(IRepository<ModeOfPayment, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ModeOfPaymentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(m => m.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ModeOfPaymentDto>(totalCount, items.Select(m => new ModeOfPaymentDto
        {
            Id = m.Id, Name = m.Name, Type = m.Type,
        }).ToList());
    }
}

// --- UOM Conversion ---
public class UomConversionDto : EntityDto<Guid>
{
    public string FromUom { get; set; } = null!;
    public string ToUom { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
    public Guid? ItemId { get; set; }
}

[Authorize(MyERPPermissions.Items.Default)]
public class UomConversionAppService : ApplicationService
{
    private readonly IRepository<Inventory.Entities.UomConversion, Guid> _repository;
    public UomConversionAppService(IRepository<Inventory.Entities.UomConversion, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<UomConversionDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(u => u.FromUom).ThenBy(u => u.ToUom)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<UomConversionDto>(totalCount, items.Select(u => new UomConversionDto
        {
            Id = u.Id, FromUom = u.FromUom, ToUom = u.ToUom,
            ConversionFactor = u.ConversionFactor, ItemId = u.ItemId,
        }).ToList());
    }
}
