using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.EInvoice;
using MyERP.EInvoice.Entities;
using MyERP.EInvoice.Services;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity batch 19:
/// 1) E-Invoice B2C Consolidation Buyer TIN Enforcement &amp; Document Metadata (LHDN Guideline Section 3.7 / myinvois consolidate_invoice.py).
/// 2) Subcontracting Receipt Return Dependant Detail Check (PR #64797 / commits fee9cb4b0e &amp; 51e5ba7ec9).
/// </summary>
public class UpstreamBatch19ParityTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    // === 1) E-Invoice B2C Consolidation Buyer TIN Enforcement & Metadata ===

    [Fact]
    public async Task EInvoiceConsolidationService_ConsolidateInvoicesAsync_EnforcesGenericBuyerTinAndMetadata_Section3_7()
    {
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var consolRepo = Substitute.For<IRepository<EInvoiceConsolidation, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());

        var inv1 = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SINV-001", DateTime.UtcNow);
        inv1.AddItem(Guid.NewGuid(), "Retail Item 1", 2, 50m, 0m);
        var inv2 = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SINV-002", DateTime.UtcNow);
        inv2.AddItem(Guid.NewGuid(), "Retail Item 2", 1, 120m, 0m);

        var invoices = new List<SalesInvoice> { inv1, inv2 };
        invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(invoices.AsQueryable()));
        invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(invoices.AsQueryable()));
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer>().AsQueryable()));
        consolRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<EInvoiceConsolidation, bool>>>())
            .Returns(Task.FromResult(new List<EInvoiceConsolidation>()));

        SalesInvoice? savedConsolidatedInvoice = null;
        await invoiceRepo.InsertAsync(Arg.Do<SalesInvoice>(si => savedConsolidatedInvoice = si));

        var service = new EInvoiceConsolidationService(invoiceRepo, consolRepo, customerRepo, guidGen);
        var result = await service.ConsolidateInvoicesAsync(new List<Guid> { inv1.Id, inv2.Id }, CompanyId);

        result.ShouldNotBeEmpty();
        savedConsolidatedInvoice.ShouldNotBeNull();
        savedConsolidatedInvoice!.BuyerTin.ShouldBe(EInvoiceConsts.GenericBuyerTin);
        savedConsolidatedInvoice.IsConsolidated.ShouldBeTrue();
        savedConsolidatedInvoice.EInvoiceDocType.ShouldBe(EInvoiceDocumentType.Invoice);
        savedConsolidatedInvoice.EInvoiceStatus.ShouldBe(EInvoiceStatus.NotSubmitted);
        inv1.ConsolidatedSalesInvoiceId.ShouldBe(savedConsolidatedInvoice.Id);
        inv2.ConsolidatedSalesInvoiceId.ShouldBe(savedConsolidatedInvoice.Id);
    }

    [Fact]
    public async Task EInvoiceConsolidationService_ConsolidateInvoicesAsync_SetsBuyerTinAndMetadataOnSplitInvoices_Over10k()
    {
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var consolRepo = Substitute.For<IRepository<EInvoiceConsolidation, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());

        var inv1 = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SINV-001", DateTime.UtcNow);
        inv1.AddItem(Guid.NewGuid(), "Luxury Item Exceeding Threshold", 1, 15000m, 0m);
        var inv2 = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SINV-002", DateTime.UtcNow);
        inv2.AddItem(Guid.NewGuid(), "Small Item", 1, 50m, 0m);

        var invoices = new List<SalesInvoice> { inv1, inv2 };
        invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(invoices.AsQueryable()));
        invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(invoices.AsQueryable()));
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer>().AsQueryable()));
        consolRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<EInvoiceConsolidation, bool>>>())
            .Returns(Task.FromResult(new List<EInvoiceConsolidation>()));

        var insertedInvoices = new List<SalesInvoice>();
        await invoiceRepo.InsertAsync(Arg.Do<SalesInvoice>(si => insertedInvoices.Add(si)));

        var service = new EInvoiceConsolidationService(invoiceRepo, consolRepo, customerRepo, guidGen);
        var result = await service.ConsolidateInvoicesAsync(new List<Guid> { inv1.Id, inv2.Id }, CompanyId);

        result.Count.ShouldBe(2); // 1 single split invoice + 1 consolidated invoice for remainder
        var splitInvoice = insertedInvoices.FirstOrDefault(i => !i.IsConsolidated);
        splitInvoice.ShouldNotBeNull();
        splitInvoice!.BuyerTin.ShouldBe(EInvoiceConsts.GenericBuyerTin);
        splitInvoice.IsConsolidated.ShouldBeFalse();
        splitInvoice.EInvoiceDocType.ShouldBe(EInvoiceDocumentType.Invoice);
        splitInvoice.EInvoiceStatus.ShouldBe(EInvoiceStatus.NotSubmitted);
    }

    [Fact]
    public async Task EInvoiceConsolidationService_ConsolidateInvoicesAsync_RejectsB2BInvoices_WithCodeEInvoice005()
    {
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var consolRepo = Substitute.For<IRepository<EInvoiceConsolidation, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var guidGen = Substitute.For<IGuidGenerator>();

        var inv1 = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SINV-001", DateTime.UtcNow)
        {
            BuyerTin = "C12345678000" // Real B2B TIN
        };
        inv1.AddItem(Guid.NewGuid(), "Item 1", 1, 100m, 0m);

        var inv2 = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SINV-002", DateTime.UtcNow);
        inv2.AddItem(Guid.NewGuid(), "Item 2", 1, 200m, 0m);

        var invoices = new List<SalesInvoice> { inv1, inv2 };
        invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(invoices.AsQueryable()));
        invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(invoices.AsQueryable()));
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer>().AsQueryable()));

        var service = new EInvoiceConsolidationService(invoiceRepo, consolRepo, customerRepo, guidGen);
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ConsolidateInvoicesAsync(new List<Guid> { inv1.Id, inv2.Id }, CompanyId));

        ex.Code.ShouldBe("EInvoice:005");
    }

    // === 2) Subcontracting Receipt Return Dependant Detail Check (PR #64797 / 51e5ba7ec9) ===

    [Fact]
    public void SubcontractingManager_IsReturnedSuppliedItem_IdentifiesRawMaterialReturnWithoutDatabaseQuery_PR64797()
    {
        var dependentDetailNo = Guid.NewGuid();

        // 1) Valid Subcontracting Receipt return inward entry (actual_qty > 0 with dependant_sle_voucher_detail_no)
        SubcontractingManager.IsReturnedSuppliedItem("SubcontractingReceipt", 5m, dependentDetailNo).ShouldBeTrue();
        SubcontractingManager.IsReturnedSuppliedItem("Subcontracting Receipt", 5m, dependentDetailNo).ShouldBeTrue();

        // 2) Outward consumption entry (actual_qty < 0) is NOT a returned supplied item
        SubcontractingManager.IsReturnedSuppliedItem("SubcontractingReceipt", -5m, dependentDetailNo).ShouldBeFalse();

        // 3) Missing dependent voucher detail is NOT a returned supplied item
        SubcontractingManager.IsReturnedSuppliedItem("SubcontractingReceipt", 5m, null).ShouldBeFalse();
        SubcontractingManager.IsReturnedSuppliedItem("SubcontractingReceipt", 5m, Guid.Empty).ShouldBeFalse();

        // 4) Other voucher types are NOT returned supplied items
        SubcontractingManager.IsReturnedSuppliedItem("PurchaseReceipt", 5m, dependentDetailNo).ShouldBeFalse();
        SubcontractingManager.IsReturnedSuppliedItem("StockEntry", 5m, dependentDetailNo).ShouldBeFalse();

        // 5) Overload taking StockLedgerEntry
        var inwardReturnSle = new StockLedgerEntry(
            Guid.NewGuid(), CompanyId, Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, TimeSpan.Zero, 5m, 10m, 5m, 50m,
            voucherType: "SubcontractingReceipt", voucherId: Guid.NewGuid(),
            voucherDetailNo: Guid.NewGuid())
        {
            DependantSleVoucherDetailNo = dependentDetailNo
        };
        SubcontractingManager.IsReturnedSuppliedItem(inwardReturnSle).ShouldBeTrue();

        var consumptionSle = new StockLedgerEntry(
            Guid.NewGuid(), CompanyId, Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, TimeSpan.Zero, -5m, 10m, 0m, 0m,
            voucherType: "SubcontractingReceipt", voucherId: Guid.NewGuid(),
            voucherDetailNo: Guid.NewGuid());
        SubcontractingManager.IsReturnedSuppliedItem(consumptionSle).ShouldBeFalse();
    }
}
