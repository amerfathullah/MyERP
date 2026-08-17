using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Orchestrates the share-balance side effects of submitting/cancelling a Share Transfer,
/// spanning up to two Shareholder aggregates (and the company's own treasury shareholder).
/// Per ERPNext share_transfer.py on_submit / on_cancel.
/// </summary>
public class ShareTransferService : DomainService
{
    private readonly IRepository<Shareholder, Guid> _shareholderRepository;

    public ShareTransferService(IRepository<Shareholder, Guid> shareholderRepository)
    {
        _shareholderRepository = shareholderRepository;
    }

    /// <summary>Returns the company's treasury Shareholder, auto-creating it on first use.</summary>
    public async Task<Shareholder> GetOrCreateCompanyShareholderAsync(Guid companyId, string companyTitle, Guid? tenantId)
    {
        var query = await _shareholderRepository.GetQueryableAsync();
        var existing = query.FirstOrDefault(s => s.CompanyId == companyId && s.IsCompany);
        if (existing != null) return existing;

        var shareholder = new Shareholder(Guid.NewGuid(), companyId, companyTitle, isCompany: true, tenantId: tenantId);
        await _shareholderRepository.InsertAsync(shareholder);
        return shareholder;
    }

    public async Task SubmitAsync(ShareTransfer transfer, string companyTitle)
    {
        transfer.Validate();

        switch (transfer.TransferType)
        {
            case ShareTransferType.Issue:
            {
                var companyHolder = await GetOrCreateCompanyShareholderAsync(transfer.CompanyId, companyTitle, transfer.TenantId);
                ValidateNotAlreadyIssued(companyHolder, transfer);
                var toHolder = await _shareholderRepository.GetAsync(transfer.ToShareholderId!.Value);

                companyHolder.AddShareBalance(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo, transfer.Rate, isCompany: true, currentState: "Issued");
                toHolder.AddShareBalance(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo, transfer.Rate);

                await _shareholderRepository.UpdateAsync(companyHolder);
                await _shareholderRepository.UpdateAsync(toHolder);
                break;
            }
            case ShareTransferType.Purchase:
            {
                var fromHolder = await _shareholderRepository.GetAsync(transfer.FromShareholderId!.Value);
                ValidateSharesHeld(fromHolder, transfer);
                var companyHolder = await GetOrCreateCompanyShareholderAsync(transfer.CompanyId, companyTitle, transfer.TenantId);

                fromHolder.RemoveShareBalanceRange(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo);
                companyHolder.RemoveShareBalanceRange(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo);

                await _shareholderRepository.UpdateAsync(fromHolder);
                await _shareholderRepository.UpdateAsync(companyHolder);
                break;
            }
            case ShareTransferType.Transfer:
            {
                var fromHolder = await _shareholderRepository.GetAsync(transfer.FromShareholderId!.Value);
                ValidateSharesHeld(fromHolder, transfer);
                var toHolder = await _shareholderRepository.GetAsync(transfer.ToShareholderId!.Value);

                fromHolder.RemoveShareBalanceRange(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo);
                toHolder.AddShareBalance(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo, transfer.Rate);

                await _shareholderRepository.UpdateAsync(fromHolder);
                await _shareholderRepository.UpdateAsync(toHolder);
                break;
            }
        }

        transfer.MarkSubmitted();
    }

    public async Task CancelAsync(ShareTransfer transfer, string companyTitle)
    {
        switch (transfer.TransferType)
        {
            case ShareTransferType.Issue:
            {
                var companyHolder = await GetOrCreateCompanyShareholderAsync(transfer.CompanyId, companyTitle, transfer.TenantId);
                var toHolder = await _shareholderRepository.GetAsync(transfer.ToShareholderId!.Value);

                companyHolder.RemoveShareBalanceRange(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo);
                toHolder.RemoveShareBalanceRange(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo);

                await _shareholderRepository.UpdateAsync(companyHolder);
                await _shareholderRepository.UpdateAsync(toHolder);
                break;
            }
            case ShareTransferType.Purchase:
            {
                var fromHolder = await _shareholderRepository.GetAsync(transfer.FromShareholderId!.Value);
                var companyHolder = await GetOrCreateCompanyShareholderAsync(transfer.CompanyId, companyTitle, transfer.TenantId);

                fromHolder.AddShareBalance(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo, transfer.Rate);
                companyHolder.AddShareBalance(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo, transfer.Rate, isCompany: true);

                await _shareholderRepository.UpdateAsync(fromHolder);
                await _shareholderRepository.UpdateAsync(companyHolder);
                break;
            }
            case ShareTransferType.Transfer:
            {
                var toHolder = await _shareholderRepository.GetAsync(transfer.ToShareholderId!.Value);
                var fromHolder = await _shareholderRepository.GetAsync(transfer.FromShareholderId!.Value);

                toHolder.RemoveShareBalanceRange(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo);
                fromHolder.AddShareBalance(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo, transfer.Rate);

                await _shareholderRepository.UpdateAsync(toHolder);
                await _shareholderRepository.UpdateAsync(fromHolder);
                break;
            }
        }

        transfer.MarkCancelled();
    }

    private static void ValidateNotAlreadyIssued(Shareholder companyHolder, ShareTransfer transfer)
    {
        var state = companyHolder.CheckRangeOwnership(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo);
        if (state is "Complete" or "Partial")
            throw new BusinessException(MyERPDomainErrorCodes.ShareTransferSharesAlreadyExist);
    }

    private static void ValidateSharesHeld(Shareholder holder, ShareTransfer transfer)
    {
        var state = holder.CheckRangeOwnership(transfer.ShareTypeId, transfer.FromNo, transfer.ToNo);
        if (state is "Outside" or "Partial")
            throw new BusinessException(MyERPDomainErrorCodes.ShareTransferSharesDoNotExist);
    }
}
