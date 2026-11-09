using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.EInvoice;
using MyERP.EInvoice.Services;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 19 upstream parity:
/// 1) E-Invoice B2C Consolidation Buyer TIN &amp; Document Metadata (LHDN Guideline Section 3.7 / myinvois consolidate_invoice.py).
/// 2) StockLedgerEntry DependantSleVoucherDetailNo persistence &amp; return detection (PR #64797 / commits fee9cb4b0e &amp; 51e5ba7ec9).
/// </summary>
public abstract class UpstreamBatch19IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task StockValuationService_CreateLedgerEntryAsync_PersistsDependantSleVoucherDetailNo_PR64797()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var valuationService = GetRequiredService<StockValuationService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 19 SLE Co"), autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-B19", "Raw Material Item", ItemType.Goods) { MaintainStock = true }, autoSave: true);
            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Main Warehouse") { WarehouseCode = "WH-B19" }, autoSave: true);

            var voucherDetailNo = Guid.NewGuid();
            var dependantDetailNo = Guid.NewGuid();
            var voucherId = Guid.NewGuid();

            var sle = await valuationService.CreateLedgerEntryAsync(
                company.Id,
                item.Id,
                warehouse.Id,
                DateTime.UtcNow,
                quantityChange: 10m,
                incomingRate: 50m,
                voucherType: "SubcontractingReceipt",
                voucherId: voucherId,
                voucherDetailNo: voucherDetailNo,
                dependantSleVoucherDetailNo: dependantDetailNo);

            sle.ShouldNotBeNull();
            sle.VoucherDetailNo.ShouldBe(voucherDetailNo);
            sle.DependantSleVoucherDetailNo.ShouldBe(dependantDetailNo);

            var uowManager = GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();
            await uowManager.Current!.SaveChangesAsync();

            // Verify read-back from repository
            var saved = await sleRepo.GetAsync(sle.Id);
            saved.VoucherDetailNo.ShouldBe(voucherDetailNo);
            saved.DependantSleVoucherDetailNo.ShouldBe(dependantDetailNo);

            // Verify SubcontractingManager detection helper
            SubcontractingManager.IsReturnedSuppliedItem(saved).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task EInvoiceConsolidationService_Integration_ConsolidatesInvoicesWithGenericBuyerTinAndMetadata()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var invoiceRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var consolidationService = GetRequiredService<EInvoiceConsolidationService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 19 Consol Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "B2C Consumer") { CustomerCode = "CUST-B19-01" }, autoSave: true);

            var inv1 = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SINV-B19-001", DateTime.UtcNow);
            inv1.AddItem(Guid.NewGuid(), "Item A", 2, 100m, 0m);
            await invoiceRepo.InsertAsync(inv1, autoSave: true);

            var inv2 = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SINV-B19-002", DateTime.UtcNow);
            inv2.AddItem(Guid.NewGuid(), "Item B", 1, 300m, 0m);
            await invoiceRepo.InsertAsync(inv2, autoSave: true);

            var createdIds = await consolidationService.ConsolidateInvoicesAsync(
                new List<Guid> { inv1.Id, inv2.Id }, company.Id);

            createdIds.ShouldNotBeEmpty();
            var consolId = createdIds[0];

            var uowManager = GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();
            await uowManager.Current!.SaveChangesAsync();

            var consolidatedInvoice = await invoiceRepo.GetAsync(consolId);
            consolidatedInvoice.ShouldNotBeNull();
            consolidatedInvoice.BuyerTin.ShouldBe(EInvoiceConsts.GenericBuyerTin);
            consolidatedInvoice.IsConsolidated.ShouldBeTrue();
            consolidatedInvoice.EInvoiceDocType.ShouldBe(EInvoiceDocumentType.Invoice);
            consolidatedInvoice.EInvoiceStatus.ShouldBe(EInvoiceStatus.NotSubmitted);

            // Re-read original invoices
            var updatedInv1 = await invoiceRepo.GetAsync(inv1.Id);
            var updatedInv2 = await invoiceRepo.GetAsync(inv2.Id);
            updatedInv1.ConsolidatedSalesInvoiceId.ShouldBe(consolId);
            updatedInv2.ConsolidatedSalesInvoiceId.ShouldBe(consolId);
        });
    }
}
