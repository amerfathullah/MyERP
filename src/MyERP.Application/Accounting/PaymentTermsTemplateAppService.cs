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

public class PaymentTermsTemplateDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public PaymentTermDto[] Terms { get; set; } = [];
}

public class PaymentTermDto
{
    public Guid Id { get; set; }
    public decimal InvoicePortion { get; set; }
    public int CreditDays { get; set; }
    public string? Description { get; set; }
    public Guid? ModeOfPaymentId { get; set; }
}

public class CreateUpdatePaymentTermsTemplateDto
{
    public string Name { get; set; } = null!;
    public CreatePaymentTermDto[] Terms { get; set; } = [];
}

public class CreatePaymentTermDto
{
    public decimal InvoicePortion { get; set; }
    public int CreditDays { get; set; }
    public string? Description { get; set; }
    public Guid? ModeOfPaymentId { get; set; }
}

[Authorize(MyERPPermissions.Accounts.Default)]
public class PaymentTermsTemplateAppService : ApplicationService
{
    private readonly IRepository<PaymentTermsTemplate, Guid> _repository;

    public PaymentTermsTemplateAppService(IRepository<PaymentTermsTemplate, Guid> repository)
        => _repository = repository;

    public async Task<PagedResultDto<PaymentTermsTemplateDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(t => t.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PaymentTermsTemplateDto>(totalCount,
            items.Select(ObjectMapper.Map<PaymentTermsTemplate, PaymentTermsTemplateDto>).ToList());
    }

    public async Task<PaymentTermsTemplateDto> GetAsync(Guid id)
        => ObjectMapper.Map<PaymentTermsTemplate, PaymentTermsTemplateDto>(await _repository.GetAsync(id));

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<PaymentTermsTemplateDto> CreateAsync(CreateUpdatePaymentTermsTemplateDto input)
    {
        var template = new PaymentTermsTemplate(GuidGenerator.Create(), input.Name, CurrentTenant.Id);
        foreach (var term in input.Terms)
        {
            template.AddTerm(new PaymentTerm(GuidGenerator.Create(), template.Id,
                term.InvoicePortion, term.CreditDays, term.Description)
            { ModeOfPaymentId = term.ModeOfPaymentId });
        }
        template.ValidatePortions();
        await _repository.InsertAsync(template);
        return ObjectMapper.Map<PaymentTermsTemplate, PaymentTermsTemplateDto>(template);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<PaymentTermsTemplateDto> UpdateAsync(Guid id, CreateUpdatePaymentTermsTemplateDto input)
    {
        var template = await _repository.GetAsync(id);
        template.Name = input.Name;
        // Terms is IReadOnlyList backed by private List — re-create via delete+insert
        await _repository.DeleteAsync(template.Id);
        var updated = new PaymentTermsTemplate(template.Id, input.Name, template.TenantId) { IsActive = template.IsActive };
        foreach (var term in input.Terms)
        {
            updated.AddTerm(new PaymentTerm(GuidGenerator.Create(), updated.Id,
                term.InvoicePortion, term.CreditDays, term.Description)
            { ModeOfPaymentId = term.ModeOfPaymentId });
        }
        updated.ValidatePortions();
        await _repository.InsertAsync(updated);
        return ObjectMapper.Map<PaymentTermsTemplate, PaymentTermsTemplateDto>(updated);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
