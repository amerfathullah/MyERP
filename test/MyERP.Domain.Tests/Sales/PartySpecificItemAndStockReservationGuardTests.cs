using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class PartySpecificItemAndStockReservationGuardTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _otherCompanyId = Guid.NewGuid();
    private readonly Guid _customerAId = Guid.NewGuid();
    private readonly Guid _customerBId = Guid.NewGuid();
    private readonly Guid _groupAId = Guid.NewGuid();
    private readonly Guid _groupBId = Guid.NewGuid();
    private readonly Guid _itemAId = Guid.NewGuid();
    private readonly Guid _itemBId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private static void ConfigureLazyServiceProvider(DomainService domainService)
    {
        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>(Arg.Any<Func<IServiceProvider, object>>()).Returns(guidGen);
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        lazyProvider.LazyGetRequiredService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);

        domainService.LazyServiceProvider = lazyProvider;
    }

    // =========================================================================
    // ERPNext PR #59636 / commit 5a8a0f9f34:
    // Enforce party-specific item restrictions on save and submit
    // =========================================================================

    [Fact]
    public async Task PartySpecificItem_RestrictsItemForCustomer_ThrowsBusinessException()
    {
        var partyRuleRepo = Substitute.For<IRepository<PartySpecificItem, Guid>>();
        var brandRepo = Substitute.For<IRepository<Brand, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var restrictionRepo = Substitute.For<IRepository<CompanyRestrictionEntry, Guid>>();
        var acctRepo = Substitute.For<IRepository<Account, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        // Rule: Item A is restricted to Customer A
        var rule = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.Customer,
            _customerAId, PartySpecificItemRestrictBasedOn.Item, _itemAId);
        var rules = new List<PartySpecificItem> { rule }.AsQueryable();
        partyRuleRepo.GetQueryableAsync().Returns(Task.FromResult(rules));

        var itemA = new Item(_itemAId, _companyId, "ITEM-A", "Item A", ItemType.Goods);
        var items = new List<Item> { itemA }.AsQueryable();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(items));

        var custB = new Customer(_customerBId, _companyId, "Customer B");
        var customers = new List<Customer> { custB }.AsQueryable();
        custRepo.GetQueryableAsync().Returns(Task.FromResult(customers));

        var filterService = new PartySpecificItemFilterService(partyRuleRepo, brandRepo, itemRepo);
        var companyRestriction = new CompanyRestrictionValidationService(
            restrictionRepo, itemRepo, custRepo, suppRepo, acctRepo, whRepo, filterService);

        // Customer B attempts to purchase Item A -> throws ItemRestrictedForParty
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await companyRestriction.ValidateTransactionCompanyAsync(
                "SalesOrder", _companyId,
                itemIds: new[] { _itemAId },
                customerIds: new[] { _customerBId });
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ItemRestrictedForParty);
    }

    [Fact]
    public async Task PartySpecificItem_AllowsItemWhenCustomerAllowed_Succeeds()
    {
        var partyRuleRepo = Substitute.For<IRepository<PartySpecificItem, Guid>>();
        var brandRepo = Substitute.For<IRepository<Brand, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var restrictionRepo = Substitute.For<IRepository<CompanyRestrictionEntry, Guid>>();
        var acctRepo = Substitute.For<IRepository<Account, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        // Rule: Item A is restricted to Customer A
        var rule = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.Customer,
            _customerAId, PartySpecificItemRestrictBasedOn.Item, _itemAId);
        var rules = new List<PartySpecificItem> { rule }.AsQueryable();
        partyRuleRepo.GetQueryableAsync().Returns(Task.FromResult(rules));

        var itemA = new Item(_itemAId, _companyId, "ITEM-A", "Item A", ItemType.Goods);
        var items = new List<Item> { itemA }.AsQueryable();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(items));

        var custA = new Customer(_customerAId, _companyId, "Customer A");
        var customers = new List<Customer> { custA }.AsQueryable();
        custRepo.GetQueryableAsync().Returns(Task.FromResult(customers));

        var filterService = new PartySpecificItemFilterService(partyRuleRepo, brandRepo, itemRepo);
        var companyRestriction = new CompanyRestrictionValidationService(
            restrictionRepo, itemRepo, custRepo, suppRepo, acctRepo, whRepo, filterService);

        // Customer A purchases Item A -> allowed
        await companyRestriction.ValidateTransactionCompanyAsync(
            "SalesOrder", _companyId,
            itemIds: new[] { _itemAId },
            customerIds: new[] { _customerAId });
    }

    [Fact]
    public async Task PartySpecificItem_ExemptsReturns_Succeeds()
    {
        var partyRuleRepo = Substitute.For<IRepository<PartySpecificItem, Guid>>();
        var brandRepo = Substitute.For<IRepository<Brand, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var restrictionRepo = Substitute.For<IRepository<CompanyRestrictionEntry, Guid>>();
        var acctRepo = Substitute.For<IRepository<Account, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        // Rule: Item A is restricted to Customer A
        var rule = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.Customer,
            _customerAId, PartySpecificItemRestrictBasedOn.Item, _itemAId);
        var rules = new List<PartySpecificItem> { rule }.AsQueryable();
        partyRuleRepo.GetQueryableAsync().Returns(Task.FromResult(rules));

        var itemA = new Item(_itemAId, _companyId, "ITEM-A", "Item A", ItemType.Goods);
        var items = new List<Item> { itemA }.AsQueryable();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(items));

        var custB = new Customer(_customerBId, _companyId, "Customer B");
        var customers = new List<Customer> { custB }.AsQueryable();
        custRepo.GetQueryableAsync().Returns(Task.FromResult(customers));

        var filterService = new PartySpecificItemFilterService(partyRuleRepo, brandRepo, itemRepo);
        var companyRestriction = new CompanyRestrictionValidationService(
            restrictionRepo, itemRepo, custRepo, suppRepo, acctRepo, whRepo, filterService);

        // Customer B processes a return for Item A (isReturn = true) -> exempt
        await companyRestriction.ValidateTransactionCompanyAsync(
            "SalesInvoice", _companyId,
            itemIds: new[] { _itemAId },
            customerIds: new[] { _customerBId },
            isReturn: true);
    }

    // =========================================================================
    // ERPNext PR #59710 / commit af30285fea:
    // Party specific item rule matching across different bases and item group hierarchy
    // =========================================================================

    [Fact]
    public async Task PartySpecificItem_CrossBases_AllowsItemWhenCustomerHasItemRuleEvenWithOtherBrandRule()
    {
        var partyRuleRepo = Substitute.For<IRepository<PartySpecificItem, Guid>>();
        var brandRepo = Substitute.For<IRepository<Brand, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var restrictionRepo = Substitute.For<IRepository<CompanyRestrictionEntry, Guid>>();
        var acctRepo = Substitute.For<IRepository<Account, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        var brandId = Guid.NewGuid();
        var brand = new Brand(brandId, "_Test Brand");
        brandRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Brand> { brand }.AsQueryable()));

        // Item A has Brand "_Test Brand"
        var itemA = new Item(_itemAId, _companyId, "ITEM-A", "Item A", ItemType.Goods) { Brand = "_Test Brand" };
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { itemA }.AsQueryable()));

        var customerCId = Guid.NewGuid();
        var custA = new Customer(_customerAId, _companyId, "Customer A");
        var custB = new Customer(_customerBId, _companyId, "Customer B");
        var custC = new Customer(customerCId, _companyId, "Customer C");
        custRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { custA, custB, custC }.AsQueryable()));

        // Rule 1: Customer A restricted to Brand "_Test Brand"
        var ruleA = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.Customer,
            _customerAId, PartySpecificItemRestrictBasedOn.Brand, brandId);

        // Rule 2: Customer B restricted to Item A
        var ruleB = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.Customer,
            _customerBId, PartySpecificItemRestrictBasedOn.Item, _itemAId);

        var rules = new List<PartySpecificItem> { ruleA, ruleB }.AsQueryable();
        partyRuleRepo.GetQueryableAsync().Returns(Task.FromResult(rules));

        var filterService = new PartySpecificItemFilterService(partyRuleRepo, brandRepo, itemRepo);
        var companyRestriction = new CompanyRestrictionValidationService(
            restrictionRepo, itemRepo, custRepo, suppRepo, acctRepo, whRepo, filterService);

        // 1. Customer B buys Item A -> Allowed because B has an Item rule for Item A
        await companyRestriction.ValidateTransactionCompanyAsync(
            "SalesOrder", _companyId,
            itemIds: new[] { _itemAId },
            customerIds: new[] { _customerBId });

        // 2. Customer A buys Item A -> Allowed because A has a Brand rule for "_Test Brand"
        await companyRestriction.ValidateTransactionCompanyAsync(
            "SalesOrder", _companyId,
            itemIds: new[] { _itemAId },
            customerIds: new[] { _customerAId });

        // 3. Customer C buys Item A -> Blocked because Item A is reserved for other parties
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await companyRestriction.ValidateTransactionCompanyAsync(
                "SalesOrder", _companyId,
                itemIds: new[] { _itemAId },
                customerIds: new[] { customerCId });
        });
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ItemRestrictedForParty);
    }

    [Fact]
    public async Task PartySpecificItem_ItemGroupSubtree_ParentGroupRuleCoversChildGroup()
    {
        var partyRuleRepo = Substitute.For<IRepository<PartySpecificItem, Guid>>();
        var brandRepo = Substitute.For<IRepository<Brand, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var restrictionRepo = Substitute.For<IRepository<CompanyRestrictionEntry, Guid>>();
        var acctRepo = Substitute.For<IRepository<Account, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();

        var parentGroupId = Guid.NewGuid();
        var childGroupId = Guid.NewGuid();
        var parentGroup = new ItemGroup(parentGroupId, "ParentGroup", isGroup: true);
        var childGroup = new ItemGroup(childGroupId, "ChildGroup", isGroup: false, parentId: parentGroupId);
        itemGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ItemGroup> { parentGroup, childGroup }.AsQueryable()));

        // Item B belongs to child group
        var itemB = new Item(_itemBId, _companyId, "ITEM-B", "Item B", ItemType.Goods) { ItemGroupId = childGroupId };
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { itemB }.AsQueryable()));

        var custA = new Customer(_customerAId, _companyId, "Customer A");
        var custB = new Customer(_customerBId, _companyId, "Customer B");
        custRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { custA, custB }.AsQueryable()));

        // Rule: Customer A has rule on ParentGroup
        var ruleA = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.Customer,
            _customerAId, PartySpecificItemRestrictBasedOn.ItemGroup, parentGroupId);

        partyRuleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PartySpecificItem> { ruleA }.AsQueryable()));

        var filterService = new PartySpecificItemFilterService(partyRuleRepo, brandRepo, itemRepo, itemGroupRepo);
        var companyRestriction = new CompanyRestrictionValidationService(
            restrictionRepo, itemRepo, custRepo, suppRepo, acctRepo, whRepo, filterService);

        // Customer A can buy child group Item B
        await companyRestriction.ValidateTransactionCompanyAsync(
            "SalesOrder", _companyId,
            itemIds: new[] { _itemBId },
            customerIds: new[] { _customerAId });

        // Customer B cannot buy child group Item B (covered by parent rule for Customer A)
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await companyRestriction.ValidateTransactionCompanyAsync(
                "SalesOrder", _companyId,
                itemIds: new[] { _itemBId },
                customerIds: new[] { _customerBId });
        });
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ItemRestrictedForParty);
    }

    // =========================================================================
    // ERPNext PR #59647 / commit c7a9f069b7:
    // Filter reservation warehouses by company
    // =========================================================================

    [Fact]
    public async Task StockReservation_WarehouseCompanyMismatch_ThrowsCompanyMismatch()
    {
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        // Warehouse belongs to _otherCompanyId
        var otherWh = new Warehouse(_warehouseId, _otherCompanyId, "Other Co Warehouse");
        whRepo.FindAsync(_warehouseId).Returns(Task.FromResult<Warehouse?>(otherWh));

        var manager = new StockReservationManager(sreRepo, binRepo, sleRepo, whRepo);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await manager.ReserveStockAsync(
                _itemAId, _warehouseId, _companyId, 5m, "SalesOrder", Guid.NewGuid());
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);
    }

    [Fact]
    public async Task StockReservation_MatchingWarehouseCompany_Succeeds()
    {
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        // Warehouse belongs to _companyId
        var matchingWh = new Warehouse(_warehouseId, _companyId, "Matching Co Warehouse");
        whRepo.FindAsync(_warehouseId).Returns(Task.FromResult<Warehouse?>(matchingWh));

        var bin = new Bin(Guid.NewGuid(), _itemAId, _warehouseId) { ActualQty = 10m };
        var bins = new List<Bin> { bin }.AsQueryable();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(bins));

        var sres = new List<StockReservationEntry>().AsQueryable();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(sres));

        var manager = new StockReservationManager(sreRepo, binRepo, sleRepo, whRepo);
        ConfigureLazyServiceProvider(manager);

        await manager.ReserveStockAsync(
            _itemAId, _warehouseId, _companyId, 5m, "SalesOrder", Guid.NewGuid());

        await sreRepo.Received(1).InsertAsync(Arg.Any<StockReservationEntry>());
    }

    // =========================================================================
    // ERPNext PR #59555 / commit 25a892ee5f:
    // Planned qty zero validation in production plan
    // =========================================================================

    [Fact]
    public void ProductionPlan_Submit_ZeroPlannedQty_ThrowsAmountMustBePositive()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-001", DateTime.UtcNow);
        var item = new ProductionPlanItem(Guid.NewGuid(), plan.Id, _itemAId, "Item A", Guid.NewGuid(), plannedQty: 0m);
        plan.AddPlannedItem(item);

        var ex = Should.Throw<BusinessException>(() => plan.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AmountMustBePositive);
    }

    [Fact]
    public void ProductionPlan_Submit_PositivePlannedQty_Succeeds()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-002", DateTime.UtcNow);
        var item = new ProductionPlanItem(Guid.NewGuid(), plan.Id, _itemAId, "Item A", Guid.NewGuid(), plannedQty: 5m);
        plan.AddPlannedItem(item);

        plan.Submit();
        plan.Status.ShouldBe(ProductionPlanStatus.Submitted);
    }
}
