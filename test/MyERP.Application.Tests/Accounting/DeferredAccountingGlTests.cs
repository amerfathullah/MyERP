using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Deferred revenue / expense through the real posting path.
///
/// Before this coverage existed an invoice with a deferred row credited income for the whole net
/// total and never touched the deferred account; recognition then debited that never-credited
/// deferred account and credited income a second time. Per ERPNext the row sits in the deferred
/// account until recognition moves it out, period by period, and a credit note reverses the
/// deferred account only while the row it returns is still unrecognized.
/// </summary>
public abstract class DeferredAccountingGlTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    // Six whole months of service: three already over, three still to come.
    private static readonly DateTime ThisMonth = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    private static readonly DateTime ServiceStart = ThisMonth.AddMonths(-3);
    private static readonly DateTime ServiceEnd = ThisMonth.AddMonths(3).AddDays(-1);
    private static readonly DateTime EndOfLastMonth = ThisMonth.AddDays(-1);

    private sealed record Fixture(
        Company Company,
        Customer Customer,
        Supplier Supplier,
        Item Item,
        Account Receivable,
        Account Income,
        Account DeferredRevenue,
        Account Payable,
        Account Expense,
        Account DeferredExpense);

    private async Task<Fixture> SeedAsync(string tag)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"Deferred Co {tag}"), autoSave: true);
        var customer = await GetRequiredService<IRepository<Customer, Guid>>().InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, $"Deferred Cust {tag}"), autoSave: true);
        var supplier = await GetRequiredService<IRepository<Supplier, Guid>>().InsertAsync(
            new Supplier(Guid.NewGuid(), company.Id, $"Deferred Supp {tag}"), autoSave: true);
        var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"DEF-{tag}", "Annual Support", ItemType.Service), autoSave: true);

        var receivable = await accountRepository.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"1200-{tag}", "Debtors", AccountType.Asset), autoSave: true);
        var income = await accountRepository.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"4000-{tag}", "Sales", AccountType.Revenue), autoSave: true);
        var deferredRevenue = await accountRepository.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"2300-{tag}", "Deferred Revenue", AccountType.Liability), autoSave: true);
        var payable = await accountRepository.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"2010-{tag}", "Creditors", AccountType.Liability), autoSave: true);
        var expense = await accountRepository.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"5010-{tag}", "Expenses", AccountType.Expense), autoSave: true);
        var deferredExpense = await accountRepository.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"1500-{tag}", "Deferred Expense", AccountType.Asset), autoSave: true);
        var costCenter = await GetRequiredService<IRepository<CostCenter, Guid>>().InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, $"Deferred CC {tag}"), autoSave: true);

        company.DefaultReceivableAccountId = receivable.Id;
        company.DefaultIncomeAccountId = income.Id;
        company.DefaultPayableAccountId = payable.Id;
        company.DefaultExpenseAccountId = expense.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await GetRequiredService<IRepository<FiscalYear, Guid>>().InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY {tag}", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)), autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"SI Series {tag}", "SalesInvoice", $"DSI{tag}-"), autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"PI Series {tag}", "PurchaseInvoice", $"DPI{tag}-"), autoSave: true);

        await ruleRepository.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Recv", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 }, autoSave: true);
        await ruleRepository.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Income", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 }, autoSave: true);
        await ruleRepository.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "PI DR Expense", "PurchaseInvoice", true, AccountSource.ItemExpense, AmountSource.NetTotal) { SortOrder = 1 }, autoSave: true);
        await ruleRepository.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "PI CR Payable", "PurchaseInvoice", false, AccountSource.SupplierPayable, AmountSource.GrandTotal) { SortOrder = 2 }, autoSave: true);

        return new Fixture(company, customer, supplier, item, receivable, income, deferredRevenue, payable, expense, deferredExpense);
    }

    private CreateSalesInvoiceItemDto DeferredRow(Fixture f, decimal quantity, decimal unitPrice) => new()
    {
        ItemId = f.Item.Id,
        Description = "Annual Support",
        Quantity = quantity,
        UnitPrice = unitPrice,
        EnableDeferredRevenue = true,
        DeferredRevenueAccountId = f.DeferredRevenue.Id,
        ServiceStartDate = ServiceStart,
        ServiceEndDate = ServiceEnd,
    };

    private async Task<Guid> PostInvoiceAsync(Fixture f, params CreateSalesInvoiceItemDto[] rows)
    {
        var app = GetRequiredService<ISalesInvoiceAppService>();
        var created = await app.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = f.Company.Id,
            CustomerId = f.Customer.Id,
            IssueDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            Items = rows.ToList(),
        });
        await app.SubmitAsync(created.Id);
        await app.PostAsync(created.Id);
        return created.Id;
    }

    private async Task<Guid> PostCreditNoteAsync(Fixture f, Guid originalId, decimal quantity, decimal unitPrice)
    {
        var app = GetRequiredService<ISalesInvoiceAppService>();
        var created = await app.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = f.Company.Id,
            CustomerId = f.Customer.Id,
            IssueDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            IsReturn = true,
            ReturnAgainstId = originalId,
            Items = new List<CreateSalesInvoiceItemDto> { DeferredRow(f, -quantity, unitPrice) },
        });
        await app.SubmitAsync(created.Id);
        await app.PostAsync(created.Id);
        return created.Id;
    }

    private Task SaveAsync() => GetRequiredService<IUnitOfWorkManager>().Current!.SaveChangesAsync();

    /// <summary>Net balance of an account over the whole ledger: positive = credit balance.</summary>
    private async Task<decimal> CreditBalanceAsync(Guid accountId)
    {
        var journals = (await GetRequiredService<IRepository<JournalEntry, Guid>>().GetQueryableAsync()).ToList();
        return journals
            .Where(j => j.Status == Core.DocumentStatus.Posted)
            .SelectMany(j => j.Lines)
            .Where(l => l.AccountId == accountId)
            .Sum(l => l.IsDebit ? -l.Amount : l.Amount);
    }

    private static decimal DaysShare(decimal amount, DateTime from, DateTime to)
    {
        decimal totalDays = (ServiceEnd - ServiceStart).Days + 1;
        decimal days = (to - from).Days + 1;
        return Math.Round(amount * days / totalDays, 2);
    }

    [Fact]
    public async Task Post_DeferredRow_CreditsDeferredRevenueNotIncome()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("A");
            var invoiceId = await PostInvoiceAsync(f,
                DeferredRow(f, 1, 1200m),
                new CreateSalesInvoiceItemDto { ItemId = f.Item.Id, Description = "Setup fee", Quantity = 1, UnitPrice = 300m });

            var invoice = await GetRequiredService<IRepository<SalesInvoice, Guid>>().GetAsync(invoiceId);
            var deferredRow = invoice.Items.Single(i => i.EnableDeferredRevenue);

            var journal = (await GetRequiredService<IRepository<JournalEntry, Guid>>().GetQueryableAsync())
                .ToList()
                .Single(j => j.ReferenceType == "SalesInvoice" && j.ReferenceId == invoiceId);

            var deferredLine = journal.Lines.Single(l => l.AccountId == f.DeferredRevenue.Id);
            deferredLine.IsDebit.ShouldBeFalse();
            deferredLine.Amount.ShouldBe(1200m);
            deferredLine.ReferenceDetailId.ShouldBe(deferredRow.Id);

            // Only the row that is not deferred reaches income.
            journal.Lines.Single(l => l.AccountId == f.Income.Id).Amount.ShouldBe(300m);
            journal.Lines.Single(l => l.AccountId == f.Receivable.Id).Amount.ShouldBe(1500m);
            journal.TotalDebit.ShouldBe(journal.TotalCredit);
        });
    }

    [Fact]
    public async Task Process_BooksEachElapsedMonthOnce_AndClosesTheDeferredAccountAtServiceEnd()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("B");
            var invoiceId = await PostInvoiceAsync(f, DeferredRow(f, 1, 1200m));
            var service = GetRequiredService<DeferredAccountingService>();

            (await service.ProcessDeferredRevenueAsync(f.Company.Id, EndOfLastMonth)).ShouldBe(3);
            await SaveAsync();

            var recognized = (await GetRequiredService<IRepository<JournalEntry, Guid>>().GetQueryableAsync())
                .ToList()
                .Where(j => j.ReferenceType == DeferredAccountingService.RevenueReferenceType && j.ReferenceId == invoiceId)
                .OrderBy(j => j.PostingDate)
                .ToList();
            recognized.Count.ShouldBe(3);
            recognized[0].PostingDate.ShouldBe(ServiceStart.AddMonths(1).AddDays(-1));
            recognized[0].Lines.Single(l => l.AccountId == f.DeferredRevenue.Id).IsDebit.ShouldBeTrue();
            recognized[0].Lines.Single(l => l.AccountId == f.Income.Id).IsDebit.ShouldBeFalse();
            recognized[0].Lines.Single(l => l.AccountId == f.Income.Id).Amount
                .ShouldBe(DaysShare(1200m, ServiceStart, ServiceStart.AddMonths(1).AddDays(-1)));

            var expected = DaysShare(1200m, ServiceStart, ServiceStart.AddMonths(1).AddDays(-1))
                + DaysShare(1200m, ServiceStart.AddMonths(1), ServiceStart.AddMonths(2).AddDays(-1))
                + DaysShare(1200m, ServiceStart.AddMonths(2), EndOfLastMonth);
            (await CreditBalanceAsync(f.Income.Id)).ShouldBe(expected);
            (await CreditBalanceAsync(f.DeferredRevenue.Id)).ShouldBe(1200m - expected);

            // Nothing new to book for the same cut-off.
            (await service.ProcessDeferredRevenueAsync(f.Company.Id, EndOfLastMonth)).ShouldBe(0);

            // Through the service end: the last entry takes the remainder, to the cent.
            (await service.ProcessDeferredRevenueAsync(f.Company.Id, ServiceEnd)).ShouldBe(3);
            await SaveAsync();
            (await CreditBalanceAsync(f.Income.Id)).ShouldBe(1200m);
            (await CreditBalanceAsync(f.DeferredRevenue.Id)).ShouldBe(0m);
        });
    }

    [Fact]
    public async Task Process_TwoDeferredRowsOnOneInvoice_BooksBoth()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("C");
            await PostInvoiceAsync(f, DeferredRow(f, 1, 1200m), DeferredRow(f, 2, 300m));
            var service = GetRequiredService<DeferredAccountingService>();

            (await service.ProcessDeferredRevenueAsync(f.Company.Id, ServiceEnd)).ShouldBe(12);
            await SaveAsync();

            (await CreditBalanceAsync(f.Income.Id)).ShouldBe(1800m);
            (await CreditBalanceAsync(f.DeferredRevenue.Id)).ShouldBe(0m);
        });
    }

    [Fact]
    public async Task CreditNote_BeforeRecognition_ReversesDeferredRevenue_AndOnlyTheKeptSaleIsRecognized()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("D");
            var invoiceId = await PostInvoiceAsync(f, DeferredRow(f, 4, 300m));
            var creditNoteId = await PostCreditNoteAsync(f, invoiceId, 1, 300m);

            var creditNoteJournal = (await GetRequiredService<IRepository<JournalEntry, Guid>>().GetQueryableAsync())
                .ToList()
                .Single(j => j.ReferenceType == "SalesInvoice" && j.ReferenceId == creditNoteId);
            creditNoteJournal.Lines.ShouldNotContain(l => l.AccountId == f.Income.Id);
            var deferredLine = creditNoteJournal.Lines.Single(l => l.AccountId == f.DeferredRevenue.Id);
            deferredLine.IsDebit.ShouldBeTrue();
            deferredLine.Amount.ShouldBe(300m);

            await GetRequiredService<DeferredAccountingService>().ProcessDeferredRevenueAsync(f.Company.Id, ServiceEnd);
            await SaveAsync();

            (await CreditBalanceAsync(f.Income.Id)).ShouldBe(900m);
            (await CreditBalanceAsync(f.DeferredRevenue.Id)).ShouldBe(0m);
        });
    }

    [Fact]
    public async Task CreditNote_AfterRecognitionStarted_ReversesIncome()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("E");
            var invoiceId = await PostInvoiceAsync(f, DeferredRow(f, 4, 300m));
            await GetRequiredService<DeferredAccountingService>().ProcessDeferredRevenueAsync(f.Company.Id, EndOfLastMonth);
            await SaveAsync();

            var creditNoteId = await PostCreditNoteAsync(f, invoiceId, 1, 300m);

            var creditNoteJournal = (await GetRequiredService<IRepository<JournalEntry, Guid>>().GetQueryableAsync())
                .ToList()
                .Single(j => j.ReferenceType == "SalesInvoice" && j.ReferenceId == creditNoteId);
            creditNoteJournal.Lines.ShouldNotContain(l => l.AccountId == f.DeferredRevenue.Id);
            var incomeLine = creditNoteJournal.Lines.Single(l => l.AccountId == f.Income.Id);
            incomeLine.IsDebit.ShouldBeTrue();
            incomeLine.Amount.ShouldBe(300m);
        });
    }

    [Fact]
    public async Task CancelCreditNote_AfterOriginalIsRecognized_IsBlocked()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("F");
            var invoiceId = await PostInvoiceAsync(f, DeferredRow(f, 4, 300m));
            var creditNoteId = await PostCreditNoteAsync(f, invoiceId, 1, 300m);
            await GetRequiredService<DeferredAccountingService>().ProcessDeferredRevenueAsync(f.Company.Id, EndOfLastMonth);
            await SaveAsync();

            var exception = await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<ISalesInvoiceAppService>().CancelAsync(creditNoteId));
            exception.Code.ShouldBe(MyERPDomainErrorCodes.DeferredRevenueBookedOnReturnedInvoice);
        });
    }

    [Fact]
    public async Task CancelInvoice_ReversesItsRecognition()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("G");
            var invoiceId = await PostInvoiceAsync(f, DeferredRow(f, 1, 1200m));
            await GetRequiredService<DeferredAccountingService>().ProcessDeferredRevenueAsync(f.Company.Id, EndOfLastMonth);
            await SaveAsync();
            (await CreditBalanceAsync(f.Income.Id)).ShouldBeGreaterThan(0m);

            await GetRequiredService<ISalesInvoiceAppService>().CancelAsync(invoiceId);
            await SaveAsync();

            (await CreditBalanceAsync(f.Income.Id)).ShouldBe(0m);
            (await CreditBalanceAsync(f.DeferredRevenue.Id)).ShouldBe(0m);
            (await CreditBalanceAsync(f.Receivable.Id)).ShouldBe(0m);
        });
    }

    [Fact]
    public async Task ProcessDeferredAccounting_Cancel_TakesBackWhatTheRunBooked()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("I");
            await PostInvoiceAsync(f, DeferredRow(f, 1, 1200m));

            var app = GetRequiredService<IProcessDeferredAccountingAppService>();
            var run = await app.CreateAsync(new CreateProcessDeferredAccountingDto
            {
                CompanyId = f.Company.Id,
                Type = DeferredAccountingType.Income,
                PostingDate = DateTime.UtcNow.Date,
                StartDate = ServiceStart,
                EndDate = EndOfLastMonth,
            });

            // The preview shows exactly what the submit then books.
            var preview = await app.PreviewDeferredAccountingAsync(new PreviewDeferredAccountingInput
            {
                CompanyId = f.Company.Id,
                Type = DeferredAccountingType.Income,
                StartDate = ServiceStart,
                EndDate = EndOfLastMonth,
            });
            preview.Items.Count.ShouldBe(3);

            (await app.SubmitAsync(run.Id)).EntriesProcessed.ShouldBe(3);
            await SaveAsync();
            (await CreditBalanceAsync(f.Income.Id)).ShouldBe(preview.TotalAmountToRecognize);

            await app.CancelAsync(run.Id);
            await SaveAsync();

            (await CreditBalanceAsync(f.Income.Id)).ShouldBe(0m);
            (await CreditBalanceAsync(f.DeferredRevenue.Id)).ShouldBe(1200m);

            // Taken back, so the same periods are open to book again.
            (await GetRequiredService<DeferredAccountingService>().ProcessDeferredRevenueAsync(f.Company.Id, EndOfLastMonth)).ShouldBe(3);
        });
    }

    [Fact]
    public async Task PurchaseInvoice_DeferredRow_DebitsDeferredExpense_ThenRecognitionMovesItToExpense()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("H");
            var app = GetRequiredService<IPurchaseInvoiceAppService>();
            var created = await app.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = f.Item.Id,
                        Description = "Annual licence",
                        Quantity = 1,
                        UnitPrice = 600m,
                        EnableDeferredExpense = true,
                        DeferredExpenseAccountId = f.DeferredExpense.Id,
                        ServiceStartDate = ServiceStart,
                        ServiceEndDate = ServiceEnd,
                    }
                }
            });
            await app.SubmitAsync(created.Id);
            await app.PostAsync(created.Id);
            await SaveAsync();

            // Debit balances show as negative credit balances.
            (await CreditBalanceAsync(f.DeferredExpense.Id)).ShouldBe(-600m);
            (await CreditBalanceAsync(f.Expense.Id)).ShouldBe(0m);

            await GetRequiredService<DeferredAccountingService>().ProcessDeferredExpenseAsync(f.Company.Id, ServiceEnd);
            await SaveAsync();

            (await CreditBalanceAsync(f.DeferredExpense.Id)).ShouldBe(0m);
            (await CreditBalanceAsync(f.Expense.Id)).ShouldBe(-600m);
        });
    }
}
