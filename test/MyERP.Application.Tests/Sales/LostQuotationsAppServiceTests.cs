using System;
using System.Collections.Generic;
using System.Linq;
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

public class LostQuotationsAppServiceTests
{
    private readonly IRepository<Quotation, Guid> _quotationRepo = Substitute.For<IRepository<Quotation, Guid>>();
    private readonly IRepository<QuotationLostReasonDetail, Guid> _lostReasonDetailRepo = Substitute.For<IRepository<QuotationLostReasonDetail, Guid>>();
    private readonly IRepository<QuotationLostReason, Guid> _lostReasonRepo = Substitute.For<IRepository<QuotationLostReason, Guid>>();
    private readonly IRepository<CompetitorDetail, Guid> _competitorDetailRepo = Substitute.For<IRepository<CompetitorDetail, Guid>>();
    private readonly IRepository<Competitor, Guid> _competitorRepo = Substitute.For<IRepository<Competitor, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly LostQuotationsAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _reason1Id = Guid.NewGuid();
    private readonly Guid _reason2Id = Guid.NewGuid();
    private readonly Guid _competitor1Id = Guid.NewGuid();
    private readonly Guid _competitor2Id = Guid.NewGuid();

    public LostQuotationsAppServiceTests()
    {
        _appService = new LostQuotationsAppService(
            _quotationRepo,
            _lostReasonDetailRepo,
            _lostReasonRepo,
            _competitorDetailRepo,
            _competitorRepo,
            _companyRepo);

        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(new Company(_companyId, "Test Co") { CurrencyCode = "MYR" }));
    }

    [Fact]
    public async Task GetReportAsync_ShouldGroupAndSplitByLostReason_WhenQuotationsAreLost()
    {
        // Quotation 1: 1000 NetTotal, 1 reason ("Price too high")
        var q1 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QT-001", DateTime.UtcNow.Date);
        q1.AddItem(Guid.NewGuid(), "Item 1", 10, 100, 0);
        q1.Submit();
        q1.MarkLost("Too expensive");

        // Quotation 2: 2000 NetTotal, 2 reasons ("Price too high" and "Delivery too slow")
        var q2 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QT-002", DateTime.UtcNow.Date);
        q2.AddItem(Guid.NewGuid(), "Item 2", 20, 100, 0);
        q2.Submit();
        q2.MarkLost("Price and delivery");

        var quotations = new List<Quotation> { q1, q2 }.AsQueryable();
        _quotationRepo.GetQueryableAsync().Returns(Task.FromResult(quotations));

        var reasons = new List<QuotationLostReason>
        {
            new QuotationLostReason(_reason1Id, "Price too high"),
            new QuotationLostReason(_reason2Id, "Delivery too slow")
        }.AsQueryable();
        _lostReasonRepo.GetQueryableAsync().Returns(Task.FromResult(reasons));

        var reasonDetails = new List<QuotationLostReasonDetail>
        {
            new QuotationLostReasonDetail(Guid.NewGuid(), q1.Id, _reason1Id),
            new QuotationLostReasonDetail(Guid.NewGuid(), q2.Id, _reason1Id),
            new QuotationLostReasonDetail(Guid.NewGuid(), q2.Id, _reason2Id)
        }.AsQueryable();
        _lostReasonDetailRepo.GetQueryableAsync().Returns(Task.FromResult(reasonDetails));

        var result = await _appService.GetReportAsync(new GetLostQuotationsReportInput
        {
            CompanyId = _companyId,
            GroupBy = "Lost Reason",
            Timespan = "This Year"
        });

        result.Summary.TotalLostQuotations.ShouldBe(2);
        result.Summary.TotalLostValue.ShouldBe(3000m);
        result.Summary.Currency.ShouldBe("MYR");

        result.Rows.Count.ShouldBe(2);

        // Price too high:
        // q1 contributes 1000, q2 contributes 2000 / 2 = 1000 => total 2000
        // Distinct quotations: 2 / 2 = 100%
        var priceRow = result.Rows.First(r => r.Dimension == "Price too high");
        priceRow.LostQuotations.ShouldBe(2);
        priceRow.LostQuotationsPct.ShouldBe(100m);
        priceRow.LostValue.ShouldBe(2000m);
        priceRow.LostValuePct.ShouldBe(66.67m);

        // Delivery too slow:
        // q2 contributes 2000 / 2 = 1000 => total 1000
        // Distinct quotations: 1 / 2 = 50%
        var deliveryRow = result.Rows.First(r => r.Dimension == "Delivery too slow");
        deliveryRow.LostQuotations.ShouldBe(1);
        deliveryRow.LostQuotationsPct.ShouldBe(50m);
        deliveryRow.LostValue.ShouldBe(1000m);
        deliveryRow.LostValuePct.ShouldBe(33.33m);

        result.Chart.Labels.ShouldContain("Price too high");
        result.Chart.Labels.ShouldContain("Delivery too slow");
    }

    [Fact]
    public async Task GetReportAsync_ShouldGroupCompetitors_AndFallbackToNotSpecified_WhenNoCompetitorAssigned()
    {
        // Quotation 1: 1500 with Competitor Acme
        var q1 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QT-001", DateTime.UtcNow.Date);
        q1.AddItem(Guid.NewGuid(), "Item 1", 15, 100, 0);
        q1.Submit();
        q1.MarkLost();

        // Quotation 2: 500 with no competitor
        var q2 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QT-002", DateTime.UtcNow.Date);
        q2.AddItem(Guid.NewGuid(), "Item 2", 5, 100, 0);
        q2.Submit();
        q2.MarkLost();

        var quotations = new List<Quotation> { q1, q2 }.AsQueryable();
        _quotationRepo.GetQueryableAsync().Returns(Task.FromResult(quotations));

        var competitors = new List<Competitor>
        {
            new Competitor(_competitor1Id, "Acme Corp")
        }.AsQueryable();
        _competitorRepo.GetQueryableAsync().Returns(Task.FromResult(competitors));

        var compDetails = new List<CompetitorDetail>
        {
            new CompetitorDetail(Guid.NewGuid(), "Quotation", q1.Id, _competitor1Id)
        }.AsQueryable();
        _competitorDetailRepo.GetQueryableAsync().Returns(Task.FromResult(compDetails));

        var result = await _appService.GetReportAsync(new GetLostQuotationsReportInput
        {
            CompanyId = _companyId,
            GroupBy = "Competitor",
            Timespan = "This Year"
        });

        result.Summary.TotalLostQuotations.ShouldBe(2);
        result.Summary.TotalLostValue.ShouldBe(2000m);

        result.Rows.Count.ShouldBe(2);

        var acmeRow = result.Rows.First(r => r.Dimension == "Acme Corp");
        acmeRow.LostQuotations.ShouldBe(1);
        acmeRow.LostQuotationsPct.ShouldBe(50m);
        acmeRow.LostValue.ShouldBe(1500m);
        acmeRow.LostValuePct.ShouldBe(75m);

        var notSpecRow = result.Rows.First(r => r.Dimension == "Not Specified");
        notSpecRow.LostQuotations.ShouldBe(1);
        notSpecRow.LostQuotationsPct.ShouldBe(50m);
        notSpecRow.LostValue.ShouldBe(500m);
        notSpecRow.LostValuePct.ShouldBe(25m);
    }

    [Fact]
    public async Task GetReportAsync_ShouldExcludeNonRejectedOrInactiveQuotations()
    {
        // Submitted (active, not rejected)
        var q1 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QT-001", DateTime.UtcNow.Date);
        q1.AddItem(Guid.NewGuid(), "Item 1", 10, 100, 0);
        q1.Submit();

        // Draft
        var q2 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QT-002", DateTime.UtcNow.Date);
        q2.AddItem(Guid.NewGuid(), "Item 2", 10, 100, 0);

        // Rejected but Inactive
        var q3 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QT-003", DateTime.UtcNow.Date);
        q3.AddItem(Guid.NewGuid(), "Item 3", 10, 100, 0);
        q3.Submit();
        q3.MarkLost();
        q3.Deactivate();

        var quotations = new List<Quotation> { q1, q2, q3 }.AsQueryable();
        _quotationRepo.GetQueryableAsync().Returns(Task.FromResult(quotations));

        var result = await _appService.GetReportAsync(new GetLostQuotationsReportInput
        {
            CompanyId = _companyId,
            GroupBy = "Lost Reason",
            Timespan = "This Year"
        });

        result.Summary.TotalLostQuotations.ShouldBe(0);
        result.Summary.TotalLostValue.ShouldBe(0m);
        result.Rows.ShouldBeEmpty();
    }
}
