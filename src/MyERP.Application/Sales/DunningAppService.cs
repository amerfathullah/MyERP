using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class DunningDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public DateTime PostingDate { get; set; }
    public int DunningLevel { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal DunningFee { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public int Status { get; set; }
    public int OverduePaymentCount { get; set; }
}

public class CreateDunningDto
{
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public DateTime PostingDate { get; set; }
    public int DunningLevel { get; set; } = 1;
    public decimal DunningFee { get; set; }
    public decimal InterestAmount { get; set; }
    public CreateDunningOverdueDto[] OverduePayments { get; set; } = [];
}

public class CreateDunningOverdueDto
{
    public Guid SalesInvoiceId { get; set; }
    public decimal OutstandingAmount { get; set; }
    public DateTime DueDate { get; set; }
    public int OverdueDays { get; set; }
}

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class DunningAppService : ApplicationService
{
    private readonly IRepository<Dunning, Guid> _repository;
    public DunningAppService(IRepository<Dunning, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<DunningDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderByDescending(d => d.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<DunningDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<DunningDto> GetAsync(Guid id)
    {
        var d = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        return MapToDto(d);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<DunningDto> CreateAsync(CreateDunningDto input)
    {
        var d = new Dunning(GuidGenerator.Create(), input.CompanyId, input.CustomerId,
            input.PostingDate, input.DunningLevel, CurrentTenant.Id)
        { CustomerName = input.CustomerName, DunningFee = input.DunningFee, InterestAmount = input.InterestAmount };
        foreach (var p in input.OverduePayments)
            d.AddOverduePayment(p.SalesInvoiceId, p.OutstandingAmount, p.DueDate, p.OverdueDays);
        await _repository.InsertAsync(d);
        return MapToDto(d);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<DunningDto> SubmitAsync(Guid id)
    {
        var d = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        d.Submit();
        await _repository.UpdateAsync(d);
        return MapToDto(d);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<DunningDto> ResolveAsync(Guid id)
    {
        var d = await _repository.GetAsync(id);
        d.Resolve();
        await _repository.UpdateAsync(d);
        return MapToDto(d);
    }

    private static DunningDto MapToDto(Dunning d) => new()
    {
        Id = d.Id, CompanyId = d.CompanyId, CustomerId = d.CustomerId,
        CustomerName = d.CustomerName, PostingDate = d.PostingDate,
        DunningLevel = d.DunningLevel, TotalOutstanding = d.TotalOutstanding,
        DunningFee = d.DunningFee, InterestAmount = d.InterestAmount,
        GrandTotal = d.GrandTotal, Status = (int)d.Status,
        OverduePaymentCount = d.OverduePayments.Count,
    };
}
