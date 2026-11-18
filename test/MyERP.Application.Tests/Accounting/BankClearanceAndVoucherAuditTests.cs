using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
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

    [Fact]
    public async Task PaymentPeriodBasedOnInvoiceDate_AgesPleAndPosPaymentsAndValidatesFilters()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var peRepo = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var pleRepo = GetRequiredService<IRepository<PaymentLedgerEntry, Guid>>();
            var reportingAppService = GetRequiredService<IReportingAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Payment Period Co") { CurrencyCode = "MYR" }, autoSave: true);
            var bankGl = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-PP", "Maybank", AccountType.Asset) { AccountSubType = AccountSubType.BankAccount }, autoSave: true);
            var arGl = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1200-PP", "Debtors", AccountType.Asset) { AccountSubType = AccountSubType.AccountsReceivable }, autoSave: true);
            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Beta Trading Sdn Bhd"), autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SRV-PP-01", "Consulting", ItemType.Service) { MaintainStock = false }, autoSave: true);

            // Standard invoice issued Sept 20, due Oct 20, paid Nov 10 -> Age = 51 days (31-60 bucket), Delay = 21 days
            var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SINV-PP-001", new DateTime(2026, 9, 20))
            {
                DueDate = new DateTime(2026, 10, 20),
                GrandTotal = 1000m,
                BaseGrandTotal = 1000m,
            };
            si.AddItem(item.Id, "Service", 1m, 1000m, 0m);
            si.Submit();
            si.Post();
            await siRepo.InsertAsync(si, autoSave: true);

            var pe = new PaymentEntry(Guid.NewGuid(), company.Id, PaymentType.Receive,
                new DateTime(2026, 11, 10), 600m, arGl.Id, bankGl.Id)
            {
                PaymentNumber = "PE-PP-001",
                PartyType = "Customer",
                PartyId = customer.Id,
                Remarks = "Partial settlement"
            };
            pe.Submit();
            pe.Post();
            await peRepo.InsertAsync(pe, autoSave: true);

            await pleRepo.InsertAsync(new PaymentLedgerEntry(
                Guid.NewGuid(), company.Id, new DateTime(2026, 11, 10),
                arGl.Id, "Customer", customer.Id,
                "PaymentEntry", pe.Id, "SalesInvoice", si.Id,
                -600m, -600m, "MYR"), autoSave: true);

            // POS invoice paid immediately Nov 15 -> Age = 0 days (0-30 bucket), Delay = 0
            var posSi = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "POS-PP-001", new DateTime(2026, 11, 15))
            {
                IsPos = true,
                AmountPaid = 250m,
                GrandTotal = 250m,
                BaseGrandTotal = 250m,
                Notes = "Counter sale"
            };
            posSi.AddItem(item.Id, "Service", 1m, 250m, 0m);
            posSi.Submit();
            posSi.Post();
            await siRepo.InsertAsync(posSi, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                reportingAppService.GetPaymentPeriodBasedOnInvoiceDateAsync(new PaymentPeriodBasedOnInvoiceDateRequestDto
                {
                    CompanyId = company.Id,
                    PaymentType = "Incoming",
                    PartyType = "Supplier",
                }));

            var report = await reportingAppService.GetPaymentPeriodBasedOnInvoiceDateAsync(new PaymentPeriodBasedOnInvoiceDateRequestDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 11, 1),
                ToDate = new DateTime(2026, 11, 30),
                PaymentType = "Incoming",
            });

            report.CurrencyCode.ShouldBe("MYR");
            report.Rows.Count.ShouldBe(2);
            report.TotalAmount.ShouldBe(850m);
            report.TotalRange0To30.ShouldBe(250m);
            report.TotalRange31To60.ShouldBe(600m);
            report.TotalRange61To90.ShouldBe(0m);
            report.TotalRange90Above.ShouldBe(0m);

            var peRow = report.Rows[0];
            peRow.PaymentDocumentType.ShouldBe("PaymentEntry");
            peRow.PaymentDocumentNo.ShouldBe("PE-PP-001");
            peRow.PartyName.ShouldBe("Beta Trading Sdn Bhd");
            peRow.InvoiceNo.ShouldBe("SINV-PP-001");
            peRow.Age.ShouldBe(51);
            peRow.Range31To60.ShouldBe(600m);
            peRow.DelayInPaymentDays.ShouldBe(21);

            var posRow = report.Rows[1];
            posRow.PaymentDocumentType.ShouldBe("SalesInvoice");
            posRow.PaymentDocumentNo.ShouldBe("POS-PP-001");
            posRow.InvoiceNo.ShouldBe("POS-PP-001");
            posRow.Age.ShouldBe(0);
            posRow.Range0To30.ShouldBe(250m);
        });
    }
}
