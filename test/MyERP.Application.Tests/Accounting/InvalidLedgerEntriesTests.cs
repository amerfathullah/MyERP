using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;
using Xunit;

namespace MyERP.Accounting;

public abstract class InvalidLedgerEntriesTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IInvalidLedgerEntriesAppService _reportService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<FiscalYear, Guid> _fyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<JournalEntry, Guid> _jeRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<SalesInvoice, Guid> _siRepository;

    protected InvalidLedgerEntriesTests()
    {
        _reportService = GetRequiredService<IInvalidLedgerEntriesAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _fyRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _jeRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
        _pleRepository = GetRequiredService<IRepository<PaymentLedgerEntry, Guid>>();
        _siRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
    }

    [Fact]
    public async Task Should_Validate_Mandatory_Filters()
    {
        await Should.ThrowAsync<BusinessException>(async () =>
            await _reportService.GetReportAsync(new InvalidLedgerEntriesRequestDto()));

        await Should.ThrowAsync<BusinessException>(async () =>
            await _reportService.GetReportAsync(new InvalidLedgerEntriesRequestDto
            {
                CompanyId = Guid.NewGuid(),
                FromDate = new DateTime(2026, 12, 31),
                ToDate = new DateTime(2026, 1, 1),
            }));
    }

    [Fact]
    public async Task Should_Flag_Cancelled_Deleted_And_Corrupt_PLE_References_With_Account_And_VoucherNo_Filters()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Invalid Ledger Co"), autoSave: true);
        var fy = await _fyRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)),
            autoSave: true);
        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "Invalid Ledger Cust"), autoSave: true);
        var item = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "ILE-ITEM", "Service Item", ItemType.Service), autoSave: true);

        var bank = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1100", "Bank", AccountType.Asset), autoSave: true);
        var debtors = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1200", "Debtors", AccountType.Asset), autoSave: true);
        var income = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "4000", "Sales", AccountType.Revenue), autoSave: true);
        var unrelated = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "5000", "Rent", AccountType.Expense), autoSave: true);

        // 1. Healthy submitted JE referencing a posted SalesInvoice -> MUST NOT be flagged
        var healthySi = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-HEALTHY-1",
            new DateTime(2026, 6, 1)) { DebitToAccountId = debtors.Id };
        healthySi.AddItem(item.Id, "Service", 1m, 500m, 0m);
        healthySi.Submit();
        healthySi.Post();
        await _siRepository.InsertAsync(healthySi, autoSave: true);

        var healthyJe = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 6, 1))
        {
            ReferenceType = "SalesInvoice",
            ReferenceId = healthySi.Id,
            ReferenceNumber = healthySi.InvoiceNumber,
        };
        healthyJe.AddLine(debtors.Id, 500m, true);
        healthyJe.AddLine(income.Id, 500m, false);
        healthyJe.Post();
        await _jeRepository.InsertAsync(healthyJe, autoSave: true);

        // 2. Active posted JE referencing a CANCELLED SalesInvoice -> MUST be flagged with reason Cancelled
        var cancelledSi = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-CORRUPT-2",
            new DateTime(2026, 6, 2)) { DebitToAccountId = debtors.Id };
        cancelledSi.AddItem(item.Id, "Service", 1m, 250m, 0m);
        cancelledSi.Submit();
        cancelledSi.Post();
        cancelledSi.Cancel();
        await _siRepository.InsertAsync(cancelledSi, autoSave: true);

        var orphanJe = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 6, 2))
        {
            ReferenceType = "SalesInvoice",
            ReferenceId = cancelledSi.Id,
            ReferenceNumber = cancelledSi.InvoiceNumber,
        };
        orphanJe.AddLine(debtors.Id, 250m, true);
        orphanJe.AddLine(income.Id, 250m, false);
        orphanJe.Post();
        await _jeRepository.InsertAsync(orphanJe, autoSave: true);

        // 3. Active posted JE referencing a non-existent (deleted) PurchaseInvoice -> MUST be flagged as NotFound
        var missingPiId = Guid.NewGuid();
        var deletedPiJe = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 6, 3))
        {
            ReferenceType = "PurchaseInvoice",
            ReferenceId = missingPiId,
            ReferenceNumber = "PI-DELETED-3",
        };
        deletedPiJe.AddLine(bank.Id, 100m, true);
        deletedPiJe.AddLine(income.Id, 100m, false);
        deletedPiJe.Post();
        await _jeRepository.InsertAsync(deletedPiJe, autoSave: true);

        // 4. Active PLE referencing healthy payment (VoucherId -> cancelled JE) vs AgainstVoucherId -> healthy/cancelled
        var cancelledJe = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 6, 4))
        {
            EntryNumber = "JE-CANCELLED-4",
        };
        cancelledJe.AddLine(bank.Id, 50m, true);
        cancelledJe.AddLine(debtors.Id, 50m, false);
        cancelledJe.Post();
        cancelledJe.Cancel();
        await _jeRepository.InsertAsync(cancelledJe, autoSave: true);

        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(),
            company.Id,
            new DateTime(2026, 6, 4),
            debtors.Id,
            "Customer",
            customer.Id,
            "JournalEntry",
            cancelledJe.Id,
            "SalesInvoice",
            healthySi.Id,
            -50m,
            -50m,
            "MYR");
        await _pleRepository.InsertAsync(ple, autoSave: true);

        // 5. Delinked PLE pointing to cancelled invoice must NOT be flagged
        var delinkedPle = new PaymentLedgerEntry(
            Guid.NewGuid(),
            company.Id,
            new DateTime(2026, 6, 5),
            debtors.Id,
            "Customer",
            customer.Id,
            "JournalEntry",
            cancelledJe.Id,
            "SalesInvoice",
            healthySi.Id,
            -10m,
            -10m,
            "MYR")
        {
            Delinked = true,
        };
        await _pleRepository.InsertAsync(delinkedPle, autoSave: true);

        // Full range scan
        var fullReport = await _reportService.GetReportAsync(new InvalidLedgerEntriesRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
        });

        fullReport.Rows.Select(r => r.VoucherNo).ShouldNotContain("SI-HEALTHY-1");
        fullReport.Rows.Single(r => r.VoucherNo == "SI-CORRUPT-2").Reason.ShouldBe("Cancelled");
        fullReport.Rows.Single(r => r.VoucherNo == "PI-DELETED-3").Reason.ShouldBe("NotFound");
        fullReport.Rows.Single(r => r.VoucherNo == "JE-CANCELLED-4").LedgerSource.ShouldContain("Payment Ledger Entry");

        // VoucherNo filter scopes strictly to matching voucher
        var byVoucherNo = await _reportService.GetReportAsync(new InvalidLedgerEntriesRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            VoucherNo = "SI-CORRUPT-2",
        });
        byVoucherNo.Rows.Count.ShouldBe(1);
        byVoucherNo.Rows[0].VoucherId.ShouldBe(cancelledSi.Id);

        // Account filter includes matching account and excludes unrelated account
        var byMatchingAccount = await _reportService.GetReportAsync(new InvalidLedgerEntriesRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            AccountIds = new List<Guid> { debtors.Id },
        });
        byMatchingAccount.Rows.Select(r => r.VoucherNo).ShouldContain("SI-CORRUPT-2");
        byMatchingAccount.Rows.Select(r => r.VoucherNo).ShouldNotContain("PI-DELETED-3");

        var byUnrelatedAccount = await _reportService.GetReportAsync(new InvalidLedgerEntriesRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            AccountIds = new List<Guid> { unrelated.Id },
        });
        byUnrelatedAccount.Rows.ShouldBeEmpty();

        // Out-of-range period excludes entries
        var outOfRange = await _reportService.GetReportAsync(new InvalidLedgerEntriesRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2025, 1, 1),
            ToDate = new DateTime(2025, 12, 31),
        });
        outOfRange.Rows.ShouldBeEmpty();
    }
}
