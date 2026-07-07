using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.JournalEntries.Default)]
public class PaymentEntryAppService : ApplicationService
{
    private readonly IRepository<PaymentEntry, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PaymentEntryAppService(
        IRepository<PaymentEntry, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PaymentEntryDto> GetAsync(Guid id)
    {
        var pe = await _repository.GetAsync(id);
        return MapToDto(pe);
    }

    public async Task<PagedResultDto<PaymentEntryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var count = await _repository.GetCountAsync();
        var list = await _repository.GetPagedListAsync(input.SkipCount, input.MaxResultCount, input.Sorting ?? "PostingDate DESC");
        return new PagedResultDto<PaymentEntryDto>(count, list.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.JournalEntries.Create)]
    public async Task<PaymentEntryDto> CreateAsync(CreatePaymentEntryDto input)
    {
        var paymentNumber = await _numberGenerator.GenerateAsync("PaymentEntry", input.CompanyId);
        var pe = new PaymentEntry(
            GuidGenerator.Create(), input.CompanyId, input.PaymentType, input.PostingDate,
            input.PaidAmount, input.PaidFromAccountId, input.PaidToAccountId);

        pe.PaymentNumber = paymentNumber;
        pe.ModeOfPayment = input.ModeOfPayment;
        pe.PartyType = input.PartyType;
        pe.PartyId = input.PartyId;
        pe.ReferenceNumber = input.ReferenceNumber;
        pe.Notes = input.Notes;
        pe.AgainstInvoiceId = input.AgainstInvoiceId;
        pe.AgainstInvoiceType = input.AgainstInvoiceType;

        await _repository.InsertAsync(pe, autoSave: true);
        return MapToDto(pe);
    }

    public async Task<PaymentEntryDto> SubmitAsync(Guid id)
    {
        var pe = await _repository.GetAsync(id);
        pe.Submit();
        await _repository.UpdateAsync(pe, autoSave: true);
        return MapToDto(pe);
    }

    public async Task<PaymentEntryDto> PostAsync(Guid id)
    {
        var pe = await _repository.GetAsync(id);
        pe.Post();
        await _repository.UpdateAsync(pe, autoSave: true);
        return MapToDto(pe);
    }

    private static PaymentEntryDto MapToDto(PaymentEntry pe) => new()
    {
        Id = pe.Id,
        CompanyId = pe.CompanyId,
        PaymentNumber = pe.PaymentNumber,
        PaymentType = pe.PaymentType.ToString(),
        PostingDate = pe.PostingDate,
        ModeOfPayment = pe.ModeOfPayment,
        PaidAmount = pe.PaidAmount,
        CurrencyCode = pe.CurrencyCode,
        Status = pe.Status.ToString(),
        ReferenceNumber = pe.ReferenceNumber
    };
}
