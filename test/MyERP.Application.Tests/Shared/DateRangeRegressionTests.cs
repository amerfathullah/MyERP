using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Shared;

public class DateRangeRegressionTests
{
    [Fact]
    public void DateRangeHelper_NormalizesStartOfDayAndExclusiveNextDay()
    {
        var input = new DateTime(2026, 11, 16, 15, 42, 10);

        DateRangeHelper.StartOfDay(input).ShouldBe(new DateTime(2026, 11, 16, 0, 0, 0));
        DateRangeHelper.ExclusiveNextDay(input).ShouldBe(new DateTime(2026, 11, 17, 0, 0, 0));
    }

    [Fact]
    public async Task StatementOfAccounts_GetAllPartyStatements_IncludesSameDayAfternoonInvoices()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        customerRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Customer>
            {
                new Customer(customerId, companyId, "Evening Buyer Sdn Bhd")
            }.AsQueryable()));

        var afternoonInv = new SalesInvoice(
            Guid.NewGuid(),
            companyId,
            customerId,
            "SINV-PM-001",
            new DateTime(2026, 11, 16, 18, 30, 0));
        afternoonInv.AddItem(Guid.NewGuid(), "Service", 1m, 450m, 0m);
        afternoonInv.Submit();
        afternoonInv.Post();

        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        siRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesInvoice> { afternoonInv }.AsQueryable()));

        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        peRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<PaymentEntry>().AsQueryable()));
        var supRepo = Substitute.For<IRepository<Supplier, Guid>>();

        var service = new StatementOfAccountsAppService(
            siRepo,
            piRepo,
            peRepo,
            customerRepo,
            supRepo);

        var result = await service.ProcessBatchStatementAsync(new BatchStatementOfAccountsInput
        {
            CompanyId = companyId,
            PartyType = "Customer",
            FromDate = new DateTime(2026, 11, 16),
            ToDate = new DateTime(2026, 11, 16),
            IncludeZeroBalance = false,
            IncludeAging = true
        });

        result.Statements.Count.ShouldBe(1);
        result.Statements[0].InvoicedAmount.ShouldBe(450m);
        result.Statements[0].ClosingBalance.ShouldBe(450m);
    }

    [Fact]
    public async Task RequestedItemsToTransfer_IncludesSameDayAfternoonRequests_WhenToDateAtMidnight()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var mr = new MaterialRequest(
            Guid.NewGuid(),
            companyId,
            "MR-PM-001",
            MaterialRequestType.MaterialTransfer,
            new DateTime(2026, 11, 16, 17, 15, 0));
        mr.AddItem(itemId, "Bolt M8", 10m, "Nos");
        mr.Submit();

        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        mrRepo.WithDetailsAsync(Arg.Any<Expression<Func<MaterialRequest, object>>[]>())
            .Returns(Task.FromResult(new List<MaterialRequest> { mr }.AsQueryable()));

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item>
            {
                new Item(itemId, companyId, "BOLT-M8", "Bolt M8", ItemType.Goods)
            }.AsQueryable()));

        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        var service = new RequestedItemsToTransferAppService(mrRepo, itemRepo, warehouseRepo);
        var report = await service.GetReportAsync(new RequestedItemsToTransferFilterDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 11, 16),
            ToDate = new DateTime(2026, 11, 16)
        });

        report.Rows.Count.ShouldBe(1);
        report.Rows[0].RequestNumber.ShouldBe("MR-PM-001");
        report.Rows[0].QtyToTransfer.ShouldBe(10m);
    }

    [Fact]
    public async Task CogsByItemGroup_IncludesSameDayAfternoonEntries_WhenToDateAtMidnight()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var targetDate = new DateTime(2026, 11, 16);

        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        companyRepo.FindAsync(companyId).Returns(new Company(companyId, "Evening Mfg"));

        var itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
        itemGroupRepo.GetListAsync().Returns(new List<ItemGroup>
        {
            new(groupId, "Goods", isGroup: false, parentId: null)
        });

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(new List<Item>
        {
            new(itemId, companyId, "ITEM-PM", "Evening Item", ItemType.Goods) { ItemGroupId = groupId }
        }.AsQueryable());

        var sle = new StockLedgerEntry(
            Guid.NewGuid(),
            companyId,
            itemId,
            whId,
            new DateTime(2026, 11, 16, 18, 45, 0),
            -3m,
            50m,
            7m,
            350m)
        {
            StockValueDifference = -150m
        };

        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sle }.AsQueryable());

        var service = new CogsByItemGroupAppService(sleRepo, itemRepo, itemGroupRepo, companyRepo);
        var report = await service.GetReportAsync(new CogsByItemGroupFilterDto
        {
            CompanyId = companyId,
            FromDate = targetDate,
            ToDate = targetDate
        });

        report.TotalCogsDebit.ShouldBe(150m);
        report.Rows.Count.ShouldBe(1);
        report.Rows[0].CogsDebit.ShouldBe(150m);
    }

    [Fact]
    public async Task SubcontractOrderSummary_IncludesSameDayAfternoonOrders_WhenToDateAtMidnight()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var targetDate = new DateTime(2026, 11, 16);

        var sco = new SubcontractingOrder(
            Guid.NewGuid(),
            companyId,
            "SCO-PM-001",
            new DateTime(2026, 11, 16, 16, 20, 0),
            supplierId);
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItemId, "Motor", qty: 5m, rate: 100m));
        sco.Submit();

        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        scoRepo.WithDetailsAsync(Arg.Any<Expression<Func<SubcontractingOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SubcontractingOrder> { sco }.AsQueryable()));

        var supRepo = Substitute.For<IRepository<Supplier, Guid>>();
        supRepo.GetQueryableAsync().Returns(new List<Supplier>
        {
            new(supplierId, companyId, "Evening Subcontractor")
        }.AsQueryable());

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(new List<Item>
        {
            new(fgItemId, companyId, "MOTOR-1", "Motor", ItemType.Goods)
        }.AsQueryable());

        var service = new SubcontractOrderSummaryAppService(scoRepo, supRepo, itemRepo);
        var report = await service.GetReportAsync(new SubcontractOrderSummaryFilterDto
        {
            CompanyId = companyId,
            FromDate = targetDate,
            ToDate = targetDate
        });

        report.TotalOrders.ShouldBe(1);
        report.Rows.Count.ShouldBe(1);
        report.Rows[0].SubcontractOrderNumber.ShouldBe("SCO-PM-001");
    }
}
