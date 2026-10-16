using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class TerritoryWiseSalesAppServiceTests
{
    private readonly IRepository<Territory, Guid> _territoryRepo = Substitute.For<IRepository<Territory, Guid>>();
    private readonly IRepository<Opportunity, Guid> _opportunityRepo = Substitute.For<IRepository<Opportunity, Guid>>();
    private readonly IRepository<Quotation, Guid> _quotationRepo = Substitute.For<IRepository<Quotation, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _northTerritoryId = Guid.NewGuid();
    private readonly Guid _southTerritoryId = Guid.NewGuid();

    private readonly Territory _northTerritory;
    private readonly Territory _southTerritory;
    private readonly Company _company;

    public TerritoryWiseSalesAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Corp") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _northTerritory = new Territory(_northTerritoryId, "North Region");
        _southTerritory = new Territory(_southTerritoryId, "South Region");

        _territoryRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<Territory> { _northTerritory, _southTerritory }.AsQueryable()
        ));
    }

    [Fact]
    public async Task GetReportAsync_ShouldReturnEmpty_WhenNoDataExists()
    {
        // Arrange
        _opportunityRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Opportunity>().AsQueryable()));
        _quotationRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Quotation>().AsQueryable()));
        _salesOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));
        _salesInvoiceRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice>().AsQueryable()));

        var service = new TerritoryWiseSalesAppService(
            _territoryRepo,
            _opportunityRepo,
            _quotationRepo,
            _salesOrderRepo,
            _salesInvoiceRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetTerritoryWiseSalesInput
        {
            CompanyId = _companyId,
        });

        // Assert
        result.ShouldNotBeNull();
        result.Currency.ShouldBe("MYR");
        result.Rows.Count.ShouldBe(2); // North and South with zeros
        result.TotalOpportunityAmount.ShouldBe(0);
        result.TotalQuotationAmount.ShouldBe(0);
        result.TotalOrderAmount.ShouldBe(0);
        result.TotalBillingAmount.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_ShouldAggregatePipelineByTerritory()
    {
        // Arrange
        var oppNorthId = Guid.NewGuid();
        var oppNorth = new Opportunity(oppNorthId, _companyId, "OPP-001", "Deal North")
        {
            Territory = "North Region",
            OpportunityAmount = 10000m,
            ConversionRate = 1.0m,
        };

        var oppSouthId = Guid.NewGuid();
        var oppSouth = new Opportunity(oppSouthId, _companyId, "OPP-002", "Deal South")
        {
            Territory = "South Region",
            OpportunityAmount = 5000m,
            ConversionRate = 1.0m,
        };

        var oppUnassignedId = Guid.NewGuid();
        var oppUnassigned = new Opportunity(oppUnassignedId, _companyId, "OPP-003", "Deal Other")
        {
            Territory = null,
            OpportunityAmount = 2000m,
            ConversionRate = 1.0m,
        };

        _opportunityRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<Opportunity> { oppNorth, oppSouth, oppUnassigned }.AsQueryable()
        ));

        // Quotation for North Opportunity
        var quotationId = Guid.NewGuid();
        var quotation = new Quotation(quotationId, _companyId, _customerId, "QTN-001", DateTime.UtcNow)
        {
            OpportunityId = oppNorthId,
        };
        quotation.AddItem(Guid.NewGuid(), "Item A", 2, 4750m, 0); // Total 9500
        quotation.Submit();

        _quotationRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<Quotation> { quotation }.AsQueryable()
        ));

        // Sales Order for North Quotation
        var salesOrderId = Guid.NewGuid();
        var salesOrder = new SalesOrder(salesOrderId, _companyId, _customerId, "SO-001", DateTime.UtcNow)
        {
            QuotationId = quotationId,
        };
        salesOrder.AddItem(Guid.NewGuid(), "Item A", 2, 4500m, 0); // Total 9000
        salesOrder.Submit();

        _salesOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { salesOrder }.AsQueryable()));

        // Sales Invoice for North Sales Order
        var salesInvoiceId = Guid.NewGuid();
        var salesInvoice = new SalesInvoice(salesInvoiceId, _companyId, _customerId, "INV-001", DateTime.UtcNow);
        salesInvoice.AddItem(Guid.NewGuid(), "Item A", 2, 4500m, 0); // Total 9000
        salesInvoice.Items.First().SalesOrderItemId = salesOrder.Items.First().Id;
        salesInvoice.Submit();

        _salesInvoiceRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { salesInvoice }.AsQueryable()));

        var service = new TerritoryWiseSalesAppService(
            _territoryRepo,
            _opportunityRepo,
            _quotationRepo,
            _salesOrderRepo,
            _salesInvoiceRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetTerritoryWiseSalesInput
        {
            CompanyId = _companyId,
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(3); // North, South, Unassigned

        var northRow = result.Rows.FirstOrDefault(r => r.TerritoryName == "North Region");
        northRow.ShouldNotBeNull();
        northRow.OpportunityAmount.ShouldBe(10000m);
        northRow.OpportunityCount.ShouldBe(1);
        northRow.QuotationAmount.ShouldBe(9500m);
        northRow.QuotationCount.ShouldBe(1);
        northRow.OrderAmount.ShouldBe(9000m);
        northRow.OrderCount.ShouldBe(1);
        northRow.BillingAmount.ShouldBe(9000m);
        northRow.BillingCount.ShouldBe(1);

        var southRow = result.Rows.FirstOrDefault(r => r.TerritoryName == "South Region");
        southRow.ShouldNotBeNull();
        southRow.OpportunityAmount.ShouldBe(5000m);
        southRow.QuotationAmount.ShouldBe(0m);
        southRow.OrderAmount.ShouldBe(0m);
        southRow.BillingAmount.ShouldBe(0m);

        var unassignedRow = result.Rows.FirstOrDefault(r => r.TerritoryName == "Unassigned");
        unassignedRow.ShouldNotBeNull();
        unassignedRow.OpportunityAmount.ShouldBe(2000m);

        result.TotalOpportunityAmount.ShouldBe(17000m);
        result.TotalQuotationAmount.ShouldBe(9500m);
        result.TotalOrderAmount.ShouldBe(9000m);
        result.TotalBillingAmount.ShouldBe(9000m);
    }
}
