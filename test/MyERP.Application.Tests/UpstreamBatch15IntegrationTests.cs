using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 15 upstream ERPNext parity:
/// 1) Payment Entry references preserve PaymentRequestId, AdvanceVoucherType, AdvanceVoucherId (PR #64511 / commit 97de257a13).
/// 2) Stock Ledger Entry stores and filters by ProjectId (PR #64652 / commit 6b9f2c01d6).
/// </summary>
public abstract class UpstreamBatch15IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task PaymentEntryAppService_PreservesPaymentRequestAndAdvanceVoucherOnCreate_97de257a13()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var peAppService = GetRequiredService<IPaymentEntryAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 15 Test Co"), autoSave: true);
            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PE Series B15", "PaymentEntry", "PE-B15-"), autoSave: true);
            var bankAccount = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1120-B15", "Batch 15 Bank", AccountType.Asset) { AccountSubType = AccountSubType.BankAccount }, autoSave: true);
            var receivableAccount = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130-B15", "Batch 15 Debtors", AccountType.Asset), autoSave: true);

            var invoiceId = Guid.NewGuid();
            var paymentRequestId = Guid.NewGuid();
            var advanceVoucherId = Guid.NewGuid();

            var createdPe = await peAppService.CreateAsync(new CreatePaymentEntryDto
            {
                CompanyId = company.Id,
                PaymentType = PaymentType.Receive,
                PostingDate = DateTime.UtcNow,
                PaidAmount = 500m,
                ReceivedAmount = 500m,
                PaidFromAccountId = receivableAccount.Id,
                PaidToAccountId = bankAccount.Id,
                ReferenceNumber = "REF-B15-001",
                ReferenceDate = DateTime.UtcNow.Date,
                References = new List<PaymentReferenceDto>
                {
                    new()
                    {
                        ReferenceType = "SalesInvoice",
                        ReferenceId = invoiceId,
                        AllocatedAmount = 500m,
                        PaymentRequestId = paymentRequestId,
                        AdvanceVoucherType = "SalesOrder",
                        AdvanceVoucherId = advanceVoucherId
                    }
                }
            });

            createdPe.References.Count.ShouldBe(1);
            var refCreated = createdPe.References[0];
            refCreated.ReferenceType.ShouldBe("SalesInvoice");
            refCreated.ReferenceId.ShouldBe(invoiceId);
            refCreated.PaymentRequestId.ShouldBe(paymentRequestId);
            refCreated.AdvanceVoucherType.ShouldBe("SalesOrder");
            refCreated.AdvanceVoucherId.ShouldBe(advanceVoucherId);

            // Fetch through GetAsync
            var fetchedPe = await peAppService.GetAsync(createdPe.Id);
            fetchedPe.References.Count.ShouldBe(1);
            var refFetched = fetchedPe.References[0];
            refFetched.PaymentRequestId.ShouldBe(paymentRequestId);
            refFetched.AdvanceVoucherType.ShouldBe("SalesOrder");
            refFetched.AdvanceVoucherId.ShouldBe(advanceVoucherId);
        });
    }

    [Fact]
    public async Task StockLedgerEntry_StoresAndQueriesByProjectId_6b9f2c01d6()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 15 Inv Co"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-B15", "Batch 15 Test Item", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH-B15"), autoSave: true);

            var projectId = Guid.NewGuid();
            var sle = new StockLedgerEntry(
                id: Guid.NewGuid(),
                companyId: company.Id,
                itemId: item.Id,
                warehouseId: warehouse.Id,
                postingDate: DateTime.UtcNow,
                quantityChange: 10m,
                valuationRate: 50m,
                balanceQuantity: 10m,
                balanceValue: 500m)
            {
                VoucherType = "StockEntry",
                VoucherId = Guid.NewGuid(),
                ProjectId = projectId
            };

            await sleRepo.InsertAsync(sle, autoSave: true);

            var projectEntries = await sleRepo.GetListAsync(x => x.ProjectId == projectId);
            projectEntries.Count.ShouldBe(1);
            projectEntries[0].ItemId.ShouldBe(item.Id);
            projectEntries[0].ProjectId.ShouldBe(projectId);
        });
    }
}
