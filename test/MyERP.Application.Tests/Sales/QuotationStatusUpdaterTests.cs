using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Core;
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

public abstract class QuotationStatusUpdaterTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IQuotationAppService _quotationAppService;
    private readonly ISalesOrderAppService _salesOrderAppService;
    private readonly IDocumentConversionAppService _conversionAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;
    private readonly IRepository<Quotation, Guid> _quotationRepository;
    private readonly IRepository<DocumentSeries, Guid> _seriesRepository;

    protected QuotationStatusUpdaterTests()
    {
        _quotationAppService = GetRequiredService<IQuotationAppService>();
        _salesOrderAppService = GetRequiredService<ISalesOrderAppService>();
        _conversionAppService = GetRequiredService<IDocumentConversionAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _opportunityRepository = GetRequiredService<IRepository<Opportunity, Guid>>();
        _quotationRepository = GetRequiredService<IRepository<Quotation, Guid>>();
        _seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
    }

    [Fact]
    public async Task Quotation_Submit_Cancel_And_MarkLost_Should_Cascade_To_Opportunity()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "QTN Opp Cascade Co"), autoSave: true);
            await _seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Quotation Series", "Quotation", "QTN-"), autoSave: true);
            var customer = await _customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "QTN Opp Cust"), autoSave: true);
            var item = await _itemRepository.InsertAsync(new Item(Guid.NewGuid(), company.Id, "QTN-OPP-ITEM", "Quotation Item", ItemType.Goods) { MaintainStock = false }, autoSave: true);

            var opp = new Opportunity(Guid.NewGuid(), company.Id, "OPP-QTN-001", "Cascade Deal")
            {
                CustomerId = customer.Id
            };
            opp.AddItem("Cascade Item", 10m, 100m, item.Id);
            await _opportunityRepository.InsertAsync(opp, autoSave: true);
            opp.Status.ShouldBe(OpportunityStatus.Open);

            // Create Quotation linked to Opportunity
            var qtnDto = await _quotationAppService.CreateAsync(new CreateQuotationDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                IssueDate = DateTime.UtcNow.Date,
                OpportunityId = opp.Id,
                Items = new List<CreateQuotationItemDto>
                {
                    new CreateQuotationItemDto
                    {
                        ItemId = item.Id,
                        Description = "Cascade Item",
                        Quantity = 10m,
                        UnitPrice = 100m,
                        Uom = "Unit"
                    }
                }
            });

            // Reload opp — creation advances to Quotation stage
            var oppAfterCreate = await _opportunityRepository.GetAsync(opp.Id);
            oppAfterCreate.Status.ShouldBe(OpportunityStatus.Quotation);

            // Submit Quotation
            await _quotationAppService.SubmitAsync(qtnDto.Id);
            var oppAfterSubmit = await _opportunityRepository.GetAsync(opp.Id);
            oppAfterSubmit.Status.ShouldBe(OpportunityStatus.Quotation);

            // Cancel Quotation — reverts Opportunity to Open since no other submitted quotations exist
            await _quotationAppService.CancelAsync(qtnDto.Id);
            var oppAfterCancel = await _opportunityRepository.GetAsync(opp.Id);
            oppAfterCancel.Status.ShouldBe(OpportunityStatus.Open);

            // Create another Quotation and Mark Lost
            var qtnDto2 = await _quotationAppService.CreateAsync(new CreateQuotationDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                IssueDate = DateTime.UtcNow.Date,
                OpportunityId = opp.Id,
                Items = new List<CreateQuotationItemDto>
                {
                    new CreateQuotationItemDto
                    {
                        ItemId = item.Id,
                        Description = "Cascade Item",
                        Quantity = 10m,
                        UnitPrice = 100m,
                        Uom = "Unit"
                    }
                }
            });
            await _quotationAppService.SubmitAsync(qtnDto2.Id);
            await _quotationAppService.MarkLostAsync(qtnDto2.Id);

            var oppAfterLost = await _opportunityRepository.GetAsync(opp.Id);
            oppAfterLost.Status.ShouldBe(OpportunityStatus.Lost);
        });
    }

    [Fact]
    public async Task SalesOrder_Submit_And_Cancel_Should_Update_Quotation_OrderedQty_And_Opportunity_Status()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SO QTN Cascade Co"), autoSave: true);
            await _seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Quotation Series", "Quotation", "QTN-"), autoSave: true);
            await _seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SO Series", "SalesOrder", "SO-"), autoSave: true);
            var customer = await _customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "SO QTN Cust"), autoSave: true);
            var item = await _itemRepository.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SO-QTN-ITEM", "Orderable Item", ItemType.Goods) { MaintainStock = false }, autoSave: true);

            var opp = new Opportunity(Guid.NewGuid(), company.Id, "OPP-SO-001", "SO Deal") { CustomerId = customer.Id };
            await _opportunityRepository.InsertAsync(opp, autoSave: true);

            var qtnDto = await _quotationAppService.CreateAsync(new CreateQuotationDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                IssueDate = DateTime.UtcNow.Date,
                OpportunityId = opp.Id,
                Items = new List<CreateQuotationItemDto>
                {
                    new CreateQuotationItemDto
                    {
                        ItemId = item.Id,
                        Description = "Orderable Item",
                        Quantity = 10m,
                        UnitPrice = 50m,
                        Uom = "Unit"
                    }
                }
            });
            await _quotationAppService.SubmitAsync(qtnDto.Id);

            var qtn = await _quotationRepository.GetAsync(qtnDto.Id, includeDetails: true);
            qtn.OrderStatus.ShouldBe("Open");
            qtn.PerOrdered.ShouldBe(0m);
            var qtnItemId = qtn.Items[0].Id;

            // Partial Sales Order: 6 of 10 units
            var soDto1 = await _salesOrderAppService.CreateAsync(new CreateSalesOrderDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                OrderDate = DateTime.UtcNow.Date,
                QuotationId = qtn.Id,
                Items = new List<CreateSalesOrderItemDto>
                {
                    new CreateSalesOrderItemDto
                    {
                        ItemId = item.Id,
                        Description = "Orderable Item",
                        Quantity = 6m,
                        UnitPrice = 50m,
                        Uom = "Unit",
                        QuotationItemId = qtnItemId
                    }
                }
            });

            // Submit SO 1
            await _salesOrderAppService.SubmitAsync(soDto1.Id);

            var qtnAfterSo1 = await _quotationRepository.GetAsync(qtn.Id, includeDetails: true);
            qtnAfterSo1.Items[0].OrderedQty.ShouldBe(6m);
            qtnAfterSo1.PerOrdered.ShouldBe(60m);
            qtnAfterSo1.OrderStatus.ShouldBe("Partially Ordered");

            var oppAfterSo1 = await _opportunityRepository.GetAsync(opp.Id);
            oppAfterSo1.Status.ShouldBe(OpportunityStatus.Converted);

            // Cannot mark lost while partially ordered (guard check)
            Should.Throw<BusinessException>(() => qtnAfterSo1.MarkLost());

            // Second Sales Order: remaining 4 units
            var soDto2 = await _salesOrderAppService.CreateAsync(new CreateSalesOrderDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                OrderDate = DateTime.UtcNow.Date,
                QuotationId = qtn.Id,
                Items = new List<CreateSalesOrderItemDto>
                {
                    new CreateSalesOrderItemDto
                    {
                        ItemId = item.Id,
                        Description = "Orderable Item",
                        Quantity = 4m,
                        UnitPrice = 50m,
                        Uom = "Unit",
                        QuotationItemId = qtnItemId
                    }
                }
            });
            await _salesOrderAppService.SubmitAsync(soDto2.Id);

            var qtnAfterSo2 = await _quotationRepository.GetAsync(qtn.Id, includeDetails: true);
            qtnAfterSo2.Items[0].OrderedQty.ShouldBe(10m);
            qtnAfterSo2.PerOrdered.ShouldBe(100m);
            qtnAfterSo2.OrderStatus.ShouldBe("Ordered");
            qtnAfterSo2.IsFullyOrdered.ShouldBeTrue();

            // Cancel SO 2: OrderedQty decrements to 6
            await _salesOrderAppService.CancelAsync(soDto2.Id);

            var qtnAfterCancel2 = await _quotationRepository.GetAsync(qtn.Id, includeDetails: true);
            qtnAfterCancel2.Items[0].OrderedQty.ShouldBe(6m);
            qtnAfterCancel2.PerOrdered.ShouldBe(60m);
            qtnAfterCancel2.OrderStatus.ShouldBe("Partially Ordered");

            // Opportunity stays Converted because SO 1 is still active
            var oppStillConverted = await _opportunityRepository.GetAsync(opp.Id);
            oppStillConverted.Status.ShouldBe(OpportunityStatus.Converted);

            // Cancel SO 1: OrderedQty decrements to 0
            await _salesOrderAppService.CancelAsync(soDto1.Id);

            var qtnAfterCancel1 = await _quotationRepository.GetAsync(qtn.Id, includeDetails: true);
            qtnAfterCancel1.Items[0].OrderedQty.ShouldBe(0m);
            qtnAfterCancel1.PerOrdered.ShouldBe(0m);
            qtnAfterCancel1.OrderStatus.ShouldBe("Open");

            // Opportunity reverts to Quotation
            var oppReverted = await _opportunityRepository.GetAsync(opp.Id);
            oppReverted.Status.ShouldBe(OpportunityStatus.Quotation);
        });
    }
}
