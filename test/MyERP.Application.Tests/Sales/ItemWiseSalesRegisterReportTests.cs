using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class ItemWiseSalesRegisterReportTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISalesRegisterAppService _salesRegisterAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<CustomerGroup, Guid> _customerGroupRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;

    protected ItemWiseSalesRegisterReportTests()
    {
        _salesRegisterAppService = GetRequiredService<ISalesRegisterAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerGroupRepository = GetRequiredService<IRepository<CustomerGroup, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _itemGroupRepository = GetRequiredService<IRepository<ItemGroup, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        _salesOrderRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
        _deliveryNoteRepository = GetRequiredService<IRepository<DeliveryNote, Guid>>();
        _salesInvoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
    }

    [Fact]
    public async Task Should_Return_ItemWiseSalesRegister_With_Hierarchy_And_Links()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "IWSR Test Co"), autoSave: true);

        var parentCustGroup = await _customerGroupRepository.InsertAsync(
            new CustomerGroup(Guid.NewGuid(), "IWSR Parent Cust", isGroup: true), autoSave: true);
        var childCustGroup = await _customerGroupRepository.InsertAsync(
            new CustomerGroup(Guid.NewGuid(), "IWSR Retail", parentId: parentCustGroup.Id), autoSave: true);

        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "IWSR Customer") { CustomerGroupId = childCustGroup.Id },
            autoSave: true);

        var parentItemGroup = await _itemGroupRepository.InsertAsync(
            new ItemGroup(Guid.NewGuid(), "IWSR Parent Item", isGroup: true), autoSave: true);
        var childItemGroup = await _itemGroupRepository.InsertAsync(
            new ItemGroup(Guid.NewGuid(), "IWSR Hardware", parentId: parentItemGroup.Id), autoSave: true);

        var item = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "IWSR-001", "Router", ItemType.Goods)
            {
                ItemGroup = "IWSR Hardware",
                ItemGroupId = childItemGroup.Id,
            },
            autoSave: true);

        var wh = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "IWSR Main WH"), autoSave: true);

        var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-IWSR-001", new DateTime(2026, 11, 1));
        so.AddItem(item.Id, "Router", 3m, 100m, 0m);
        await _salesOrderRepository.InsertAsync(so, autoSave: true);
        var soItemId = so.Items.First().Id;

        var dn = new DeliveryNote(Guid.NewGuid(), company.Id, customer.Id, wh.Id, "DN-IWSR-001", new DateTime(2026, 11, 2));
        dn.AddItem(item.Id, "Router", 3m, 100m, 0m, salesOrderItemId: soItemId);
        dn.Submit();
        await _deliveryNoteRepository.InsertAsync(dn, autoSave: true);
        var dnItemId = dn.Items.First().Id;

        var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SINV-IWSR-001", new DateTime(2026, 11, 3));
        si.AddItem(item.Id, "Router", 3m, 100m, 18m);
        si.Items.First().SalesOrderItemId = soItemId;
        si.Items.First().DeliveryNoteItemId = dnItemId;
        si.Submit();
        si.Post();
        await _salesInvoiceRepository.InsertAsync(si, autoSave: true);

        var result = await _salesRegisterAppService.GetItemWiseReportAsync(new ItemWiseSalesRegisterFilterDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 11, 1),
            ToDate = new DateTime(2026, 11, 30),
            CustomerGroupId = parentCustGroup.Id,
            ItemGroupId = parentItemGroup.Id,
        });

        result.Count.ShouldBe(1);
        var line = result.Items.Single();
        line.InvoiceNumber.ShouldBe("SINV-IWSR-001");
        line.CustomerName.ShouldBe("IWSR Customer");
        line.CustomerGroupName.ShouldBe("IWSR Retail");
        line.ItemCode.ShouldBe("IWSR-001");
        line.SalesOrderNumber.ShouldBe("SO-IWSR-001");
        line.DeliveryNoteNumber.ShouldBe("DN-IWSR-001");
        line.Amount.ShouldBe(300m);
        line.TotalTax.ShouldBe(18m);
        line.Total.ShouldBe(318m);
    }
}
