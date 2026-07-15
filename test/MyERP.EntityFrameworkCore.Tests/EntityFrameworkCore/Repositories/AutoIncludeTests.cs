using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.EntityFrameworkCore.Repositories;

/// <summary>
/// Tests that verify AutoInclude() is correctly configured on parent-child relationships.
/// These catch the silent runtime bug where GetAsync returns entities with empty Items collections.
/// </summary>
[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class AutoIncludeTests : MyERPEntityFrameworkCoreTestBase
{
    private readonly IRepository<SalesOrder, Guid> _soRepo;
    private readonly IRepository<PurchaseOrder, Guid> _poRepo;
    private readonly IRepository<JournalEntry, Guid> _jeRepo;
    private readonly IRepository<Company, Guid> _companyRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Account, Guid> _accountRepo;

    public AutoIncludeTests()
    {
        _soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
        _poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
        _jeRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
        _companyRepo = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
        _supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
        _accountRepo = GetRequiredService<IRepository<Account, Guid>>();
    }

    [Fact]
    public async Task SalesOrder_GetAsync_Should_Include_Items()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test");
            await _companyRepo.InsertAsync(company, autoSave: true);
            var customer = new Customer(Guid.NewGuid(), company.Id, "Cust");
            await _customerRepo.InsertAsync(customer, autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-001", DateTime.Today);
            so.AddItem(Guid.NewGuid(), "Widget", 5, 100, 0);
            so.AddItem(Guid.NewGuid(), "Gadget", 2, 250, 0);
            await _soRepo.InsertAsync(so, autoSave: true);

            // Retrieve using standard GetAsync — AutoInclude should load Items
            var loaded = await _soRepo.GetAsync(so.Id);
            loaded.Items.ShouldNotBeNull();
            loaded.Items.Count.ShouldBe(2);
            loaded.Items.First().Description.ShouldBe("Widget");
        });
    }

    [Fact]
    public async Task PurchaseOrder_GetAsync_Should_Include_Items()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test");
            await _companyRepo.InsertAsync(company, autoSave: true);
            var supplier = new Supplier(Guid.NewGuid(), company.Id, "Vendor");
            await _supplierRepo.InsertAsync(supplier, autoSave: true);

            var po = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-001", DateTime.Today);
            po.AddItem(Guid.NewGuid(), "Raw Material A", 100, 10, 0);
            await _poRepo.InsertAsync(po, autoSave: true);

            var loaded = await _poRepo.GetAsync(po.Id);
            loaded.Items.ShouldNotBeNull();
            loaded.Items.Count.ShouldBe(1);
            loaded.Items.First().Quantity.ShouldBe(100);
        });
    }

    [Fact]
    public async Task JournalEntry_GetAsync_Should_Include_Lines()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test");
            await _companyRepo.InsertAsync(company, autoSave: true);

            var fy = new FiscalYear(Guid.NewGuid(), company.Id, "FY2026",
                new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
            await GetRequiredService<IRepository<FiscalYear, Guid>>().InsertAsync(fy, autoSave: true);

            var receivableAcct = new Account(Guid.NewGuid(), company.Id, "1130", "AR", AccountType.Asset);
            var revenueAcct = new Account(Guid.NewGuid(), company.Id, "4100", "Revenue", AccountType.Revenue);
            await _accountRepo.InsertAsync(receivableAcct, autoSave: true);
            await _accountRepo.InsertAsync(revenueAcct, autoSave: true);

            var je = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, DateTime.Today);
            je.AddLine(receivableAcct.Id, 1000m, true, "AR - Invoice");
            je.AddLine(revenueAcct.Id, 1000m, false, "Revenue");
            await _jeRepo.InsertAsync(je, autoSave: true);

            var loaded = await _jeRepo.GetAsync(je.Id);
            loaded.Lines.ShouldNotBeNull();
            loaded.Lines.Count.ShouldBe(2);
            loaded.Lines.Sum(l => l.IsDebit ? l.Amount : -l.Amount).ShouldBe(0); // Balanced
        });
    }

    [Fact]
    public async Task SalesOrder_GetListAsync_Should_Include_Items()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test");
            await _companyRepo.InsertAsync(company, autoSave: true);
            var customer = new Customer(Guid.NewGuid(), company.Id, "Cust");
            await _customerRepo.InsertAsync(customer, autoSave: true);

            var so1 = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-A01", DateTime.Today);
            so1.AddItem(Guid.NewGuid(), "Item 1", 1, 100, 0);
            var so2 = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-A02", DateTime.Today);
            so2.AddItem(Guid.NewGuid(), "Item 2", 2, 200, 0);
            so2.AddItem(Guid.NewGuid(), "Item 3", 3, 300, 0);
            await _soRepo.InsertAsync(so1, autoSave: true);
            await _soRepo.InsertAsync(so2, autoSave: true);

            var list = await _soRepo.GetListAsync();
            list.ShouldNotBeNull();
            // AutoInclude ensures Items are loaded even in GetListAsync
            list.All(s => s.Items != null && s.Items.Count > 0).ShouldBeTrue();
        });
    }
}
