using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Shared;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.PaymentOrders.Default)]
public class PaymentOrderAppService : ApplicationService, IPaymentOrderAppService
{
    private readonly IRepository<PaymentOrder, Guid> _repository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<BankAccount, Guid> _bankAccountRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PaymentOrderAppService(
        IRepository<PaymentOrder, Guid> repository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<BankAccount, Guid> bankAccountRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
        _bankAccountRepository = bankAccountRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _journalEntryRepository = journalEntryRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PaymentOrderDto> GetAsync(Guid id)
    {
        var entity = (await _repository.WithDetailsAsync()).First(o => o.Id == id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<PaymentOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(o => o.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(o => o.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<PaymentOrderDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.PaymentOrders.Create)]
    public async Task<PaymentOrderDto> CreateAsync(CreatePaymentOrderDto input)
    {
        var orderNumber = await _numberGenerator.GenerateAsync("PaymentOrder", input.CompanyId, input.PostingDate);

        var entity = new PaymentOrder(GuidGenerator.Create(), input.CompanyId, input.PaymentOrderType,
            input.PostingDate, input.CompanyBankAccountId, CurrentTenant.Id)
        {
            OrderNumber = orderNumber,
            PartyId = input.PartyId,
        };

        foreach (var r in input.References)
        {
            entity.AddReference(r.ReferenceType, r.ReferenceId, r.Amount, r.SupplierId, r.ModeOfPayment, r.BankAccountId, r.PaymentReference);
        }

        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.PaymentOrders.Submit)]
    public async Task<PaymentOrderDto> SubmitAsync(Guid id)
    {
        var entity = (await _repository.WithDetailsAsync()).First(o => o.Id == id);
        entity.Submit();
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.PaymentOrders.Cancel)]
    public async Task<PaymentOrderDto> CancelAsync(Guid id)
    {
        var entity = (await _repository.WithDetailsAsync()).First(o => o.Id == id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.PaymentOrders.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    /// <summary>
    /// Batches all reference rows for one supplier into a single Journal Entry — the bank
    /// submission run. Mirrors ERPNext's payment_order.py make_payment_records/make_journal_entry:
    /// DR supplier's payable account per reference row (linked via PartyId), CR the company bank account
    /// for the total. Voucher type is Bank Entry unless the mode of payment is "Cash".
    /// </summary>
    [Authorize(MyERPPermissions.PaymentOrders.Submit)]
    public async Task<Guid> MakePaymentRecordsAsync(Guid id, MakePaymentRecordsDto input)
    {
        var order = (await _repository.WithDetailsAsync()).First(o => o.Id == id);
        var rows = order.ReferencesForSupplier(input.SupplierId).ToList();
        if (rows.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.PaymentOrderHasNoReferences);

        var supplier = await _supplierRepository.GetAsync(input.SupplierId);
        if (!supplier.DefaultPayableAccountId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.PartyNotAllowedOnAccount)
                .WithData("partyType", "Supplier").WithData("accountSubType", "AccountsPayable");

        var bankAccount = await _bankAccountRepository.GetAsync(order.CompanyBankAccountId);

        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fiscalYear = fyQuery.FirstOrDefault(f => f.CompanyId == order.CompanyId
            && f.StartDate <= order.PostingDate && f.EndDate >= order.PostingDate);
        if (fiscalYear == null)
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", order.PostingDate.ToString("yyyy-MM-dd"));

        var totalAmount = rows.Sum(r => r.Amount);
        var modeOfPayment = input.ModeOfPayment ?? rows.First().ModeOfPayment;
        var voucherType = string.Equals(modeOfPayment, "Cash", StringComparison.OrdinalIgnoreCase)
            ? JournalEntryVoucherType.CashEntry
            : JournalEntryVoucherType.BankEntry;

        var je = new JournalEntry(GuidGenerator.Create(), order.CompanyId, fiscalYear.Id, order.PostingDate, order.TenantId)
        {
            VoucherType = voucherType,
            ReferenceType = "PaymentOrder",
            ReferenceId = order.Id,
            ReferenceNumber = order.OrderNumber,
            Narration = $"Payment Order {order.OrderNumber} — {rows.Count} reference(s) for supplier",
        };

        foreach (var row in rows)
        {
            je.AddLineWithParty(supplier.DefaultPayableAccountId.Value, row.Amount, isDebit: true,
                partyId: input.SupplierId, partyType: "Supplier", accountSubType: AccountSubType.AccountsPayable,
                description: row.PaymentReference);
        }

        je.AddLine(bankAccount.AccountId, totalAmount, isDebit: false, description: $"Payment Order {order.OrderNumber}");

        je.Post();
        await _journalEntryRepository.InsertAsync(je);

        return je.Id;
    }

    private static PaymentOrderDto MapToDto(PaymentOrder entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        OrderNumber = entity.OrderNumber,
        PaymentOrderType = entity.PaymentOrderType,
        PostingDate = entity.PostingDate,
        PartyId = entity.PartyId,
        CompanyBankAccountId = entity.CompanyBankAccountId,
        Status = (int)entity.Status,
        AmendedFromId = entity.AmendedFromId,
        CreationTime = entity.CreationTime,
        LastModificationTime = entity.LastModificationTime,
        References = entity.References.Select(r => new PaymentOrderReferenceDto
        {
            Id = r.Id,
            ReferenceType = r.ReferenceType,
            ReferenceId = r.ReferenceId,
            Amount = r.Amount,
            SupplierId = r.SupplierId,
            ModeOfPayment = r.ModeOfPayment,
            BankAccountId = r.BankAccountId,
            PaymentReference = r.PaymentReference,
        }).ToList(),
    };
}
