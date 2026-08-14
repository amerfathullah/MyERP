using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Exposes GL repost operations via API for manual admin-triggered reposts.
/// The GlRepostService domain service handles the actual GL rebuild logic.
/// Per ERPNext: allowed for SI, PI, PE, JE, PR, DN, SE voucher types.
/// </summary>
[Authorize(MyERPPermissions.Accounts.Edit)]
public class GlRepostAppService : ApplicationService, IGlRepostAppService
{
    private readonly GlRepostService _glRepostService;
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly IRepository<PaymentEntry, Guid> _peRepository;
    private readonly IRepository<JournalEntry, Guid> _jeRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _prRepository;
    private readonly IRepository<DeliveryNote, Guid> _dnRepository;
    private readonly IRepository<StockEntry, Guid> _seRepository;

    public GlRepostAppService(
        GlRepostService glRepostService,
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<PurchaseInvoice, Guid> piRepository,
        IRepository<PaymentEntry, Guid> peRepository,
        IRepository<JournalEntry, Guid> jeRepository,
        IRepository<PurchaseReceipt, Guid> prRepository,
        IRepository<DeliveryNote, Guid> dnRepository,
        IRepository<StockEntry, Guid> seRepository)
    {
        _glRepostService = glRepostService;
        _siRepository = siRepository;
        _piRepository = piRepository;
        _peRepository = peRepository;
        _jeRepository = jeRepository;
        _prRepository = prRepository;
        _dnRepository = dnRepository;
        _seRepository = seRepository;
    }

    /// <summary>
    /// Reposts GL entries for a single voucher. Deletes existing GL and regenerates.
    /// </summary>
    public async Task<GlRepostResultDto> RepostAsync(RepostGlDto input)
    {
        var document = await ResolveDocumentAsync(input.VoucherType, input.VoucherId);
        if (document == null)
            return new GlRepostResultDto
            {
                SkippedCount = 1, TotalProcessed = 1,
                Errors = [$"Document not found: {input.VoucherType}/{input.VoucherId}"]
            };

        var result = await _glRepostService.RepostForVoucherAsync(
            input.CompanyId, input.VoucherType, input.VoucherId, document);

        return new GlRepostResultDto
        {
            SuccessCount = result != null ? 1 : 0,
            SkippedCount = result == null ? 1 : 0,
            TotalProcessed = 1
        };
    }

    /// <summary>
    /// Batch reposts GL entries for multiple vouchers.
    /// Per ERPNext: processes in posting_date order, per-voucher error isolation.
    /// </summary>
    public async Task<GlRepostResultDto> RepostBatchAsync(RepostBatchGlDto input)
    {
        var resolvedVouchers = new List<(string VoucherType, Guid VoucherId, IAccountableDocument Document)>();

        foreach (var v in input.Vouchers)
        {
            var doc = await ResolveDocumentAsync(v.VoucherType, v.VoucherId);
            if (doc != null)
                resolvedVouchers.Add((v.VoucherType, v.VoucherId, doc));
        }

        if (!resolvedVouchers.Any())
            return new GlRepostResultDto { SkippedCount = input.Vouchers.Count, TotalProcessed = input.Vouchers.Count };

        var result = await _glRepostService.RepostBatchAsync(input.CompanyId, resolvedVouchers);

        return new GlRepostResultDto
        {
            SuccessCount = result.SuccessCount,
            SkippedCount = result.SkippedCount + (input.Vouchers.Count - resolvedVouchers.Count),
            FailedCount = result.FailedCount,
            TotalProcessed = result.TotalProcessed + (input.Vouchers.Count - resolvedVouchers.Count),
            HasErrors = result.HasErrors,
            Errors = result.Errors
        };
    }

    /// <summary>
    /// Checks if a voucher type is eligible for GL repost.
    /// </summary>
    [Authorize(MyERPPermissions.Accounts.Default)]
    public Task<bool> IsRepostAllowedAsync(string voucherType)
        => Task.FromResult(GlRepostService.IsRepostAllowed(voucherType));

    /// <summary>
    /// Gets the list of allowed voucher types for GL repost.
    /// </summary>
    [Authorize(MyERPPermissions.Accounts.Default)]
    public Task<List<string>> GetAllowedVoucherTypesAsync()
        => Task.FromResult(GlRepostService.AllowedVoucherTypes.ToList());

    private async Task<IAccountableDocument?> ResolveDocumentAsync(string voucherType, Guid voucherId)
    {
        try
        {
            return voucherType switch
            {
                "SalesInvoice" => await _siRepository.FindAsync(voucherId),
                "PurchaseInvoice" => await _piRepository.FindAsync(voucherId),
                "PaymentEntry" => await _peRepository.FindAsync(voucherId),
                "JournalEntry" => null, // JE posts itself, no IAccountableDocument
                "PurchaseReceipt" => await _prRepository.FindAsync(voucherId),
                "DeliveryNote" => await _dnRepository.FindAsync(voucherId),
                "StockEntry" => await _seRepository.FindAsync(voucherId),
                _ => null
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to resolve document {VoucherType}/{VoucherId}", voucherType, voucherId);
            return null;
        }
    }
}
