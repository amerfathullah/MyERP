using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

public class UpstreamPR57140And57571Tests
{
    [Fact]
    public void SalesInvoiceItem_ServiceStopDate_CanBeSetAndCleared()
    {
        var item = new SalesInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Consulting", 1, 1200, 0);
        item.EnableDeferredRevenue = true;
        item.DeferredRevenueAccountId = Guid.NewGuid();
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 12, 31);
        item.ServiceStopDate = new DateTime(2026, 6, 30);

        Assert.True(item.EnableDeferredRevenue);
        Assert.NotNull(item.ServiceStopDate);
        Assert.Equal(new DateTime(2026, 6, 30), item.ServiceStopDate);

        // Disabling deferred revenue clears all fields per PR #57140
        item.EnableDeferredRevenue = false;
        Assert.Null(item.DeferredRevenueAccountId);
        Assert.Null(item.ServiceStartDate);
        Assert.Null(item.ServiceEndDate);
        Assert.Null(item.ServiceStopDate);
    }

    [Fact]
    public void SalesInvoice_Submit_Validates_ServiceStopDate_WithinRange()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", new DateTime(2026, 1, 1));
        si.AddItem(Guid.NewGuid(), "Subscription", 1, 1200, 0);
        var item = si.Items.First();
        item.EnableDeferredRevenue = true;
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 12, 31);
        item.ServiceStopDate = new DateTime(2026, 6, 30);

        // Valid range: Submit succeeds without throwing
        si.Submit();
        Assert.Equal(DocumentStatus.Submitted, si.Status);
    }

    [Fact]
    public void SalesInvoice_Submit_Throws_When_ServiceStopDate_Before_StartDate()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-002", new DateTime(2026, 1, 1));
        si.AddItem(Guid.NewGuid(), "Subscription", 1, 1200, 0);
        var item = si.Items.First();
        item.EnableDeferredRevenue = true;
        item.ServiceStartDate = new DateTime(2026, 3, 1);
        item.ServiceEndDate = new DateTime(2026, 12, 31);
        item.ServiceStopDate = new DateTime(2026, 1, 1); // Before start date!

        var ex = Assert.Throws<BusinessException>(() => si.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void SalesInvoice_Submit_Throws_When_ServiceStopDate_After_EndDate()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-003", new DateTime(2026, 1, 1));
        si.AddItem(Guid.NewGuid(), "Subscription", 1, 1200, 0);
        var item = si.Items.First();
        item.EnableDeferredRevenue = true;
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 6, 30);
        item.ServiceStopDate = new DateTime(2026, 12, 31); // After end date!

        var ex = Assert.Throws<BusinessException>(() => si.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void PurchaseInvoiceItem_ServiceStopDate_CanBeSetAndCleared()
    {
        var item = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Cloud Hosting", 1, 2400, 0);
        item.EnableDeferredExpense = true;
        item.DeferredExpenseAccountId = Guid.NewGuid();
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 12, 31);
        item.ServiceStopDate = new DateTime(2026, 5, 31);

        Assert.True(item.EnableDeferredExpense);
        Assert.NotNull(item.ServiceStopDate);

        item.EnableDeferredExpense = false;
        Assert.Null(item.DeferredExpenseAccountId);
        Assert.Null(item.ServiceStartDate);
        Assert.Null(item.ServiceEndDate);
        Assert.Null(item.ServiceStopDate);
    }

    [Fact]
    public void PurchaseInvoice_Submit_Throws_When_ServiceStopDate_OutsideRange()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", new DateTime(2026, 1, 1));
        pi.AddItem(Guid.NewGuid(), "Support Contract", 1, 1200, 0);
        var item = pi.Items.First();
        item.EnableDeferredExpense = true;
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 6, 30);
        item.ServiceStopDate = new DateTime(2026, 8, 31); // Invalid: after end date

        var ex = Assert.Throws<BusinessException>(() => pi.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void DeferredAccountingService_GenerateSchedule_Respects_ServiceStopDate()
    {
        var salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();

        var service = new DeferredAccountingService(jeRepo, salesInvoiceRepo, piRepo, fyRepo, companyRepo);

        var item = new SalesInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Annual SaaS", 1, 1200, 0);
        item.EnableDeferredRevenue = true;
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 12, 31);
        item.ServiceStopDate = new DateTime(2026, 4, 30); // Early termination at 4 months

        var schedule = service.GenerateSchedule(item, new DateTime(2026, 1, 1));

        // Total months should be truncated to 4 months (Jan-Apr) instead of 12
        Assert.Equal(4, schedule.Count);
        Assert.Equal(1200m, schedule.Sum(s => s.Amount));
    }

    [Fact]
    public void DeferredAccountingService_GenerateExpenseSchedule_Respects_ServiceStopDate()
    {
        var salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();

        var service = new DeferredAccountingService(jeRepo, salesInvoiceRepo, piRepo, fyRepo, companyRepo);

        var item = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Support Plan", 1, 600, 0);
        item.EnableDeferredExpense = true;
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 6, 30);
        item.ServiceStopDate = new DateTime(2026, 3, 31); // Terminated at 3 months

        var schedule = service.GenerateExpenseSchedule(item, new DateTime(2026, 1, 1));

        Assert.Equal(3, schedule.Count);
        Assert.Equal(600m, schedule.Sum(s => s.Amount));
    }

    [Fact]
    public void Company_WarehouseDefaults_PR57571_HasAllSevenWarehouses()
    {
        var company = new Company(Guid.NewGuid(), "Acme Corp");

        company.DefaultWarehouseId = Guid.NewGuid();
        company.SampleRetentionWarehouseId = Guid.NewGuid();
        company.DefaultInTransitWarehouseId = Guid.NewGuid();
        company.DefaultWarehouseForSalesReturnId = Guid.NewGuid();
        company.DefaultWipWarehouseId = Guid.NewGuid();
        company.DefaultFgWarehouseId = Guid.NewGuid();
        company.DefaultScrapWarehouseId = Guid.NewGuid();

        Assert.NotNull(company.DefaultWarehouseId);
        Assert.NotNull(company.SampleRetentionWarehouseId);
        Assert.NotNull(company.DefaultInTransitWarehouseId);
        Assert.NotNull(company.DefaultWarehouseForSalesReturnId);
        Assert.NotNull(company.DefaultWipWarehouseId);
        Assert.NotNull(company.DefaultFgWarehouseId);
        Assert.NotNull(company.DefaultScrapWarehouseId);
    }

    [Fact]
    public void PurchaseReceipt_Submit_AutoCorrects_ConversionFactor_When_Uom_Matches_StockUom()
    {
        var whId = Guid.NewGuid();
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), whId, "PR-001", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Item A", 10, 50, 0, "Nos");
        var item = pr.Items.First();
        item.StockUom = "Nos";
        item.ConversionFactor = 100m; // Accidental bad value

        pr.Submit();

        Assert.Equal(1.0m, item.ConversionFactor);
        Assert.Equal(10m, item.StockQty);
    }

    [Fact]
    public void PurchaseReceipt_Submit_Throws_When_FromWarehouse_Equals_TargetWarehouse()
    {
        var whId = Guid.NewGuid();
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), whId, "PR-002", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Item B", 10, 50, 0);
        var item = pr.Items.First();
        item.FromWarehouseId = whId; // Same as header TargetWarehouse!

        var ex = Assert.Throws<BusinessException>(() => pr.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void PurchaseReceipt_Submit_Throws_When_FromWarehouse_Set_On_Subcontracted()
    {
        var whId = Guid.NewGuid();
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), whId, "PR-003", DateTime.UtcNow);
        pr.IsSubcontracted = true;
        pr.AddItem(Guid.NewGuid(), "Subcontracted Item", 10, 50, 0);
        var item = pr.Items.First();
        item.FromWarehouseId = Guid.NewGuid(); // Forbidden on subcontracted

        var ex = Assert.Throws<BusinessException>(() => pr.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void StockEntry_Submit_Throws_When_SourceWarehouse_Equals_TargetWarehouse()
    {
        var se = new MyERP.Inventory.Entities.StockEntry(Guid.NewGuid(), Guid.NewGuid(), MyERP.Inventory.StockEntryType.MaterialTransfer, DateTime.UtcNow);
        var whId = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 5, whId, whId); // Same source and target

        var ex = Assert.Throws<BusinessException>(() => se.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void Item_GrantCommission_DefaultTrue_AndCanBeDisabled()
    {
        var item = new MyERP.Inventory.Entities.Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Commission Item", MyERP.Inventory.ItemType.Goods);
        Assert.True(item.GrantCommission);

        item.GrantCommission = false;
        Assert.False(item.GrantCommission);
    }

    [Fact]
    public void Company_AllowUomWithConversionRateDefinedInItem_CanBeToggled()
    {
        var company = new Company(Guid.NewGuid(), "Acme Global");
        Assert.False(company.AllowUomWithConversionRateDefinedInItem);

        company.AllowUomWithConversionRateDefinedInItem = true;
        Assert.True(company.AllowUomWithConversionRateDefinedInItem);
    }

    [Fact]
    public void SalesOrder_Submit_AutoCorrects_ConversionFactor_When_Uom_Matches_StockUom()
    {
        var so = new MyERP.Sales.Entities.SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Laptop", 2, 3000, 0, "Unit");
        var item = so.Items.First();
        item.StockUom = "Unit";
        item.ConversionFactor = 50m; // Bad conversion factor

        so.Submit();

        Assert.Equal(1.0m, item.ConversionFactor);
        Assert.Equal(2m, item.StockQty);
    }

    [Fact]
    public void PurchaseOrder_Submit_AutoCorrects_ConversionFactor_When_Uom_Matches_StockUom()
    {
        var po = new MyERP.Purchasing.Entities.PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Raw Material", 10, 50, 0, "Kg");
        var item = po.Items.First();
        item.StockUom = "Kg";
        item.ConversionFactor = 10m; // Bad conversion factor

        po.Submit();

        Assert.Equal(1.0m, item.ConversionFactor);
        Assert.Equal(10m, item.StockQty);
    }

    [Fact]
    public void Quotation_Submit_AutoCorrects_ConversionFactor_When_Uom_Matches_StockUom()
    {
        var q = new MyERP.Sales.Entities.Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QT-001", DateTime.UtcNow);
        q.AddItem(Guid.NewGuid(), "Service Pack", 1, 500, 0, "Nos");
        var item = q.Items.First();
        item.StockUom = "Nos";
        item.ConversionFactor = 5m;

        q.Submit();

        Assert.Equal(1.0m, item.ConversionFactor);
        Assert.Equal(1m, item.StockQty);
    }

    [Fact]
    public void SupplierQuotation_Submit_AutoCorrects_ConversionFactor_When_Uom_Matches_StockUom()
    {
        var sq = new MyERP.Purchasing.Entities.SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        sq.AddItem(Guid.NewGuid(), 5, 20, "Bolts", "Nos");
        var item = sq.Items.First();
        item.StockUom = "Nos";
        item.ConversionFactor = 25m;

        sq.Submit();

        Assert.Equal(1.0m, item.ConversionFactor);
        Assert.Equal(5m, item.StockQty);
    }

    [Fact]
    public void JournalEntry_Post_Throws_When_CreditNote_Has_No_Reference()
    {
        var je = new MyERP.Accounting.Entities.JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)
        {
            VoucherType = MyERP.Accounting.JournalEntryVoucherType.CreditNote
        };
        je.AddLine(Guid.NewGuid(), 100, true);
        je.AddLine(Guid.NewGuid(), 100, false);

        var ex = Assert.Throws<BusinessException>(() => je.Post());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);

        // With reference, posting succeeds
        je.ReferenceNumber = "SI-2026-0001";
        je.Post();
        Assert.Equal(MyERP.Core.DocumentStatus.Posted, je.Status);
    }

    [Fact]
    public void PurchaseInvoice_Submit_Throws_When_FromWarehouse_On_Subcontracted_Invoice()
    {
        var pi = new MyERP.Purchasing.Entities.PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PINV-001", DateTime.UtcNow)
        {
            IsSubcontracted = true
        };
        pi.AddItem(Guid.NewGuid(), "Subcontracted Part", 10, 50, 0, "Nos");
        var item = pi.Items.First();
        item.FromWarehouseId = Guid.NewGuid();

        var ex = Assert.Throws<BusinessException>(() => pi.Submit());
        Assert.Equal(MyERPDomainErrorCodes.FromWarehouseOnSubcontractedDocument, ex.Code);
    }

    [Fact]
    public async Task StockRepostGuard_Throws_When_Repost_Is_InProgress()
    {
        var repostRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.RepostItemValuation, Guid>>();
        var voucherId = Guid.NewGuid();
        var repost = new MyERP.Inventory.Entities.RepostItemValuation(
            Guid.NewGuid(), Guid.NewGuid(), MyERP.Inventory.Entities.RepostMethod.ItemAndWarehouse, DateTime.UtcNow)
        {
            VoucherType = "DeliveryNote",
            VoucherId = voucherId
        };
        repost.StartProcessing();

        var queryable = new List<MyERP.Inventory.Entities.RepostItemValuation> { repost }.AsQueryable();
        repostRepo.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var guard = new MyERP.Inventory.DomainServices.StockRepostGuardService(repostRepo);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => guard.ValidateCanCancelVoucherAsync("DeliveryNote", voucherId));
        Assert.Equal(MyERPDomainErrorCodes.RepostAlreadyInProgress, ex.Code);
    }

    [Fact]
    public async Task StockRepostGuard_AutoSkips_When_Repost_Is_Queued()
    {
        var repostRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.RepostItemValuation, Guid>>();
        var voucherId = Guid.NewGuid();
        var repost = new MyERP.Inventory.Entities.RepostItemValuation(
            Guid.NewGuid(), Guid.NewGuid(), MyERP.Inventory.Entities.RepostMethod.ItemAndWarehouse, DateTime.UtcNow)
        {
            VoucherType = "PurchaseReceipt",
            VoucherId = voucherId
        };

        var queryable = new List<MyERP.Inventory.Entities.RepostItemValuation> { repost }.AsQueryable();
        repostRepo.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var guard = new MyERP.Inventory.DomainServices.StockRepostGuardService(repostRepo);
        await guard.ValidateCanCancelVoucherAsync("PurchaseReceipt", voucherId);

        Assert.Equal(MyERP.Inventory.Entities.RepostStatus.Skipped, repost.Status);
        await repostRepo.Received(1).UpdateAsync(repost, autoSave: true);
    }

    [Fact]
    public void ChildItemUpdateService_Throws_When_Deleting_Delivered_Or_Billed_SO_Item()
    {
        var service = new MyERP.Core.DomainServices.ChildItemUpdateService();
        var soItem = new MyERP.Sales.Entities.SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Laptop", 1, 1000, 0, "Nos")
        {
            DeliveredQty = 1
        };

        var ex = Assert.Throws<BusinessException>(() => service.ValidateSalesOrderItemDeletion(soItem));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);

        soItem.DeliveredQty = 0;
        soItem.BilledQty = 1;
        ex = Assert.Throws<BusinessException>(() => service.ValidateSalesOrderItemDeletion(soItem));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);

        soItem.BilledQty = 0;
        // Should not throw when 0 delivered and 0 billed
        service.ValidateSalesOrderItemDeletion(soItem);
    }

    [Fact]
    public void ChildItemUpdateService_Throws_When_Deleting_Received_Or_Billed_PO_Item()
    {
        var service = new MyERP.Core.DomainServices.ChildItemUpdateService();
        var poItem = new MyERP.Purchasing.Entities.PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Server", 1, 5000, 0, "Nos")
        {
            ReceivedQty = 1
        };

        var ex = Assert.Throws<BusinessException>(() => service.ValidatePurchaseOrderItemDeletion(poItem));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);

        poItem.ReceivedQty = 0;
        poItem.BilledQty = 1;
        ex = Assert.Throws<BusinessException>(() => service.ValidatePurchaseOrderItemDeletion(poItem));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);

        poItem.BilledQty = 0;
        // Should not throw when 0 received and 0 billed
        service.ValidatePurchaseOrderItemDeletion(poItem);
    }

    [Fact]
    public void PaymentEntry_ClearanceDate_Is_Cleared_When_Null_Passed()
    {
        var pe = new MyERP.Accounting.Entities.PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), MyERP.Accounting.PaymentType.Receive,
            DateTime.UtcNow.Date, 500, Guid.NewGuid(), Guid.NewGuid());
        pe.SetClearanceDate(DateTime.UtcNow.Date);
        Assert.NotNull(pe.ClearanceDate);

        pe.SetClearanceDate(null);
        Assert.Null(pe.ClearanceDate);
    }

    [Fact]
    public void JournalEntry_ClearanceDate_Is_Cleared_When_Null_Passed()
    {
        var je = new MyERP.Accounting.Entities.JournalEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date);
        je.SetClearanceDate(DateTime.UtcNow.Date);
        Assert.NotNull(je.ClearanceDate);

        je.SetClearanceDate(null);
        Assert.Null(je.ClearanceDate);
    }

    [Fact]
    public void DocumentSeries_Resolves_Naming_Tokens_With_Custom_ReferenceDate_And_Abbr()
    {
        var series = new MyERP.Core.Entities.DocumentSeries(
            Guid.NewGuid(), Guid.NewGuid(), "Test Series", "SalesInvoice", ".ABBR.-INV-.YYYY..MM.-#####");

        var testDate = new DateTime(2025, 4, 15);
        var number = series.GenerateNextNumber(testDate, "MYCO");

        Assert.Equal("MYCO-INV-202504-00001", number);
    }

    [Fact]
    public void MaintenanceSchedule_ClearDetails_Clears_Visits()
    {
        var schedule = new MyERP.Maintenance.Entities.MaintenanceSchedule(
            Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), "Monthly");

        schedule.AddDetail(new MyERP.Maintenance.Entities.MaintenanceScheduleDetail(
            Guid.NewGuid(), schedule.Id, new DateTime(2026, 2, 1)));
        Assert.Single(schedule.Details);

        schedule.ClearDetails();
        Assert.Empty(schedule.Details);
    }

    [Fact]
    public void HolidayList_IsHoliday_Checks_Date()
    {
        var hl = new MyERP.HumanResources.Entities.HolidayList(
            Guid.NewGuid(), Guid.NewGuid(), "MY Holidays", 2026);
        hl.AddHoliday(new MyERP.HumanResources.Entities.Holiday(
            Guid.NewGuid(), hl.Id, new DateTime(2026, 8, 31), "National Day"));

        Assert.True(hl.IsHoliday(new DateTime(2026, 8, 31)));
        Assert.False(hl.IsHoliday(new DateTime(2026, 9, 1)));
    }

    [Fact]
    public void Quotation_HasUnitPriceItems_Detected_When_Zero_Qty()
    {
        var qtn = new MyERP.Sales.Entities.Quotation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.UtcNow);
        qtn.AddItem(Guid.NewGuid(), "Service item", 0m, 150m, 0m);

        Assert.True(qtn.HasUnitPriceItems);
    }

    [Fact]
    public void SupplierQuotation_HasUnitPriceItems_Detected_When_Zero_Qty()
    {
        var sq = new MyERP.Purchasing.Entities.SupplierQuotation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        sq.AddItem(Guid.NewGuid(), 0m, 200m);

        Assert.True(sq.HasUnitPriceItems);
    }

    [Fact]
    public void Timesheet_Detail_Calculates_Amounts_Correctly()
    {
        var start = new DateTime(2026, 8, 20, 9, 0, 0);
        var end = new DateTime(2026, 8, 20, 12, 0, 0);
        var detail = new MyERP.Projects.Entities.TimesheetDetail(
            Guid.NewGuid(), Guid.NewGuid(), "Development", start, end, 3.0m)
        {
            IsBillable = true,
            BillingRate = 100m,
            CostingRate = 60m
        };

        Assert.Equal(300m, detail.BillingAmount);
        Assert.Equal(180m, detail.CostingAmount);
    }

    [Fact]
    public void MaintenanceVisit_State_Transitions_Work()
    {
        var visit = new MyERP.Maintenance.Entities.MaintenanceVisit(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "Scheduled");
        Assert.Equal(MyERP.Maintenance.MaintenanceVisitStatus.Open, visit.CompletionStatus);

        visit.Complete();
        Assert.Equal(MyERP.Maintenance.MaintenanceVisitStatus.Completed, visit.CompletionStatus);

        visit.Cancel();
        Assert.Equal(MyERP.Maintenance.MaintenanceVisitStatus.Cancelled, visit.CompletionStatus);
    }
}
