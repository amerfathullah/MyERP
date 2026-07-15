using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.EntityFrameworkCore.Repositories;

/// <summary>
/// Integration tests verifying EF Core entity persistence, relationships, 
/// AutoInclude configurations, and query behavior against SQLite.
/// </summary>
[Collection(MyERPTestConsts.CollectionDefinitionName)]
public class EntityPersistenceTests : MyERPEntityFrameworkCoreTestBase
{
    private readonly IRepository<Company, Guid> _companyRepo;
    private readonly IRepository<Account, Guid> _accountRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;

    public EntityPersistenceTests()
    {
        _companyRepo = GetRequiredService<IRepository<Company, Guid>>();
        _accountRepo = GetRequiredService<IRepository<Account, Guid>>();
        _customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
        _supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
        _itemRepo = GetRequiredService<IRepository<Item, Guid>>();
        _warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
    }

    [Fact]
    public async Task Company_Should_Persist_And_Retrieve()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test Corp");
            company.CurrencyCode = "MYR";
            await _companyRepo.InsertAsync(company, autoSave: true);

            var loaded = await _companyRepo.GetAsync(company.Id);
            loaded.ShouldNotBeNull();
            loaded.Name.ShouldBe("Test Corp");
            loaded.CurrencyCode.ShouldBe("MYR");
        });
    }

    [Fact]
    public async Task Customer_Should_Persist_With_CreditLimit()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test Co");
            await _companyRepo.InsertAsync(company, autoSave: true);

            var customer = new Customer(Guid.NewGuid(), company.Id, "ABC Trading");
            customer.CreditLimit = 50000m;
            await _customerRepo.InsertAsync(customer, autoSave: true);

            var loaded = await _customerRepo.GetAsync(customer.Id);
            loaded.Name.ShouldBe("ABC Trading");
            loaded.CreditLimit.ShouldBe(50000m);
        });
    }

    [Fact]
    public async Task Supplier_Should_Persist_With_HoldType()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test Co");
            await _companyRepo.InsertAsync(company, autoSave: true);

            var supplier = new Supplier(Guid.NewGuid(), company.Id, "Vendor XYZ");
            supplier.HoldType = SupplierHoldType.Invoices;
            await _supplierRepo.InsertAsync(supplier, autoSave: true);

            var loaded = await _supplierRepo.GetAsync(supplier.Id);
            loaded.Name.ShouldBe("Vendor XYZ");
            loaded.HoldType.ShouldBe(SupplierHoldType.Invoices);
        });
    }

    [Fact]
    public async Task Item_Should_Persist_With_ReorderFields()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test Co");
            await _companyRepo.InsertAsync(company, autoSave: true);

            var item = new Item(Guid.NewGuid(), company.Id, "WIDGET-001", "Widget", ItemType.Goods);
            item.ReorderLevel = 10;
            item.ReorderQty = 50;
            item.SafetyStock = 5;
            await _itemRepo.InsertAsync(item, autoSave: true);

            var loaded = await _itemRepo.GetAsync(item.Id);
            loaded.ItemCode.ShouldBe("WIDGET-001");
            loaded.ReorderLevel.ShouldBe(10m);
            loaded.ReorderQty.ShouldBe(50m);
            loaded.SafetyStock.ShouldBe(5m);
        });
    }

    [Fact]
    public async Task Warehouse_Should_Persist_With_IsGroup()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test Co");
            await _companyRepo.InsertAsync(company, autoSave: true);

            var warehouse = new Warehouse(Guid.NewGuid(), company.Id, "Main Store");
            await _warehouseRepo.InsertAsync(warehouse, autoSave: true);

            var loaded = await _warehouseRepo.GetAsync(warehouse.Id);
            loaded.Name.ShouldBe("Main Store");
            loaded.IsGroup.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Account_Should_Persist_With_SubType()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = new Company(Guid.NewGuid(), "Test Co");
            await _companyRepo.InsertAsync(company, autoSave: true);

            var account = new Account(Guid.NewGuid(), company.Id, "1130", "Accounts Receivable",
                AccountType.Asset);
            account.AccountSubType = AccountSubType.AccountsReceivable;
            await _accountRepo.InsertAsync(account, autoSave: true);

            var loaded = await _accountRepo.GetAsync(account.Id);
            loaded.AccountCode.ShouldBe("1130");
            loaded.AccountSubType.ShouldBe(AccountSubType.AccountsReceivable);
        });
    }

    [Fact]
    public async Task QueryByCompany_Should_Filter_Correctly()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company1 = new Company(Guid.NewGuid(), "Company A");
            var company2 = new Company(Guid.NewGuid(), "Company B");
            await _companyRepo.InsertAsync(company1, autoSave: true);
            await _companyRepo.InsertAsync(company2, autoSave: true);

            await _customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company1.Id, "Customer A1"), autoSave: true);
            await _customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company1.Id, "Customer A2"), autoSave: true);
            await _customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company2.Id, "Customer B1"), autoSave: true);

            var queryable = await _customerRepo.GetQueryableAsync();
            var company1Customers = queryable.Where(c => c.CompanyId == company1.Id).ToList();

            company1Customers.Count.ShouldBe(2);
            company1Customers.ShouldAllBe(c => c.CompanyId == company1.Id);
        });
    }
}
