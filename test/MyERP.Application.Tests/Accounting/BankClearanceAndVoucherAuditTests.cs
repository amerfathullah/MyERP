using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// EF Core integration tests for Bank Clearance Summary & Voucher-wise Balance reports.
/// </summary>
public abstract class BankClearanceAndVoucherAuditTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task BankClearanceSummary_ResolvesCustomerAgainstAccountAndIncludesClearedEntries()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var peRepo = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var bankClearanceAppService = GetRequiredService<IBankClearanceAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Clearance Summary Co") { CurrencyCode = "MYR" }, autoSave: true);
            var bankGl = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-BCS", "Maybank", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    Currency = "MYR"
                }, autoSave: true);
            var arGl = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1200-BCS", "Debtors", AccountType.Asset), autoSave: true);
            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Acme Malaysia Sdn Bhd"), autoSave: true);

            var pe = new PaymentEntry(Guid.NewGuid(), company.Id, PaymentType.Receive,
                new DateTime(2026, 11, 10), 850m, arGl.Id, bankGl.Id)
            {
                PaymentNumber = "PE-BCS-001",
                ReferenceNumber = "CHQ-BCS-1",
                PartyType = "Customer",
                PartyId = customer.Id,
                ReceivedAmount = 850m,
            };
            pe.Submit();
            pe.Post();
            pe.SetClearanceDate(new DateTime(2026, 11, 12));
            await peRepo.InsertAsync(pe, autoSave: true);

            var entries = await bankClearanceAppService.GetEntriesAsync(new GetBankClearanceEntriesInput
            {
                CompanyId = company.Id,
                BankAccountId = bankGl.Id,
                FromDate = new DateTime(2026, 11, 1),
                ToDate = new DateTime(2026, 11, 30),
                IncludeCleared = true
            });

            entries.Count.ShouldBe(1);
            entries[0].DocumentType.ShouldBe("PaymentEntry");
            entries[0].DocumentNumber.ShouldBe("PE-BCS-001");
            entries[0].SourceDocumentId.ShouldBe(pe.Id);
            entries[0].AgainstAccount.ShouldBe("Acme Malaysia Sdn Bhd");
            entries[0].Amount.ShouldBe(850m);
            entries[0].ClearanceDate.ShouldBe(new DateTime(2026, 11, 12));
        });
    }

    [Fact]
    public async Task VoucherWiseBalance_DetectsUnbalancedPostedVoucher_AndExcludesBalancedVouchers()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var jeRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var jeLineRepo = GetRequiredService<IRepository<JournalEntryLine, Guid>>();
            var reportingAppService = GetRequiredService<IReportingAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Voucher Audit Co") { CurrencyCode = "MYR" }, autoSave: true);
            var fy = await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY-AUDIT",
                    new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            var cashGl = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1100-VA", "Cash", AccountType.Asset), autoSave: true);
            var salesGl = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4100-VA", "Sales", AccountType.Revenue), autoSave: true);

            // Balanced voucher -> must NOT appear
            var balancedJe = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 11, 12))
            {
                EntryNumber = "JE-BAL-001",
            };
            balancedJe.AddLineWithDimensions(cashGl.Id, 200m, true);
            balancedJe.AddLineWithDimensions(salesGl.Id, 200m, false);
            balancedJe.Post();
            await jeRepo.InsertAsync(balancedJe, autoSave: true);

            // Unbalanced voucher (posted JE + extra unoffset line simulating ledger corruption) -> MUST appear
            var brokenJe = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 11, 14))
            {
                EntryNumber = "JE-ERR-001",
                ReferenceType = "SalesInvoice",
                ReferenceId = Guid.NewGuid(),
                ReferenceNumber = "SI-ERR-001",
            };
            brokenJe.AddLineWithDimensions(cashGl.Id, 400m, true);
            brokenJe.AddLineWithDimensions(salesGl.Id, 400m, false);
            brokenJe.Post();
            await jeRepo.InsertAsync(brokenJe, autoSave: true);

            await jeLineRepo.InsertAsync(
                new JournalEntryLine(Guid.NewGuid(), brokenJe.Id, cashGl.Id, 75m, true, "Corrupt orphan debit"),
                autoSave: true);

            var report = await reportingAppService.GetVoucherWiseBalanceAsync(new VoucherWiseBalanceRequestDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 11, 1),
                ToDate = new DateTime(2026, 11, 30),
            });

            report.CurrencyCode.ShouldBe("MYR");
            report.UnbalancedCount.ShouldBe(1);
            report.Rows.Count.ShouldBe(1);
            report.Rows[0].VoucherType.ShouldBe("SalesInvoice");
            report.Rows[0].VoucherNo.ShouldBe("SI-ERR-001");
            report.Rows[0].Debit.ShouldBe(475m);
            report.Rows[0].Credit.ShouldBe(400m);
            report.Rows[0].Difference.ShouldBe(75m);
        });
    }
}
