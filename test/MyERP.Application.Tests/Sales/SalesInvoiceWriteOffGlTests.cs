using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
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

namespace MyERP.Sales;

public abstract class SalesInvoiceWriteOffGlTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private async Task<(Company Company, Account Receivable, Account Expense, Guid InvoiceId)> SeedPostedInvoiceAsync(string tag, bool withExpenseDefault)
    {
        var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), $"SI WO Co {tag}"), autoSave: true);
        var customer = await GetRequiredService<IRepository<Customer, Guid>>().InsertAsync(new Customer(Guid.NewGuid(), company.Id, $"SI WO Cust {tag}"), autoSave: true);
        var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(new Item(Guid.NewGuid(), company.Id, $"SIWO-{tag}", "WO Item", ItemType.Goods), autoSave: true);
        var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
        var receivable = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"1200-{tag}", "Debtors", AccountType.Asset), autoSave: true);
        var income = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"4000-{tag}", "Sales", AccountType.Revenue), autoSave: true);
        var expense = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"5900-{tag}", "Write Off", AccountType.Expense), autoSave: true);
        var costCenter = await GetRequiredService<IRepository<CostCenter, Guid>>().InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, $"SI WO CC {tag}"), autoSave: true);

        company.DefaultReceivableAccountId = receivable.Id;
        company.DefaultIncomeAccountId = income.Id;
        company.DefaultCostCenterId = costCenter.Id;
        if (withExpenseDefault) company.DefaultExpenseAccountId = expense.Id;
        await GetRequiredService<IRepository<Company, Guid>>().UpdateAsync(company, autoSave: true);

        await GetRequiredService<IRepository<FiscalYear, Guid>>().InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY {tag}", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)), autoSave: true);
        await GetRequiredService<IRepository<DocumentSeries, Guid>>().InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"SI Series {tag}", "SalesInvoice", $"SIWO{tag}-"), autoSave: true);
        var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Recv", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 }, autoSave: true);
        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Income", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 }, autoSave: true);

        var app = GetRequiredService<ISalesInvoiceAppService>();
        var created = await app.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            IssueDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            Items = new List<CreateSalesInvoiceItemDto>
            {
                new() { ItemId = item.Id, Description = "WO Item", Quantity = 1, UnitPrice = 50 }
            }
        });
        await app.SubmitAsync(created.Id);
        await app.PostAsync(created.Id);
        return (company, receivable, expense, created.Id);
    }

    [Fact]
    public async Task WriteOffAsync_PostsReceivableCreditAgainstWriteOffAccount()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedPostedInvoiceAsync("A", withExpenseDefault: true);
            await GetRequiredService<ISalesInvoiceAppService>().WriteOffAsync(f.InvoiceId);

            var journal = (await GetRequiredService<IRepository<JournalEntry, Guid>>().GetQueryableAsync())
                .ToList()
                .Single(j => j.Lines.Any(l => l.Description != null && l.Description.StartsWith("Write-off:")));
            var receivableLine = journal.Lines.Single(l => l.AccountId == f.Receivable.Id);
            receivableLine.IsDebit.ShouldBeFalse();
            receivableLine.Amount.ShouldBe(50m);
            journal.Lines.Single(l => l.AccountId == f.Expense.Id).IsDebit.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task WriteOffAsync_WithoutWriteOffAccount_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedPostedInvoiceAsync("B", withExpenseDefault: false);
            await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<ISalesInvoiceAppService>().WriteOffAsync(f.InvoiceId));
        });
    }
}
