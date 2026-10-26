using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Inventory;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

public class UpstreamParityBatch20261026Part5Tests
{
    // --- ERPNext PR #61620 / commits 0850b876a3, 308143fc67, 58e9c6f8a8: Dunning Lifecycle, Currency, Income Account ---

    [Fact]
    public void Dunning_Submit_WithNonPositiveConversionRate_ThrowsValidationFailed()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var dunning = new Dunning(Guid.NewGuid(), companyId, customerId, DateTime.UtcNow, 1)
        {
            Currency = "USD",
            ConversionRate = 0m,
        };
        dunning.AddOverduePayment(Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(-10), 10);

        var ex = Should.Throw<BusinessException>(() => dunning.Submit(validateIncomeAccount: true));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Conversion Rate must be greater than zero");
    }

    [Fact]
    public void Dunning_Submit_WithDunningAmountAndNoIncomeAccount_ThrowsValidationFailed()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var dunning = new Dunning(Guid.NewGuid(), companyId, customerId, DateTime.UtcNow, 1)
        {
            ConversionRate = 1m,
            DunningFee = 15m,
            IncomeAccountId = null,
        };
        dunning.AddOverduePayment(Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(-10), 10);

        var ex = Should.Throw<BusinessException>(() => dunning.Submit(validateIncomeAccount: true));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Income Account is required to collect dunning fees or interest");
    }

    [Fact]
    public void Dunning_Submit_WithValidConversionRateAndIncomeAccount_Succeeds()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var incomeAccId = Guid.NewGuid();
        var dunning = new Dunning(Guid.NewGuid(), companyId, customerId, DateTime.UtcNow, 1)
        {
            ConversionRate = 4.45m,
            DunningFee = 20m,
            IncomeAccountId = incomeAccId,
        };
        dunning.AddOverduePayment(Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(-10), 10);

        dunning.Submit();

        dunning.Status.ShouldBe(DocumentStatus.Submitted);
        dunning.BaseDunningAmount.ShouldBe(Math.Round(20m * 4.45m, 2));
    }

    [Fact]
    public void Dunning_Cancel_WithPaidDunningAmount_ThrowsValidationFailed()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var incomeAccId = Guid.NewGuid();
        var dunning = new Dunning(Guid.NewGuid(), companyId, customerId, DateTime.UtcNow, 1)
        {
            ConversionRate = 1m,
            DunningFee = 25m,
            IncomeAccountId = incomeAccId,
        };
        dunning.AddOverduePayment(Guid.NewGuid(), 200m, DateTime.UtcNow.AddDays(-15), 15);
        dunning.Submit();

        dunning.RecordDunningPayment(25m);

        var ex = Should.Throw<BusinessException>(() => dunning.Cancel());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Cannot cancel Dunning with paid fees or interest");
    }

    [Fact]
    public void Dunning_Cancel_WithZeroPaidDunningAmount_Succeeds()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var incomeAccId = Guid.NewGuid();
        var dunning = new Dunning(Guid.NewGuid(), companyId, customerId, DateTime.UtcNow, 1)
        {
            ConversionRate = 1m,
            DunningFee = 25m,
            IncomeAccountId = incomeAccId,
        };
        dunning.AddOverduePayment(Guid.NewGuid(), 200m, DateTime.UtcNow.AddDays(-15), 15);
        dunning.Submit();

        dunning.Cancel();

        dunning.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    // --- ERPNext PR #62287 / commit 81940327fe: Stock Entry Transfer Detection & Recalculate Valuation Rate ---

    [Fact]
    public void StockEntry_HasTransferRow_ReturnsTrueWhenRowHasSourceAndTargetWarehouse()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialTransfer, DateTime.UtcNow);
        var sourceWh = Guid.NewGuid();
        var targetWh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 10m, sourceWarehouseId: sourceWh, targetWarehouseId: targetWh);

        se.HasTransferRow.ShouldBeTrue();
    }

    [Fact]
    public void StockEntry_HasTransferRow_ReturnsFalseWhenRowsHaveOnlySourceOrOnlyTarget()
    {
        var seIssue = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialIssue, DateTime.UtcNow);
        seIssue.AddItem(Guid.NewGuid(), 10m, sourceWarehouseId: Guid.NewGuid(), targetWarehouseId: null);
        seIssue.HasTransferRow.ShouldBeFalse();

        var seReceipt = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.UtcNow);
        seReceipt.AddItem(Guid.NewGuid(), 10m, sourceWarehouseId: null, targetWarehouseId: Guid.NewGuid());
        seReceipt.HasTransferRow.ShouldBeFalse();
    }

    [Fact]
    public void RepostItemValuation_ValidateRecalculateValuationRate_RejectsTransferStockEntry()
    {
        var riv = new RepostItemValuation(
            Guid.NewGuid(), Guid.NewGuid(), RepostMethod.Transaction, DateTime.UtcNow)
        {
            VoucherType = "Stock Entry",
            VoucherId = Guid.NewGuid(),
            RecalculateValuationRate = true,
        };

        var ex = Should.Throw<BusinessException>(() =>
            riv.ValidateRecalculateValuationRate(isUpdateStock: null, isTransferStockEntry: true));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("no row that has both a Source and Target Warehouse");
    }

    [Fact]
    public void RepostItemValuation_ValidateRecalculateValuationRate_AllowsNonTransferStockEntry()
    {
        var riv = new RepostItemValuation(
            Guid.NewGuid(), Guid.NewGuid(), RepostMethod.Transaction, DateTime.UtcNow)
        {
            VoucherType = "Stock Entry",
            VoucherId = Guid.NewGuid(),
            RecalculateValuationRate = true,
        };

        // Should not throw
        riv.ValidateRecalculateValuationRate(isUpdateStock: null, isTransferStockEntry: false);
    }
}
