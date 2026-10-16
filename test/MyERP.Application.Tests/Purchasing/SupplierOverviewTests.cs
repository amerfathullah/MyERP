using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class SupplierOverviewTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetSupplierOverviewAsync_NonExistentSupplier_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var supplierService = GetRequiredService<ISupplierAppService>();
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Overview Test Co 1"), autoSave: true);

            await Should.ThrowAsync<EntityNotFoundException>(() =>
                supplierService.GetSupplierOverviewAsync(new GetSupplierOverviewInputDto
                {
                    SupplierId = Guid.NewGuid(),
                    CompanyId = company.Id
                }));
        });
    }

    [Fact]
    public async Task GetSupplierOverviewAsync_WithInvoicesAndOrders_CalculatesCorrectMetrics()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var piRepo = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var peRepo = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var sqRepo = GetRequiredService<IRepository<SupplierQuotation, Guid>>();
            var supplierService = GetRequiredService<ISupplierAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Supplier Overview Test Co") { CurrencyCode = "MYR" }, autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Supplier Overview Test Supp"), autoSave: true);

            var today = DateTime.UtcNow.Date;

            // 1. Purchase Invoice: 1000 grand total, 200 paid, 800 outstanding, overdue by 5 days
            var pi1 = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PINV-OV-001", today.AddDays(-15));
            pi1.DueDate = today.AddDays(-5);
            pi1.AddItem(Guid.NewGuid(), "Item 1", 1, 1000m, 0m);
            pi1.BaseNetTotal = 1000m;
            pi1.BaseGrandTotal = 1000m;
            pi1.Submit();
            pi1.Post();
            pi1.AmountPaid = 200m;
            await piRepo.InsertAsync(pi1, autoSave: true);

            // 2. Purchase Order: qty 2, price 300, total 600. 1 received (50%), 0 billed
            var po1 = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-OV-001", today.AddDays(-3));
            var poItem = po1.AddItem(Guid.NewGuid(), "Item 1", 2, 300m, 0m);
            poItem.ReceivedQty = 1m;
            poItem.ExpectedDeliveryDate = today.AddDays(5);
            po1.Submit();
            await poRepo.InsertAsync(po1, autoSave: true);

            // 3. Payment Entry: 150 unallocated advance
            var pe1 = new PaymentEntry(Guid.NewGuid(), company.Id, PaymentType.Pay, today.AddDays(-2), 150m, Guid.NewGuid(), Guid.NewGuid());
            pe1.PartyType = "Supplier";
            pe1.PartyId = supplier.Id;
            pe1.Submit();
            await peRepo.InsertAsync(pe1, autoSave: true);

            // 4. Supplier Quotation: 450 grand total, submitted
            var sq1 = new SupplierQuotation(Guid.NewGuid(), company.Id, supplier.Id, today.AddDays(-1))
            {
                QuotationNumber = "SQ-OV-001",
                ValidTill = today.AddDays(3)
            };
            sq1.AddItem(Guid.NewGuid(), 1, 450m);
            sq1.Submit();
            await sqRepo.InsertAsync(sq1, autoSave: true);

            var overview = await supplierService.GetSupplierOverviewAsync(new GetSupplierOverviewInputDto
            {
                SupplierId = supplier.Id,
                CompanyId = company.Id,
                Period = "Last 12 months"
            });

            overview.ShouldNotBeNull();
            overview.SupplierName.ShouldBe("Supplier Overview Test Supp");
            overview.Currency.ShouldBe("MYR");

            // Position checks
            overview.Position.NetPurchases.Value.ShouldBe(1000m);
            overview.Position.NetPurchases.Count.ShouldBe(1);
            overview.Position.Outstanding.Value.ShouldBe(800m);
            overview.Position.Outstanding.UnpaidCount.ShouldBe(1);
            overview.Position.Overdue.Value.ShouldBe(800m);
            overview.Position.Overdue.Count.ShouldBe(1);
            overview.Position.Advances.Value.ShouldBe(150m);

            // Pipeline checks
            overview.Pipeline.Quotations.Value.ShouldBe(450m);
            overview.Pipeline.Quotations.Count.ShouldBe(1);
            overview.Pipeline.Quotations.Expiring.ShouldBe(1); // valid till today + 3 is within 7 days
            overview.Pipeline.Receipt.Value.ShouldBe(300m); // remaining 1 qty * 300 = 300
            overview.Pipeline.Receipt.Count.ShouldBe(1);
            overview.Pipeline.Billing.Value.ShouldBe(300m); // received 1 - billed 0 = 1 * 300 = 300
            overview.Pipeline.Billing.Count.ShouldBe(1);

            // Ageing checks
            overview.Ageing.Total.ShouldBe(800m);
            overview.Ageing.Overdue.ShouldBe(800m);
            overview.Ageing.OverduePct.ShouldBe(100m);

            overview.UnallocatedAdvances.ShouldBe(150m);
        });
    }

    [Fact]
    public async Task GetSupplierTransactionsAsync_ReturnsOrderedTransactions()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var piRepo = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var peRepo = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var supplierService = GetRequiredService<ISupplierAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Trans Test Co"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Trans Test Supp"), autoSave: true);

            var today = DateTime.UtcNow.Date;

            var po = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-TR-001", today.AddDays(-5));
            po.AddItem(Guid.NewGuid(), "Item", 1, 500m, 0m);
            po.Submit();
            await poRepo.InsertAsync(po, autoSave: true);

            var pi = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PINV-TR-001", today.AddDays(-3));
            pi.AddItem(Guid.NewGuid(), "Item", 1, 500m, 0m);
            pi.BaseNetTotal = 500m;
            pi.BaseGrandTotal = 500m;
            pi.Submit();
            pi.Post();
            pi.AmountPaid = 100m;
            await piRepo.InsertAsync(pi, autoSave: true);

            var pe = new PaymentEntry(Guid.NewGuid(), company.Id, PaymentType.Pay, today.AddDays(-1), 100m, Guid.NewGuid(), Guid.NewGuid())
            {
                PaymentNumber = "PE-TR-001",
                PartyType = "Supplier",
                PartyId = supplier.Id
            };
            pe.Submit();
            await peRepo.InsertAsync(pe, autoSave: true);

            var all = await supplierService.GetSupplierTransactionsAsync(new GetSupplierTransactionsInputDto
            {
                SupplierId = supplier.Id,
                CompanyId = company.Id,
                DocType = "All"
            });

            all.Count.ShouldBe(3);
            all[0].DocType.ShouldBe("Payment Entry");
            all[1].DocType.ShouldBe("Purchase Invoice");
            all[2].DocType.ShouldBe("Purchase Order");

            var invoicesOnly = await supplierService.GetSupplierTransactionsAsync(new GetSupplierTransactionsInputDto
            {
                SupplierId = supplier.Id,
                CompanyId = company.Id,
                DocType = "Purchase Invoice"
            });

            invoicesOnly.Count.ShouldBe(1);
            invoicesOnly[0].TransactionNumber.ShouldBe("PINV-TR-001");
            invoicesOnly[0].OutstandingAmount.ShouldBe(400m);
        });
    }

    [Fact]
    public async Task GetSupplierCompanyIdsAsync_ReturnsAssociatedCompanyIds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var supplierService = GetRequiredService<ISupplierAppService>();

            var c1 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Co 1"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), c1.Id, "Co 1 Supp"), autoSave: true);

            var po = new PurchaseOrder(Guid.NewGuid(), c1.Id, supplier.Id, "PO-MCO-001", DateTime.UtcNow);
            po.AddItem(Guid.NewGuid(), "Item", 1, 100m, 0m);
            po.Submit();
            await poRepo.InsertAsync(po, autoSave: true);

            var companyIds = await supplierService.GetSupplierCompanyIdsAsync(supplier.Id);

            companyIds.Count.ShouldBe(1);
            companyIds.ShouldContain(c1.Id);
        });
    }
}
