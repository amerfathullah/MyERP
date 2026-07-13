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

public class PeriodClosingVoucherDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public string? VoucherNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime TransactionDate { get; set; }
    public Guid ClosingAccountId { get; set; }
    public decimal TotalClosingAmount { get; set; }
    public int Status { get; set; }
    public string? Remarks { get; set; }
    public int EntryCount { get; set; }
}

public class CreatePeriodClosingVoucherDto
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime TransactionDate { get; set; }
    public Guid ClosingAccountId { get; set; }
    public string? Remarks { get; set; }
}

[Authorize(MyERPPermissions.Accounts.Default)]
public class PeriodClosingVoucherAppService : ApplicationService
{
    private readonly IRepository<PeriodClosingVoucher, Guid> _repository;
    public PeriodClosingVoucherAppService(IRepository<PeriodClosingVoucher, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<PeriodClosingVoucherDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PeriodClosingVoucherDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<PeriodClosingVoucherDto> GetAsync(Guid id)
    {
        var pcv = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        return MapToDto(pcv);
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<PeriodClosingVoucherDto> CreateAsync(CreatePeriodClosingVoucherDto input)
    {
        var pcv = new PeriodClosingVoucher(GuidGenerator.Create(), input.CompanyId,
            input.FiscalYearId, input.PostingDate, input.TransactionDate,
            input.ClosingAccountId, CurrentTenant.Id)
        { Remarks = input.Remarks };
        await _repository.InsertAsync(pcv);
        return MapToDto(pcv);
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<PeriodClosingVoucherDto> SubmitAsync(Guid id)
    {
        var pcv = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        pcv.Submit();
        await _repository.UpdateAsync(pcv);
        return MapToDto(pcv);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task<PeriodClosingVoucherDto> CancelAsync(Guid id)
    {
        var pcv = await _repository.GetAsync(id);
        pcv.Cancel();
        await _repository.UpdateAsync(pcv);
        return MapToDto(pcv);
    }

    private static PeriodClosingVoucherDto MapToDto(PeriodClosingVoucher p) => new()
    {
        Id = p.Id, CompanyId = p.CompanyId, FiscalYearId = p.FiscalYearId,
        VoucherNumber = p.VoucherNumber, PostingDate = p.PostingDate,
        TransactionDate = p.TransactionDate, ClosingAccountId = p.ClosingAccountId,
        TotalClosingAmount = p.TotalClosingAmount, Status = (int)p.Status,
        Remarks = p.Remarks, EntryCount = p.Entries.Count,
    };
}
