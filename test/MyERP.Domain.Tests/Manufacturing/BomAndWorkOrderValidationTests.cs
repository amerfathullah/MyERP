using System;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Unit tests for BOM and Work Order validation rules:
/// - BOM with operations material transfer destination (Gotcha #446)
/// - BOM secondary items FG exclusion and process loss limit (Gotcha #441)
/// - Work Order whole number UOM check (Gotcha #497)
/// - Work Order independent date pairs check (Gotcha #717)
/// </summary>
public class BomAndWorkOrderValidationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _rmItemId = Guid.NewGuid();

    [Fact]
    public void BillOfMaterials_WithoutOperations_ForcesTransferAgainstWorkOrder()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-ITEM-001", _fgItemId)
        {
            WithOperations = false,
            TransferMaterialAgainst = "Job Card" // Attempt to set Job Card without operations
        };

        bom.ValidateOperations();

        Assert.Equal("Work Order", bom.TransferMaterialAgainst);
    }

    [Fact]
    public void BillOfMaterials_WithOperations_EmptyTransferAgainst_ThrowsValidationException()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-ITEM-002", _fgItemId)
        {
            WithOperations = true,
            TrackSemiFinishedGoods = false,
            TransferMaterialAgainst = "" // Invalid
        };

        var ex = Assert.Throws<BusinessException>(() => bom.ValidateOperations());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("Transfer Material Against is mandatory", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void BillOfMaterials_AddSecondaryItem_SameAsFg_ThrowsException()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-ITEM-003", _fgItemId);
        var secondaryItem = new BomSecondaryItem(Guid.NewGuid(), bom.Id, _fgItemId, SecondaryItemType.Scrap, 1m);

        var ex = Assert.Throws<BusinessException>(() => bom.AddSecondaryItem(secondaryItem));
        Assert.Equal(MyERPDomainErrorCodes.BomFgCannotBeSecondaryItem, ex.Code);
    }

    [Fact]
    public void BillOfMaterials_AddSecondaryItem_ProcessLoss100OrMore_ThrowsException()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-ITEM-004", _fgItemId);
        var secondaryItem = new BomSecondaryItem(Guid.NewGuid(), bom.Id, _rmItemId, SecondaryItemType.ByProduct, 1m)
        {
            ProcessLossPercentage = 100m
        };

        var ex = Assert.Throws<BusinessException>(() => bom.AddSecondaryItem(secondaryItem));
        Assert.Equal(MyERPDomainErrorCodes.InvalidProcessLossPercentage, ex.Code);
    }

    [Fact]
    public void WorkOrder_ValidateWholeNumberQuantity_Fractional_ThrowsValidationException()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "MFG-WO-2026-0001", _fgItemId, Guid.NewGuid(), 2.5m);

        var ex = Assert.Throws<BusinessException>(() => wo.ValidateWholeNumberQuantity(mustBeWholeNumber: true));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("must be a whole number", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void WorkOrder_ValidateWholeNumberQuantity_Integer_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "MFG-WO-2026-0002", _fgItemId, Guid.NewGuid(), 5.0m);
        wo.ValidateWholeNumberQuantity(mustBeWholeNumber: true); // No exception
    }

    [Fact]
    public void WorkOrder_ValidateDates_PlannedEndBeforeStart_ThrowsException()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "MFG-WO-2026-0003", _fgItemId, Guid.NewGuid(), 10m);
        wo.PlannedStartDate = DateTime.UtcNow.AddDays(5);
        wo.PlannedEndDate = DateTime.UtcNow.AddDays(2);

        var ex = Assert.Throws<BusinessException>(() => wo.ValidateDates());
        Assert.Equal(MyERPDomainErrorCodes.PlannedEndDateBeforeStartDate, ex.Code);
    }
}
