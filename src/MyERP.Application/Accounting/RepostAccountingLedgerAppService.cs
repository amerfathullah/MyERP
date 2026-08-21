using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.BackgroundJobs;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Inventory.Entities;
using MyERP.Shared;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Admin tool: rebuilds GL/PLE for a batch of already-Posted vouchers from their current field
/// values. See <see cref="Entities.RepostAccountingLedger"/> for the lifecycle and
/// <see cref="RepostAllowedVoucherTypes"/> for which voucher types are supported.
/// </summary>
[Authorize(MyERPPermissions.RepostAccountingLedger.Default)]
public class RepostAccountingLedgerAppService : ApplicationService, IRepostAccountingLedgerAppService
{
    private readonly IRepository<Entities.RepostAccountingLedger, Guid> _repository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly IBackgroundJobManager _jobManager;

    public RepostAccountingLedgerAppService(
        IRepository<Entities.RepostAccountingLedger, Guid> repository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        DocumentPostingOrchestrator postingOrchestrator,
        IBackgroundJobManager jobManager)
    {
        _repository = repository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _stockEntryRepository = stockEntryRepository;
        _postingOrchestrator = postingOrchestrator;
        _jobManager = jobManager;
    }

    public async Task<PagedResultDto<RepostAccountingLedgerDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.WithDetailsAsync(x => x.Vouchers);
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        var count = query.Count();
        var items = query.OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<RepostAccountingLedgerDto>(count, items.Select(ToDto).ToList());
    }

    public async Task<RepostAccountingLedgerDto> GetAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(x => x.Vouchers);
        var entity = query.First(x => x.Id == id);
        return ToDto(entity);
    }

    public Task<List<string>> GetAllowedVoucherTypesAsync() => Task.FromResult(RepostAllowedVoucherTypes.Values.ToList());

    public async Task<RepostableVoucherDto> ResolveVoucherAsync(string voucherType, string voucherNumber)
    {
        if (!RepostAllowedVoucherTypes.IsAllowed(voucherType))
            throw new BusinessException(MyERPDomainErrorCodes.RepostVoucherTypeNotAllowed)
                .WithData("voucherType", voucherType);

        switch (voucherType)
        {
            case "SalesInvoice":
            {
                var query = await _salesInvoiceRepository.WithDetailsAsync(x => x.Items);
                var invoice = query.FirstOrDefault(x => x.InvoiceNumber == voucherNumber);
                RequirePosted(invoice?.Status, voucherType, voucherNumber);
                RequireNoDeferredAccounting(invoice!.Items.Any(i => i.EnableDeferredRevenue), voucherType, voucherNumber);
                return new RepostableVoucherDto { VoucherType = voucherType, VoucherId = invoice.Id, VoucherNumber = invoice.InvoiceNumber, PostingDate = invoice.IssueDate };
            }
            case "PurchaseInvoice":
            {
                var query = await _purchaseInvoiceRepository.WithDetailsAsync(x => x.Items);
                var invoice = query.FirstOrDefault(x => x.InvoiceNumber == voucherNumber);
                RequirePosted(invoice?.Status, voucherType, voucherNumber);
                RequireNoDeferredAccounting(invoice!.Items.Any(i => i.EnableDeferredExpense), voucherType, voucherNumber);
                return new RepostableVoucherDto { VoucherType = voucherType, VoucherId = invoice.Id, VoucherNumber = invoice.InvoiceNumber, PostingDate = invoice.IssueDate };
            }
            case "StockEntry":
            {
                var query = await _stockEntryRepository.GetQueryableAsync();
                var entry = query.FirstOrDefault(x => x.EntryNumber == voucherNumber);
                RequirePosted(entry?.Status, voucherType, voucherNumber);
                return new RepostableVoucherDto { VoucherType = voucherType, VoucherId = entry!.Id, VoucherNumber = entry.EntryNumber!, PostingDate = entry.PostingDate };
            }
            default:
                throw new BusinessException(MyERPDomainErrorCodes.RepostVoucherTypeNotAllowed)
                    .WithData("voucherType", voucherType);
        }
    }

    private static void RequirePosted(DocumentStatus? status, string voucherType, string voucherNumber)
    {
        if (status != DocumentStatus.Posted)
            throw new BusinessException(MyERPDomainErrorCodes.RepostVoucherNotPosted)
                .WithData("voucherType", voucherType)
                .WithData("voucherNo", voucherNumber);
    }

    private static void RequireNoDeferredAccounting(bool hasDeferred, string voucherType, string voucherNumber)
    {
        if (hasDeferred)
            throw new BusinessException(MyERPDomainErrorCodes.RepostVoucherHasDeferredAccounting)
                .WithData("voucherType", voucherType)
                .WithData("voucherNo", voucherNumber);
    }

    [Authorize(MyERPPermissions.RepostAccountingLedger.Create)]
    public async Task<RepostAccountingLedgerDto> CreateAsync(CreateRepostAccountingLedgerDto input)
    {
        var entity = new Entities.RepostAccountingLedger(GuidGenerator.Create(), input.CompanyId, CurrentTenant.Id);

        var vouchers = new List<RepostAccountingLedgerVoucher>();
        foreach (var v in input.Vouchers)
        {
            var resolved = await ResolveVoucherByIdAsync(v.VoucherType, v.VoucherId);
            vouchers.Add(new RepostAccountingLedgerVoucher(
                GuidGenerator.Create(), entity.Id, resolved.VoucherType, resolved.VoucherId, resolved.VoucherNumber));
        }
        entity.SetVouchers(vouchers);

        await _repository.InsertAsync(entity);
        return ToDto(entity);
    }

    private async Task<RepostableVoucherDto> ResolveVoucherByIdAsync(string voucherType, Guid voucherId)
    {
        if (!RepostAllowedVoucherTypes.IsAllowed(voucherType))
            throw new BusinessException(MyERPDomainErrorCodes.RepostVoucherTypeNotAllowed)
                .WithData("voucherType", voucherType);

        switch (voucherType)
        {
            case "SalesInvoice":
            {
                var query = await _salesInvoiceRepository.WithDetailsAsync(x => x.Items);
                var invoice = query.FirstOrDefault(x => x.Id == voucherId);
                RequirePosted(invoice?.Status, voucherType, voucherId.ToString());
                RequireNoDeferredAccounting(invoice!.Items.Any(i => i.EnableDeferredRevenue), voucherType, invoice.InvoiceNumber);
                return new RepostableVoucherDto { VoucherType = voucherType, VoucherId = invoice.Id, VoucherNumber = invoice.InvoiceNumber, PostingDate = invoice.IssueDate };
            }
            case "PurchaseInvoice":
            {
                var query = await _purchaseInvoiceRepository.WithDetailsAsync(x => x.Items);
                var invoice = query.FirstOrDefault(x => x.Id == voucherId);
                RequirePosted(invoice?.Status, voucherType, voucherId.ToString());
                RequireNoDeferredAccounting(invoice!.Items.Any(i => i.EnableDeferredExpense), voucherType, invoice.InvoiceNumber);
                return new RepostableVoucherDto { VoucherType = voucherType, VoucherId = invoice.Id, VoucherNumber = invoice.InvoiceNumber, PostingDate = invoice.IssueDate };
            }
            case "StockEntry":
            {
                var query = await _stockEntryRepository.GetQueryableAsync();
                var entry = query.FirstOrDefault(x => x.Id == voucherId);
                RequirePosted(entry?.Status, voucherType, voucherId.ToString());
                return new RepostableVoucherDto { VoucherType = voucherType, VoucherId = entry!.Id, VoucherNumber = entry.EntryNumber!, PostingDate = entry.PostingDate };
            }
            default:
                throw new BusinessException(MyERPDomainErrorCodes.RepostVoucherTypeNotAllowed)
                    .WithData("voucherType", voucherType);
        }
    }

    [Authorize(MyERPPermissions.RepostAccountingLedger.Submit)]
    public async Task<RepostAccountingLedgerDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);

        // Preconditions a repost queued earlier could have outlived — re-checked here rather than
        // only at create time, per ERPNext's validate_repost_preconditions.
        foreach (var voucher in entity.Vouchers)
        {
            var resolved = await ResolveVoucherByIdAsync(voucher.VoucherType, voucher.VoucherId);
            await _postingOrchestrator.ValidatePostingPeriodAsync(entity.CompanyId, resolved.PostingDate, voucher.VoucherType);
        }

        entity.Submit();
        await _repository.UpdateAsync(entity, autoSave: true);

        await _jobManager.EnqueueAsync(new RepostAccountingLedgerJobArgs
        {
            RepostId = entity.Id,
            TenantId = entity.TenantId,
        });

        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.RepostAccountingLedger.Cancel)]
    public async Task<RepostAccountingLedgerDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity, autoSave: true);
        return ToDto(entity);
    }

    private static RepostAccountingLedgerDto ToDto(Entities.RepostAccountingLedger entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        Status = (int)entity.Status,
        StatusName = entity.Status.ToString(),
        ErrorLog = entity.ErrorLog,
        CreationTime = entity.CreationTime,
        Vouchers = entity.Vouchers.Select(v => new RepostAccountingLedgerVoucherDto
        {
            Id = v.Id,
            VoucherType = v.VoucherType,
            VoucherId = v.VoucherId,
            VoucherNumber = v.VoucherNumber,
            Status = (int)v.Status,
            StatusName = v.Status.ToString(),
            ErrorMessage = v.ErrorMessage,
        }).ToList(),
    };
}
