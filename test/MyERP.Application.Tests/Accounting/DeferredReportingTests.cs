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
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;
using Xunit;

namespace MyERP.Accounting;

public abstract class DeferredReportingTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDeferredReportingAppService _deferredReportingAppService;

    protected DeferredReportingTests()
    {
        _deferredReportingAppService = GetRequiredService<IDeferredReportingAppService>();
    }

    [Fact]
    public async Task DeferredRevenue_Should_Report_Actual_And_Upcoming_Postings()
    {
        var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
        var accRepo = GetRequiredService<IRepository<Account, Guid>>();
        var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
        var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
        var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();

        var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Def Rpt Co"), autoSave: true);
        var customer = await custRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Def Rpt Cust"), autoSave: true);
        var item = await itemRepo.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "SUB-001", "Annual SaaS Subscription", ItemType.Service), autoSave: true);

        var recv = await accRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1200", "Debtors", AccountType.Asset), autoSave: true);
        var inc = await accRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "4000", "Sales", AccountType.Revenue), autoSave: true);
        var defRev = await accRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "2300", "Deferred Revenue", AccountType.Liability), autoSave: true);

        company.DefaultReceivableAccountId = recv.Id;
        company.DefaultIncomeAccountId = inc.Id;
        await companyRepo.UpdateAsync(company, autoSave: true);

        var fy = await fyRepo.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY2025", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)), autoSave: true);
        await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SI", "SalesInvoice", "SI-DR-"), autoSave: true);
        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "SI DR", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 }, autoSave: true);
        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "SI CR", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 }, autoSave: true);

        // Set Days basis explicitly for predictable exact proration: May 1 .. Jul 31 (31+30+31 = 92 days -> 920 total -> 10/day)
        var siApp = GetRequiredService<ISalesInvoiceAppService>();
        var created = await siApp.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            IssueDate = new DateTime(2025, 5, 1),
            DueDate = new DateTime(2025, 5, 31),
            Items = new List<CreateSalesInvoiceItemDto>
            {
                new()
                {
                    ItemId = item.Id,
                    Description = "Annual SaaS Subscription",
                    Quantity = 1m,
                    UnitPrice = 920m,
                    EnableDeferredRevenue = true,
                    DeferredRevenueAccountId = defRev.Id,
                    ServiceStartDate = new DateTime(2025, 5, 1),
                    ServiceEndDate = new DateTime(2025, 7, 31),
                },
            },
        });
        await siApp.SubmitAsync(created.Id);
        await siApp.PostAsync(created.Id);

        // Book ONLY May 2025 actual recognition
        await WithUnitOfWorkAsync(async () =>
        {
            var deferredService = GetRequiredService<DeferredAccountingService>();
            var booked = await deferredService.ProcessDeferredRevenueAsync(company.Id, new DateTime(2025, 5, 31));
            booked.ShouldBe(1);
            await GetRequiredService<IUnitOfWorkManager>().Current!.SaveChangesAsync();
        });

        // WithUpcomingPostings = false -> only May actual (310), Jun/Jul = 0
        var actualOnly = await _deferredReportingAppService.GetDeferredRevenueAndExpenseAsync(
            new DeferredRevenueAndExpenseRequestDto
            {
                CompanyId = company.Id,
                FilterBasedOn = "Date Range",
                PeriodStartDate = new DateTime(2025, 5, 1),
                PeriodEndDate = new DateTime(2025, 8, 31),
                Periodicity = "Monthly",
                Type = "Revenue",
                WithUpcomingPostings = false,
            });

        actualOnly.Periods.Select(p => p.Key).ShouldBe(new[] { "may_2025", "jun_2025", "jul_2025", "aug_2025" });
        actualOnly.Periods[0].ActualTotal.ShouldBe(310m);
        actualOnly.Periods[0].ExpectedTotal.ShouldBe(310m);
        actualOnly.Periods[1].ExpectedTotal.ShouldBe(0m);
        actualOnly.Periods[2].ExpectedTotal.ShouldBe(0m);
        actualOnly.Periods[3].ExpectedTotal.ShouldBe(0m);

        var totalRowActual = actualOnly.Rows.Single(r => r.IsTotalRow);
        totalRowActual.Name.ShouldBe("Total Deferred Income");
        totalRowActual.Amount.ShouldBe(920m);

        // WithUpcomingPostings = true -> forecasts Jun (300) and Jul (310) while preserving May actual (310)
        var withForecast = await _deferredReportingAppService.GetDeferredRevenueAndExpenseAsync(
            new DeferredRevenueAndExpenseRequestDto
            {
                CompanyId = company.Id,
                FilterBasedOn = "Fiscal Year",
                FromFiscalYearId = fy.Id,
                ToFiscalYearId = fy.Id,
                Periodicity = "Monthly",
                Type = "Revenue",
                WithUpcomingPostings = true,
            });

        var mayIdx = withForecast.Periods.FindIndex(p => p.Key == "may_2025");
        var junIdx = withForecast.Periods.FindIndex(p => p.Key == "jun_2025");
        var julIdx = withForecast.Periods.FindIndex(p => p.Key == "jul_2025");

        withForecast.Periods[mayIdx].ActualTotal.ShouldBe(310m);
        withForecast.Periods[mayIdx].ExpectedTotal.ShouldBe(310m);
        withForecast.Periods[junIdx].ActualTotal.ShouldBe(0m);
        withForecast.Periods[junIdx].ExpectedTotal.ShouldBe(300m);
        withForecast.Periods[julIdx].ActualTotal.ShouldBe(0m);
        withForecast.Periods[julIdx].ExpectedTotal.ShouldBe(310m);
    }

    [Fact]
    public async Task DeferredExpense_Should_Report_Negative_Amounts_And_Quarterly_Rollup()
    {
        var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
        var accRepo = GetRequiredService<IRepository<Account, Guid>>();
        var suppRepo = GetRequiredService<IRepository<Supplier, Guid>>();
        var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
        var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();

        var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Def Exp Co"), autoSave: true);
        var supplier = await suppRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Def Exp Supp"), autoSave: true);
        var item = await itemRepo.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "INS-001", "Annual Insurance", ItemType.Service), autoSave: true);

        var pay = await accRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "2010", "Creditors", AccountType.Liability), autoSave: true);
        var exp = await accRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "5010", "Expenses", AccountType.Expense), autoSave: true);
        var defExp = await accRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1500", "Deferred Expense", AccountType.Asset), autoSave: true);

        company.DefaultPayableAccountId = pay.Id;
        company.DefaultExpenseAccountId = exp.Id;
        await companyRepo.UpdateAsync(company, autoSave: true);

        await fyRepo.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY2025", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)), autoSave: true);
        await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PI", "PurchaseInvoice", "PI-DE-"), autoSave: true);
        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "PI DR", "PurchaseInvoice", true, AccountSource.ItemExpense, AmountSource.NetTotal) { SortOrder = 1 }, autoSave: true);
        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "PI CR", "PurchaseInvoice", false, AccountSource.SupplierPayable, AmountSource.GrandTotal) { SortOrder = 2 }, autoSave: true);

        // Apr 1 .. Jun 30 = Q2 2025 (30+31+30 = 91 days -> 910 total)
        var piApp = GetRequiredService<IPurchaseInvoiceAppService>();
        var created = await piApp.CreateAsync(new CreatePurchaseInvoiceDto
        {
            CompanyId = company.Id,
            SupplierId = supplier.Id,
            IssueDate = new DateTime(2025, 4, 1),
            DueDate = new DateTime(2025, 4, 30),
            Items = new List<CreatePurchaseInvoiceItemDto>
            {
                new()
                {
                    ItemId = item.Id,
                    Description = "Annual Insurance",
                    Quantity = 1m,
                    UnitPrice = 910m,
                    EnableDeferredExpense = true,
                    DeferredExpenseAccountId = defExp.Id,
                    ServiceStartDate = new DateTime(2025, 4, 1),
                    ServiceEndDate = new DateTime(2025, 6, 30),
                },
            },
        });
        await piApp.SubmitAsync(created.Id);
        await piApp.PostAsync(created.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            var deferredService = GetRequiredService<DeferredAccountingService>();
            await deferredService.ProcessDeferredExpenseAsync(company.Id, new DateTime(2025, 4, 30));
            await GetRequiredService<IUnitOfWorkManager>().Current!.SaveChangesAsync();
        });

        var report = await _deferredReportingAppService.GetDeferredRevenueAndExpenseAsync(
            new DeferredRevenueAndExpenseRequestDto
            {
                CompanyId = company.Id,
                FilterBasedOn = "Date Range",
                PeriodStartDate = new DateTime(2025, 1, 1),
                PeriodEndDate = new DateTime(2025, 12, 31),
                Periodicity = "Quarterly",
                Type = "Expense",
                WithUpcomingPostings = true,
            });

        report.Periods.Select(p => p.Key).ShouldBe(new[] { "q1_2025", "q2_2025", "q3_2025", "q4_2025" });
        report.Periods[1].ActualTotal.ShouldBe(-300m); // Apr actual only
        report.Periods[1].ExpectedTotal.ShouldBe(-910m); // Apr actual (-300) + May (-310) + Jun (-300)

        var totalRow = report.Rows.Single(r => r.IsTotalRow);
        totalRow.Name.ShouldBe("Total Deferred Expense");
        totalRow.PeriodValues[1].ShouldBe(-910m);
    }
}
