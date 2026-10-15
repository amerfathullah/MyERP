using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.CRM;

/// <summary>
/// Unit tests for Sales Funnel report per ERPNext PR #59941 / PR #59940.
/// Verifies:
/// - Quotations stage counts ONLY lead-originated quotations.
/// - Quotations originating from customer opportunities (LeadId == null) are excluded from the funnel.
/// - Draft / non-submitted quotations are excluded.
/// - Opportunities stage counts only lead-originated opportunities (LeadId != null).
/// - Converted stage counts only customers whose linked Lead belongs to the filtered company.
/// - Stage colors and titles match ERPNext standards.
/// </summary>
public class SalesFunnelReportTests
{
    private readonly IRepository<Lead, Guid> _leadRepo = Substitute.For<IRepository<Lead, Guid>>();
    private readonly IRepository<Opportunity, Guid> _oppRepo = Substitute.For<IRepository<Opportunity, Guid>>();
    private readonly IRepository<Quotation, Guid> _quotationRepo = Substitute.For<IRepository<Quotation, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<SalesStage, Guid> _stageRepo = Substitute.For<IRepository<SalesStage, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();

    private readonly Guid _companyA = Guid.NewGuid();
    private readonly Guid _companyB = Guid.NewGuid();

    private SalesPipelineAppService CreateAppService(
        List<Lead> leads,
        List<Opportunity> opportunities,
        List<Quotation> quotations,
        List<Customer> customers)
    {
        _leadRepo.GetQueryableAsync().Returns(Task.FromResult(leads.AsQueryable()));
        _oppRepo.GetQueryableAsync().Returns(Task.FromResult(opportunities.AsQueryable()));
        _quotationRepo.GetQueryableAsync().Returns(Task.FromResult(quotations.AsQueryable()));
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(customers.AsQueryable()));

        return new SalesPipelineAppService(
            _leadRepo,
            _oppRepo,
            _quotationRepo,
            _soRepo,
            _stageRepo,
            _customerRepo);
    }

    [Fact]
    public async Task GetSalesFunnelAsync_ExcludesCustomerOriginatedQuotations_PerPR59941()
    {
        var now = DateTime.UtcNow;

        // Lead in Company A
        var leadA = new Lead(Guid.NewGuid(), _companyA, "John Doe", "Acme Corp")
        {
            CreationTime = now
        };

        // Lead in Company B (should be excluded)
        var leadB = new Lead(Guid.NewGuid(), _companyB, "Jane Smith", "Beta Corp")
        {
            CreationTime = now
        };

        // Opportunity 1: originated from Lead A
        var oppLead = new Opportunity(Guid.NewGuid(), _companyA, "OPP-001", "Lead Deal")
        {
            LeadId = leadA.Id,
            CreationTime = now
        };

        // Opportunity 2: originated for an existing customer (LeadId is null)
        var customerExisting = new Customer(Guid.NewGuid(), _companyA, "Existing Customer")
        {
            LeadId = null,
            CreationTime = now.AddDays(-10)
        };
        var oppCustomer = new Opportunity(Guid.NewGuid(), _companyA, "OPP-002", "Existing Customer Upsell")
        {
            CustomerId = customerExisting.Id,
            LeadId = null,
            CreationTime = now
        };

        // Quotation 1: Submitted, created from oppLead (lead-originated) -> MUST BE COUNTED
        var qtnLead = new Quotation(Guid.NewGuid(), _companyA, customerExisting.Id, "QTN-001", now)
        {
            OpportunityId = oppLead.Id,
            CreationTime = now
        };
        qtnLead.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m);
        qtnLead.Submit();

        // Quotation 2: Submitted, created from oppCustomer (customer-originated) -> MUST BE EXCLUDED per PR #59941
        var qtnCustomer = new Quotation(Guid.NewGuid(), _companyA, customerExisting.Id, "QTN-002", now)
        {
            OpportunityId = oppCustomer.Id,
            CreationTime = now
        };
        qtnCustomer.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m);
        qtnCustomer.Submit();

        // Quotation 3: Draft, created from oppLead -> MUST BE EXCLUDED (not submitted)
        var qtnDraft = new Quotation(Guid.NewGuid(), _companyA, customerExisting.Id, "QTN-003", now)
        {
            OpportunityId = oppLead.Id,
            CreationTime = now
        };
        qtnDraft.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m);

        // Customer converted from Lead A
        var convertedCustomer = new Customer(Guid.NewGuid(), _companyA, "Converted Lead Customer")
        {
            LeadId = leadA.Id,
            CreationTime = now
        };

        // Customer converted from Lead B (Company B) -> MUST BE EXCLUDED from Company A funnel
        var convertedCustomerB = new Customer(Guid.NewGuid(), _companyB, "Company B Customer")
        {
            LeadId = leadB.Id,
            CreationTime = now
        };

        var appService = CreateAppService(
            new List<Lead> { leadA, leadB },
            new List<Opportunity> { oppLead, oppCustomer },
            new List<Quotation> { qtnLead, qtnCustomer, qtnDraft },
            new List<Customer> { customerExisting, convertedCustomer, convertedCustomerB });

        var funnel = await appService.GetSalesFunnelAsync(new SalesFunnelInputDto
        {
            CompanyId = _companyA,
            FromDate = now.AddDays(-1),
            ToDate = now.AddDays(1)
        });

        // Assertions
        funnel.ActiveLeads.ShouldBe(1); // leadA only
        funnel.Opportunities.ShouldBe(1); // oppLead only (oppCustomer has LeadId = null)
        funnel.Quotations.ShouldBe(1); // qtnLead only (qtnCustomer excluded because customer-originated, qtnDraft excluded because draft)
        funnel.Converted.ShouldBe(1); // convertedCustomer only (convertedCustomerB belongs to company B lead)

        funnel.Stages.Count.ShouldBe(4);
        funnel.Stages[0].Title.ShouldBe("Active Leads");
        funnel.Stages[0].Value.ShouldBe(1);
        funnel.Stages[0].Color.ShouldBe("#B03B46");

        funnel.Stages[1].Title.ShouldBe("Opportunities");
        funnel.Stages[1].Value.ShouldBe(1);
        funnel.Stages[1].Color.ShouldBe("#F09C00");

        funnel.Stages[2].Title.ShouldBe("Quotations");
        funnel.Stages[2].Value.ShouldBe(1);
        funnel.Stages[2].Color.ShouldBe("#006685");

        funnel.Stages[3].Title.ShouldBe("Converted");
        funnel.Stages[3].Value.ShouldBe(1);
        funnel.Stages[3].Color.ShouldBe("#00AD65");
    }
}
