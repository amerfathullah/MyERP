using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Unlinks a Payment Entry or Journal Entry from the invoices/orders it settled, by delinking
/// the Payment Ledger Entry rows created for it. Since Outstanding = SUM(PLE where !Delinked),
/// setting Delinked=true is sufficient to restore the target document's outstanding balance —
/// no separate cache recompute is needed (see PaymentLedgerEntry doc comment).
/// </summary>
[Authorize(MyERPPermissions.UnreconcilePayments.Default)]
public class UnreconcilePaymentAppService : ApplicationService, IUnreconcilePaymentAppService
{
    private readonly IRepository<UnreconcilePayment, Guid> _repository;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly DomainServices.DocumentPostingOrchestrator _postingOrchestrator;

    public UnreconcilePaymentAppService(
        IRepository<UnreconcilePayment, Guid> repository,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        DomainServices.DocumentPostingOrchestrator postingOrchestrator)
    {
        _repository = repository;
        _pleRepository = pleRepository;
        _postingOrchestrator = postingOrchestrator;
    }

    public async Task<UnreconcilePaymentDto> GetAsync(Guid id)
    {
        var entity = (await _repository.WithDetailsAsync()).First(u => u.Id == id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<UnreconcilePaymentDto>> GetListAsync(GetUnreconcilePaymentListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(u => u.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(u => u.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<UnreconcilePaymentDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.UnreconcilePayments.Create)]
    public async Task<UnreconcilePaymentDto> CreateAsync(CreateUnreconcilePaymentDto input)
    {
        var voucherTypeName = input.VoucherType.ToString(); // "PaymentEntry" or "JournalEntry"

        var pleQuery = await _pleRepository.GetQueryableAsync();
        var linkedEntries = pleQuery
            .Where(p => p.CompanyId == input.CompanyId && p.VoucherType == voucherTypeName
                && p.VoucherId == input.VoucherId && !p.Delinked)
            .ToList();

        if (linkedEntries.Count == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        var entity = new UnreconcilePayment(GuidGenerator.Create(), input.CompanyId, input.VoucherType, input.VoucherId, CurrentTenant.Id);

        foreach (var ple in linkedEntries)
        {
            entity.AddAllocation(ple.Id, ple.AgainstVoucherType, ple.AgainstVoucherId, ple.AmountInAccountCurrency);
        }

        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "UnreconcilePayment", entity.Id,
            "Created", entity.CompanyId,
            entity.VoucherType.ToString(), "Draft", "Draft",
            CurrentUser.Id,
            $"Unreconcile payment created for {entity.VoucherType} with {entity.Allocations.Count} allocations", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.UnreconcilePayments.Submit)]
    public async Task<UnreconcilePaymentDto> SubmitAsync(Guid id)
    {
        var entity = (await _repository.WithDetailsAsync()).First(u => u.Id == id);
        entity.Submit();

        foreach (var allocation in entity.Allocations)
        {
            var ple = await _pleRepository.GetAsync(allocation.PaymentLedgerEntryId);
            ple.Delinked = true;
            await _pleRepository.UpdateAsync(ple);
            allocation.Unlinked = true;
        }

        // Cancel any exchange gain/loss JE(s) posted for this voucher during the reconciliation(s)
        // being undone — otherwise they stay posted forever with no reconciliation left to justify
        // them. Reverses only the FX JE(s), never the voucher's own main GL (which is untouched —
        // unreconcile undoes an allocation, not the payment itself).
        var voucherTypeName = entity.VoucherType.ToString();
        await _postingOrchestrator.ReverseExchangeGainLossJournalEntriesAsync(voucherTypeName, entity.VoucherId);

        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "UnreconcilePayment", entity.Id,
            "Submitted", entity.CompanyId,
            entity.VoucherType.ToString(), "Draft", "Submitted",
            CurrentUser.Id,
            $"Unreconcile payment submitted for {entity.VoucherType}", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.UnreconcilePayments.Cancel)]
    public async Task<UnreconcilePaymentDto> CancelAsync(Guid id)
    {
        var entity = (await _repository.WithDetailsAsync()).First(u => u.Id == id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "UnreconcilePayment", entity.Id,
            "Cancelled", entity.CompanyId,
            entity.VoucherType.ToString(), "Submitted", "Cancelled",
            CurrentUser.Id,
            $"Unreconcile payment cancelled for {entity.VoucherType}", CurrentTenant.Id));

        return MapToDto(entity);
    }

    private static UnreconcilePaymentDto MapToDto(UnreconcilePayment entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        VoucherType = entity.VoucherType,
        VoucherId = entity.VoucherId,
        Status = (int)entity.Status,
        CreationTime = entity.CreationTime,
        LastModificationTime = entity.LastModificationTime,
        Allocations = entity.Allocations.Select(a => new UnreconcilePaymentAllocationDto
        {
            Id = a.Id,
            PaymentLedgerEntryId = a.PaymentLedgerEntryId,
            AgainstVoucherType = a.AgainstVoucherType,
            AgainstVoucherId = a.AgainstVoucherId,
            Amount = a.Amount,
            Unlinked = a.Unlinked,
        }).ToList(),
    };
}
