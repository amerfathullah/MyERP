using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

public class AssetRepairPurchaseInvoice : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AssetRepairId { get; set; }
    public Guid PurchaseInvoiceId { get; set; }
    public string? PurchaseInvoiceNumber { get; set; }
    public decimal RepairCost { get; set; }
    public Guid? ExpenseAccountId { get; set; }

    protected AssetRepairPurchaseInvoice() { }

    public AssetRepairPurchaseInvoice(
        Guid id,
        Guid assetRepairId,
        Guid purchaseInvoiceId,
        decimal repairCost,
        string? purchaseInvoiceNumber = null,
        Guid? expenseAccountId = null,
        Guid? tenantId = null)
        : base(id)
    {
        if (repairCost < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "RepairCost");
        }

        AssetRepairId = assetRepairId;
        PurchaseInvoiceId = purchaseInvoiceId;
        RepairCost = repairCost;
        PurchaseInvoiceNumber = purchaseInvoiceNumber;
        ExpenseAccountId = expenseAccountId;
        TenantId = tenantId;
    }
}
