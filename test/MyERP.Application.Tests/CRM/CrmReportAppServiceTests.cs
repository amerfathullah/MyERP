using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using MyERP.Support.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

public abstract class CrmReportAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICrmReportAppService _crmReportAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Lead, Guid> _leadRepository;
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;
    private readonly IRepository<CrmNote, Guid> _crmNoteRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<Issue, Guid> _issueRepository;

    protected CrmReportAppServiceTests()
    {
        _crmReportAppService = GetRequiredService<ICrmReportAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _leadRepository = GetRequiredService<IRepository<Lead, Guid>>();
        _opportunityRepository = GetRequiredService<IRepository<Opportunity, Guid>>();
        _crmNoteRepository = GetRequiredService<IRepository<CrmNote, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _salesInvoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
        _issueRepository = GetRequiredService<IRepository<Issue, Guid>>();
    }

    [Fact]
    public async Task Should_Return_LeadDetails_And_ProspectsEngaged()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "CRM Report Co"), autoSave: true);

        var activeLead = new Lead(Guid.NewGuid(), company.Id, "LD-RPT-1", "Alice")
        {
            LastName = "Wong",
            CompanyName = "Beta Systems",
            Country = "Malaysia",
            Source = LeadSource.Website,
            Email = "alice@beta.my",
        };
        activeLead.MarkOpen();
        activeLead.MarkInterested();
        activeLead.Qualify();
        await _leadRepository.InsertAsync(activeLead, autoSave: true);

        var opp = new Opportunity(Guid.NewGuid(), company.Id, "OPP-RPT-1", "ERP Upgrade")
        {
            LeadId = activeLead.Id,
            SalesStage = "Proposal",
            OpportunityAmount = 50000m,
            ExpectedClosingDate = new DateTime(2026, 11, 20),
        };
        await _opportunityRepository.InsertAsync(opp, autoSave: true);

        await _crmNoteRepository.InsertAsync(
            activeLead.AddNote(Guid.NewGuid(), "Initial discovery call", Guid.NewGuid()), autoSave: true);
        await _crmNoteRepository.InsertAsync(
            opp.AddNote(Guid.NewGuid(), "Sent proposal deck", Guid.NewGuid()), autoSave: true);

        var details = await _crmReportAppService.GetLeadDetailsAsync(new LeadDetailsFilterDto
        {
            CompanyId = company.Id,
            Status = LeadStatus.Qualified,
            Country = "Malaysia",
            FromDate = DateTime.UtcNow.Date.AddDays(-1),
            ToDate = DateTime.UtcNow.Date.AddDays(1),
        });
        details.TotalCount.ShouldBe(1);
        details.Rows.Single().LeadName.ShouldBe("Alice Wong");
        details.Rows.Single().Country.ShouldBe("Malaysia");

        var engaged = await _crmReportAppService.GetProspectsEngagedButNotConvertedAsync(new ProspectsEngagedFilterDto
        {
            CompanyId = company.Id,
            LeadAgeDays = 60,
            MinInteractions = 2,
        });
        engaged.TotalCount.ShouldBe(1);
        var row = engaged.Rows.Single();
        row.LeadNumber.ShouldBe("LD-RPT-1");
        row.InteractionCount.ShouldBe(2);
        row.ReferenceType.ShouldBe("Opportunity");
        row.ReferenceName.ShouldBe("OPP-RPT-1");
    }

    [Fact]
    public async Task Should_Return_LeadConversionTime_And_SalesPipelineAnalytics()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "CRM Pipeline Co"), autoSave: true);

        var lead = new Lead(Guid.NewGuid(), company.Id, "LD-CONV-1", "Bob")
        {
            LastName = "Lim",
        };
        var customer = new Customer(Guid.NewGuid(), company.Id, "Converted Co")
        {
            LeadId = lead.Id,
        };
        lead.ConvertToCustomer(customer.Id);
        await _leadRepository.InsertAsync(lead, autoSave: true);
        await _customerRepository.InsertAsync(customer, autoSave: true);

        await _crmNoteRepository.InsertAsync(
            lead.AddNote(Guid.NewGuid(), "Demo completed", Guid.NewGuid()), autoSave: true);

        var invoiceDate = DateTime.UtcNow.Date.AddDays(5);
        var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SINV-CRM-1", invoiceDate);
        si.AddItem(Guid.NewGuid(), "License", 1m, 1000m, 0m);
        si.Submit();
        si.Post();
        await _salesInvoiceRepository.InsertAsync(si, autoSave: true);

        await _issueRepository.InsertAsync(
            new Issue(Guid.NewGuid(), company.Id, "Onboarding question") { CustomerId = customer.Id },
            autoSave: true);

        var conv = await _crmReportAppService.GetLeadConversionTimeAsync(new CrmReportDateFilterDto
        {
            CompanyId = company.Id,
            FromDate = DateTime.UtcNow.Date.AddDays(-1),
            ToDate = invoiceDate.AddDays(1),
        });
        conv.TotalConverted.ShouldBe(1);
        conv.Rows.Single().CustomerName.ShouldBe("Converted Co");
        conv.Rows.Single().Interactions.ShouldBe(1);
        conv.Rows.Single().DurationInDays.ShouldBe(5m);
        conv.Rows.Single().SupportTickets.ShouldBe(1);

        var oppOct = new Opportunity(Guid.NewGuid(), company.Id, "OPP-OCT", "Deal Oct")
        {
            SalesStage = "Qualification",
            OpportunityAmount = 2000m,
            ExpectedClosingDate = new DateTime(2026, 10, 15),
        };
        var oppNov = new Opportunity(Guid.NewGuid(), company.Id, "OPP-NOV", "Deal Nov")
        {
            SalesStage = "Qualification",
            OpportunityAmount = 3500m,
            ExpectedClosingDate = new DateTime(2026, 11, 20),
        };
        await _opportunityRepository.InsertAsync(oppOct, autoSave: true);
        await _opportunityRepository.InsertAsync(oppNov, autoSave: true);

        var pipeline = await _crmReportAppService.GetSalesPipelineAnalyticsAsync(new SalesPipelineAnalyticsFilterDto
        {
            CompanyId = company.Id,
            PipelineBy = "Sales Stage",
            BasedOn = "Amount",
            Range = "Monthly",
            FromDate = new DateTime(2026, 10, 1),
            ToDate = new DateTime(2026, 11, 30),
        });
        pipeline.PeriodLabels.ShouldBe(new[] { "Oct", "Nov" });
        pipeline.GrandTotal.ShouldBe(5500m);
        var qualRow = pipeline.Rows.Single(r => r.GroupLabel == "Qualification");
        qualRow.PeriodValues.ShouldBe(new[] { 2000m, 3500m });

        await Should.ThrowAsync<BusinessException>(() =>
            _crmReportAppService.GetSalesPipelineAnalyticsAsync(new SalesPipelineAnalyticsFilterDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 11, 30),
                ToDate = new DateTime(2026, 11, 1),
            }));
    }
}
