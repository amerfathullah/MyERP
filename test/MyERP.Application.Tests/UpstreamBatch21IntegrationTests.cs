using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 21 upstream parity:
/// 1) Sales Order advance payment status lifecycle (PR #64511 / commit 97de257a13).
/// 2) Payment Entry Reference Handover on Reconciliation (PR #64511 / commit 97de257a13).
/// </summary>
public abstract class UpstreamBatch21IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SalesOrder_AdvancePaymentStatus_FullLifecycleTransitions_FollowUpstreamInvariant()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var prAppService = GetRequiredService<IPaymentRequestAppService>();
            var peAppService = GetRequiredService<IPaymentEntryAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 21 Advance Co") { CurrencyCode = "MYR" }, autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Customer B21"), autoSave: true);

            var bankAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1200", "Bank Account", AccountType.Asset) { Currency = "MYR" }, autoSave: true);
            var recAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1300", "Debtors", AccountType.Asset) { Currency = "MYR" }, autoSave: true);

            company.DefaultReceivableAccountId = recAcc.Id;
            company.DefaultBankAccountId = bankAcc.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PaymentEntry", "PaymentEntry", "PE-2026-"), autoSave: true);

            // Step 1: Create and submit Sales Order -> status starts at "Not Requested"
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-B21-001", DateTime.UtcNow)
            {
                CurrencyCode = "MYR",
                GrandTotal = 500m,
                NetTotal = 500m
            };
            so.AddItem(Guid.NewGuid(), "Service item", 1m, 500m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            so.AdvancePaymentStatus.ShouldBe("Not Requested");

            // Step 2: Create Payment Request against the Sales Order
            var prDto = await prAppService.CreateAsync(new CreatePaymentRequestDto
            {
                CompanyId = company.Id,
                ReferenceDoctype = "SalesOrder",
                ReferenceId = so.Id,
                PartyType = "Customer",
                PartyId = customer.Id,
                GrandTotal = 500m,
                Currency = "MYR"
            });

            // Submit Payment Request -> SO advance payment status becomes "Requested"
            await prAppService.SubmitAsync(prDto.Id);

            var reloadedSo = await soRepo.GetAsync(so.Id);
            reloadedSo.AdvancePaymentStatus.ShouldBe("Requested");

            // Step 3: Pay Payment Request -> creates Payment Entry -> SO status becomes "Fully Paid"
            var paidPr = await prAppService.PayAsync(prDto.Id);
            paidPr.PaymentEntryId.ShouldNotBeNull();

            reloadedSo = await soRepo.GetAsync(so.Id);
            reloadedSo.AdvancePaymentStatus.ShouldBe("Fully Paid");

            // Step 4: Cancel Payment Entry -> Payment Request reopens to "Initiated" -> SO status reverts to "Requested"
            await peAppService.CancelAsync(paidPr.PaymentEntryId!.Value);

            var prEntity = await GetRequiredService<IRepository<PaymentRequest, Guid>>().GetAsync(prDto.Id);
            prEntity.Status.ShouldBe(PaymentRequestStatus.Initiated);

            reloadedSo = await soRepo.GetAsync(so.Id);
            reloadedSo.AdvancePaymentStatus.ShouldBe("Requested");

            // Step 5: Cancel Payment Request -> SO status resets to "Not Requested"
            await prAppService.CancelAsync(prDto.Id);

            reloadedSo = await soRepo.GetAsync(so.Id);
            reloadedSo.AdvancePaymentStatus.ShouldBe("Not Requested");
        });
    }

    [Fact]
    public async Task PaymentReconciliation_AdvanceHandover_RetainsPaymentRequestIdAndAdvanceVoucherDetails()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var peRepo = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var pleRepo = GetRequiredService<IRepository<PaymentLedgerEntry, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var reconAppService = GetRequiredService<IPaymentReconciliationAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 21 Recon Co") { CurrencyCode = "MYR" }, autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Customer Recon B21"), autoSave: true);

            var bankAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1200", "Bank", AccountType.Asset) { Currency = "MYR" }, autoSave: true);
            var recAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1300", "Debtors", AccountType.Asset) { Currency = "MYR" }, autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PaymentEntry", "PaymentEntry", "PE-2026-"), autoSave: true);

            // Create Sales Order
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-RECON-001", DateTime.UtcNow)
            {
                CurrencyCode = "MYR",
                GrandTotal = 1000m,
                NetTotal = 1000m
            };
            so.AddItem(Guid.NewGuid(), "Item", 1m, 1000m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // Create Payment Entry with advance reference linked to PaymentRequest
            var dummyPrId = Guid.NewGuid();
            var pe = new PaymentEntry(
                Guid.NewGuid(), company.Id, PaymentType.Receive, DateTime.UtcNow.Date,
                1000m, recAcc.Id, bankAcc.Id)
            {
                PartyType = "Customer",
                PartyId = customer.Id,
                ReceivedAmount = 1000m,
                PaymentNumber = "PE-RECON-001"
            };
            pe.References.Add(new PaymentEntryReference(
                Guid.NewGuid(), pe.Id, "SalesOrder", so.Id,
                totalAmount: 1000m, outstandingAmount: 1000m, allocatedAmount: 1000m, referenceNumber: "SO-RECON-001")
            {
                PaymentRequestId = dummyPrId
            });
            pe.Submit();
            pe.Post();
            await peRepo.InsertAsync(pe, autoSave: true);

            // Create Sales Invoice for 600m
            var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-RECON-001", DateTime.UtcNow)
            {
                CurrencyCode = "MYR",
                GrandTotal = 600m,
                DebitToAccountId = recAcc.Id
            };
            si.AddItem(Guid.NewGuid(), "Item", 1m, 600m, 0m);
            si.Submit();
            await siRepo.InsertAsync(si, autoSave: true);

            // Seed initial PLE for Sales Invoice so GetOutstandingAsync returns 600m
            await pleRepo.InsertAsync(new PaymentLedgerEntry(
                Guid.NewGuid(), company.Id, DateTime.UtcNow.Date,
                recAcc.Id, "Customer", customer.Id,
                "SalesInvoice", si.Id, "SalesInvoice", si.Id,
                amount: 600m, amountInAccountCurrency: 600m, accountCurrency: "MYR"), autoSave: true);

            // Execute batch reconciliation: allocate 600m from PE to SI
            await reconAppService.ReconcileAsync(new ReconcilePaymentDto
            {
                CompanyId = company.Id,
                PartyType = "Customer",
                PartyId = customer.Id,
                AccountId = recAcc.Id,
                Allocations = new List<ReconcileAllocationDto>
                {
                    new ReconcileAllocationDto
                    {
                        PaymentVoucherType = "PaymentEntry",
                        PaymentVoucherId = pe.Id,
                        InvoiceVoucherType = "SalesInvoice",
                        InvoiceVoucherId = si.Id,
                        AllocatedAmount = 600m
                    }
                }
            });

            // Reload PE and verify references
            var reloadedPe = (await peRepo.WithDetailsAsync(p => p.References)).First(p => p.Id == pe.Id);

            // Advance reference was reduced from 1000 to 400
            var advanceRef = reloadedPe.References.FirstOrDefault(r => r.ReferenceType == "SalesOrder");
            advanceRef.ShouldNotBeNull();
            advanceRef.AllocatedAmount.ShouldBe(400m);

            // Invoice reference row was created for 600m and retained PaymentRequestId & Advance Voucher details
            var invoiceRef = reloadedPe.References.FirstOrDefault(r => r.ReferenceType == "SalesInvoice");
            invoiceRef.ShouldNotBeNull();
            invoiceRef.AllocatedAmount.ShouldBe(600m);
            invoiceRef.AdvanceVoucherType.ShouldBe("SalesOrder");
            invoiceRef.AdvanceVoucherId.ShouldBe(so.Id);
            invoiceRef.PaymentRequestId.ShouldBe(dummyPrId);
        });
    }
}
