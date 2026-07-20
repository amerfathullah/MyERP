using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class PaymentRequestDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string PaymentRequestType { get; set; } = null!;
    public string ReferenceDoctype { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = null!;
    public string? PartyName { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public int Status { get; set; }
    public Guid? PaymentEntryId { get; set; }
}

public class CreatePaymentRequestDto
{
    public Guid CompanyId { get; set; }
    public string PaymentRequestType { get; set; } = "Inward";
    public string ReferenceDoctype { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = "Customer";
    public string? PartyName { get; set; }
    public decimal GrandTotal { get; set; }
    public string Currency { get; set; } = "MYR";
    public string? EmailTo { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }
}

[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class PaymentRequestAppService : ApplicationService
{
    private readonly IRepository<PaymentRequest, Guid> _repository;
    public PaymentRequestAppService(IRepository<PaymentRequest, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<PaymentRequestDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
             query = query.Where(x => x.PartyName != null && x.PartyName.ToLower().Contains(filter.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<PaymentRequestStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PaymentRequestDto>(totalCount, items.Select(x => ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<PaymentRequestDto> CreateAsync(CreatePaymentRequestDto input)
    {
        var pr = new PaymentRequest(GuidGenerator.Create(), input.CompanyId,
            input.ReferenceDoctype, input.ReferenceId, input.PartyId, input.PartyType,
            input.GrandTotal, CurrentTenant.Id)
        {
            PartyName = input.PartyName, Currency = input.Currency,
            EmailTo = input.EmailTo, Subject = input.Subject, Message = input.Message,
        };
        await _repository.InsertAsync(pr);
        return ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(pr);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<PaymentRequestDto> SubmitAsync(Guid id)
    {
        var pr = await _repository.GetAsync(id);
        pr.Submit();
        await _repository.UpdateAsync(pr);
        return ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(pr);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<PaymentRequestDto> CancelAsync(Guid id)
    {
        var pr = await _repository.GetAsync(id);
        pr.Cancel();
        await _repository.UpdateAsync(pr);
        return ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(pr);
    }
}

