using System;
using MyERP.Accounting;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Share Transfer — records the issue, purchase (buy-back), or transfer of a contiguous
/// block of shares between shareholders. Submitting mutates the involved Shareholders'
/// share balances (see <see cref="MyERP.Accounting.DomainServices.ShareTransferService"/>).
/// Maps to ERPNext accounts/doctype/share_transfer.
/// </summary>
public class ShareTransfer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public ShareTransferType TransferType { get; set; }
    public DateTime Date { get; set; }

    public Guid? FromShareholderId { get; set; }
    public string? FromFolioNo { get; set; }
    public Guid? ToShareholderId { get; set; }
    public string? ToFolioNo { get; set; }

    public Guid ShareTypeId { get; set; }
    public int FromNo { get; set; }
    public int ToNo { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public int NoOfShares => ToNo - FromNo + 1;

    public Guid EquityOrLiabilityAccountId { get; set; }
    public Guid? AssetAccountId { get; set; }

    public string? Remarks { get; set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    protected ShareTransfer() { }

    public ShareTransfer(Guid id, Guid companyId, ShareTransferType transferType, DateTime date,
        Guid shareTypeId, int fromNo, int toNo, decimal rate, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        TransferType = transferType;
        Date = date;
        ShareTypeId = shareTypeId;
        FromNo = fromNo;
        ToNo = toNo;
        Rate = rate;
        Amount = rate * NoOfShares;
        TenantId = tenantId;
    }

    /// <summary>
    /// Basic field validations. Per ERPNext ShareTransfer.basic_validations.
    /// Cross-shareholder checks (folio numbers, existing share ranges) require repository
    /// access and live in the domain service.
    /// </summary>
    public void Validate()
    {
        switch (TransferType)
        {
            case ShareTransferType.Issue:
                if (ToShareholderId == null)
                    throw new BusinessException(MyERPDomainErrorCodes.ShareTransferMissingParty).WithData("field", "ToShareholder");
                break;
            case ShareTransferType.Purchase:
                if (FromShareholderId == null)
                    throw new BusinessException(MyERPDomainErrorCodes.ShareTransferMissingParty).WithData("field", "FromShareholder");
                if (AssetAccountId == null)
                    throw new BusinessException(MyERPDomainErrorCodes.ShareTransferMissingAccount).WithData("field", "AssetAccount");
                break;
            case ShareTransferType.Transfer:
                if (FromShareholderId == null || ToShareholderId == null)
                    throw new BusinessException(MyERPDomainErrorCodes.ShareTransferMissingParty).WithData("field", "FromShareholder/ToShareholder");
                break;
        }

        if (FromShareholderId.HasValue && FromShareholderId == ToShareholderId)
            throw new BusinessException(MyERPDomainErrorCodes.ShareTransferSellerBuyerSame);

        if (NoOfShares <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.ShareTransferCountMismatch);

        if (Amount != Rate * NoOfShares)
            throw new BusinessException(MyERPDomainErrorCodes.ShareTransferAmountMismatch);
    }

    public void MarkSubmitted() => Status = DocumentStatus.Submitted;
    public void MarkCancelled() => Status = DocumentStatus.Cancelled;
}
