using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for PurchaseInvoiceManager.UpdateLinkedPurchaseReceiptBillingAsync's
/// AmountDifferenceWithPurchaseInvoice tracking (75th migration session — "PR&lt;-&gt;PI billing
/// rate variance tracking" backlog item, informational-only scope; see
/// PurchaseReceiptItem.AmountDifferenceWithPurchaseInvoice's own doc comment for what's
/// deliberately NOT in scope — the adjust-incoming-rate + revaluation cascade).
/// </summary>
public abstract class PurchaseReceiptBillingVarianceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task Billing_At_Higher_Rate_Than_Receipt_Accumulates_Positive_Variance()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var prRepository = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var piManager = GetRequiredService<PurchaseInvoiceManager>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PR-PI Variance Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Test Supplier"), autoSave: true);
            var itemId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();

            var pr = new PurchaseReceipt(Guid.NewGuid(), company.Id, supplier.Id, warehouseId, "PR-0001", DateTime.Today);
            pr.AddItem(itemId, "Widget", quantity: 10m, unitPrice: 5.00m, taxAmount: 0m);
            await prRepository.InsertAsync(pr, autoSave: true);

            var pi = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PI-0001", DateTime.Today);
            pi.AddItem(itemId, "Widget", quantity: 10m, unitPrice: 5.50m, taxAmount: 0m);
            pi.Items.Single().PurchaseReceiptItemId = pr.Items.Single().Id;

            await piManager.UpdateLinkedPurchaseReceiptBillingAsync(pi);

            var updatedPr = await prRepository.GetAsync(pr.Id);
            var prItem = updatedPr.Items.Single();

            prItem.BilledQty.ShouldBe(10m);
            // (5.50 - 5.00) × 10 = 5.00
            prItem.AmountDifferenceWithPurchaseInvoice.ShouldBe(5.00m);
        });
    }

    [Fact]
    public async Task Cancelling_The_Invoice_Reverses_Both_BilledQty_And_Variance()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var prRepository = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var piManager = GetRequiredService<PurchaseInvoiceManager>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PR-PI Variance Co 2"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Test Supplier 2"), autoSave: true);
            var itemId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();

            var pr = new PurchaseReceipt(Guid.NewGuid(), company.Id, supplier.Id, warehouseId, "PR-0002", DateTime.Today);
            pr.AddItem(itemId, "Widget", quantity: 20m, unitPrice: 8.00m, taxAmount: 0m);
            await prRepository.InsertAsync(pr, autoSave: true);

            var pi = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PI-0002", DateTime.Today);
            pi.AddItem(itemId, "Widget", quantity: 20m, unitPrice: 7.25m, taxAmount: 0m); // billed LOWER than receipt
            pi.Items.Single().PurchaseReceiptItemId = pr.Items.Single().Id;

            await piManager.UpdateLinkedPurchaseReceiptBillingAsync(pi);

            var afterBill = (await prRepository.GetAsync(pr.Id)).Items.Single();
            afterBill.BilledQty.ShouldBe(20m);
            // (7.25 - 8.00) × 20 = -15.00
            afterBill.AmountDifferenceWithPurchaseInvoice.ShouldBe(-15.00m);

            await piManager.UpdateLinkedPurchaseReceiptBillingAsync(pi, reverse: true);

            var afterCancel = (await prRepository.GetAsync(pr.Id)).Items.Single();
            afterCancel.BilledQty.ShouldBe(0m);
            afterCancel.AmountDifferenceWithPurchaseInvoice.ShouldBe(0m);
        });
    }
}
