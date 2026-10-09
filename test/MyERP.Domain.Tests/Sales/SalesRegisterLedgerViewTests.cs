using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Sales Register:
/// - Tree hierarchy resolution for customer groups (ERPNext PR #59930 / commit 1baaec920e)
/// - Ledger view journal entries (prior & in-period) with JournalEntryId (ERPNext PR #59888 / commit 4f95812874)
/// - Rounding adjustment debit vs grand total debit (ERPNext PR #59930 / commit 1ae23fadef)
/// </summary>
public class SalesRegisterLedgerViewTests
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<Customer, Guid> _custRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<CustomerGroup, Guid> _custGroupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();

    private readonly SalesRegisterAppService _salesRegisterAppService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerGroupIdCorporate = Guid.NewGuid();
    private readonly Guid _customerGroupIdRetail = Guid.NewGuid();

    private readonly Customer _customerCorporate;
    private readonly Customer _customerRetail;
    private readonly CustomerGroup _groupCorporate;
    private readonly CustomerGroup _groupRetail;

    public SalesRegisterLedgerViewTests()
    {
        _salesRegisterAppService = new SalesRegisterAppService(
            _siRepo, _peRepo, _custRepo, _custGroupRepo, _journalRepo);

        _groupCorporate = new CustomerGroup(_customerGroupIdCorporate, "Corporate");
        _groupRetail = new CustomerGroup(_customerGroupIdRetail, "Retail");

        _customerCorporate = new Customer(Guid.NewGuid(), _companyId, "Globex Corp")
        {
            CustomerGroupId = _customerGroupIdCorporate
        };

        _customerRetail = new Customer(Guid.NewGuid(), _companyId, "Alice Johnson")
        {
            CustomerGroupId = _customerGroupIdRetail
        };

        var customers = new List<Customer> { _customerCorporate, _customerRetail };
        _custRepo.GetQueryableAsync().Returns(Task.FromResult(customers.AsQueryable()));
        _custRepo.FindAsync(_customerCorporate.Id).Returns(Task.FromResult<Customer?>(_customerCorporate));
        _custRepo.FindAsync(_customerRetail.Id).Returns(Task.FromResult<Customer?>(_customerRetail));

        var groups = new List<CustomerGroup> { _groupCorporate, _groupRetail };
        _custGroupRepo.GetQueryableAsync().Returns(Task.FromResult(groups.AsQueryable()));
        _custGroupRepo.GetListAsync().Returns(Task.FromResult(groups));

        _peRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentEntry>().AsQueryable()));
        _peRepo.WithDetailsAsync(Arg.Any<Expression<Func<PaymentEntry, object>>[]>())
            .Returns(Task.FromResult(new List<PaymentEntry>().AsQueryable()));

        _journalRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry>().AsQueryable()));
        _journalRepo.WithDetailsAsync(Arg.Any<Expression<Func<JournalEntry, object>>[]>())
            .Returns(Task.FromResult(new List<JournalEntry>().AsQueryable()));
    }

    [Fact]
    public async Task SalesRegister_CustomerGroupFilter_UsesCustomerMaster()
    {
        var issueDate = DateTime.UtcNow.Date;

        var si1 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerCorporate.Id, "SINV-001", issueDate);
        si1.AddItem(Guid.NewGuid(), "Enterprise License", 1m, 1000m, 0m);
        si1.Submit();
        si1.Post();

        var si2 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerRetail.Id, "SINV-002", issueDate);
        si2.AddItem(Guid.NewGuid(), "Retail Gadget", 2m, 50m, 0m);
        si2.Submit();
        si2.Post();

        _siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { si1, si2 }.AsQueryable()));

        var report = await _salesRegisterAppService.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = issueDate.AddDays(-1),
            ToDate = issueDate.AddDays(1),
            CustomerGroupId = _customerGroupIdCorporate
        });

        Assert.Equal(1, report.Count);
        var line = report.Items.Single();
        Assert.Equal("SINV-001", line.InvoiceNumber);
        Assert.Equal(_customerCorporate.Id, line.CustomerId);
        Assert.Equal("Globex Corp", line.CustomerName);
        Assert.Equal(_customerGroupIdCorporate, line.CustomerGroupId);
        Assert.Equal("Corporate", line.CustomerGroupName);
    }

    [Fact]
    public async Task SalesRegister_GroupFilters_IncludeChildren()
    {
        var rootGroupId = Guid.NewGuid();
        var childGroupId = Guid.NewGuid();
        var rootGroup = new CustomerGroup(rootGroupId, "All Customers", parentId: null, isGroup: true);
        var childGroup = new CustomerGroup(childGroupId, "Direct Consumers", parentId: rootGroupId, isGroup: false);

        var childCustomer = new Customer(Guid.NewGuid(), _companyId, "Bob Smith")
        {
            CustomerGroupId = childGroupId
        };

        var allCustomers = new List<Customer> { _customerCorporate, _customerRetail, childCustomer };
        _custRepo.GetQueryableAsync().Returns(Task.FromResult(allCustomers.AsQueryable()));
        _custRepo.FindAsync(childCustomer.Id).Returns(Task.FromResult<Customer?>(childCustomer));

        var allGroups = new List<CustomerGroup> { _groupCorporate, _groupRetail, rootGroup, childGroup };
        _custGroupRepo.GetQueryableAsync().Returns(Task.FromResult(allGroups.AsQueryable()));
        _custGroupRepo.GetListAsync().Returns(Task.FromResult(allGroups));

        var issueDate = DateTime.UtcNow.Date;
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, childCustomer.Id, "SINV-TREE", issueDate);
        si.AddItem(Guid.NewGuid(), "Consumer Product", 1m, 120m, 0m);
        si.Submit();
        si.Post();

        _siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { si }.AsQueryable()));

        var report = await _salesRegisterAppService.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = issueDate.AddDays(-1),
            ToDate = issueDate.AddDays(1),
            CustomerGroupId = rootGroupId
        });

        Assert.Equal(1, report.Count);
        Assert.Equal("SINV-TREE", report.Items[0].InvoiceNumber);
        Assert.Equal(childCustomer.Id, report.Items[0].CustomerId);
        Assert.Equal("Bob Smith", report.Items[0].CustomerName);
        Assert.Equal(childGroupId, report.Items[0].CustomerGroupId);
        Assert.Equal("Direct Consumers", report.Items[0].CustomerGroupName);
    }

    [Fact]
    public async Task SalesRegister_LedgerView_IncludesJournalEntries()
    {
        var baseDate = DateTime.UtcNow.Date;
        var fromDate = baseDate;
        var toDate = baseDate.AddDays(10);

        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();

        // Prior JE before fromDate (debits receivable for customer by 400)
        var priorJe = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), fromDate.AddDays(-2));
        priorJe.EntryNumber = "JE-CUST-PRIOR";
        priorJe.AddFullLine(accountId1, 400m, isDebit: true, description: "Debit receivable", partyId: _customerCorporate.Id, partyType: "Customer");
        priorJe.AddFullLine(accountId2, 400m, isDebit: false, description: "Credit income", partyId: null, partyType: null);
        priorJe.Post();

        // In-period JE (debits receivable for customer by 250)
        var periodJe = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), fromDate.AddDays(3));
        periodJe.EntryNumber = "JE-CUST-PERIOD";
        periodJe.AddFullLine(accountId1, 250m, isDebit: true, description: "Debit receivable", partyId: _customerCorporate.Id, partyType: "Customer");
        periodJe.AddFullLine(accountId2, 250m, isDebit: false, description: "Credit income", partyId: null, partyType: null);
        periodJe.Post();

        var journals = new List<JournalEntry> { priorJe, periodJe };
        _journalRepo.GetQueryableAsync().Returns(Task.FromResult(journals.AsQueryable()));
        _journalRepo.WithDetailsAsync(Arg.Any<Expression<Func<JournalEntry, object>>[]>())
            .Returns(Task.FromResult(journals.AsQueryable()));

        _siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice>().AsQueryable()));

        var report = await _salesRegisterAppService.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate,
            CustomerId = _customerCorporate.Id,
            IncludePayments = true
        });

        // Opening row: priorJe had Debit: 400m and Credit: 0m => opening balance = +400 (debit balance)
        var opening = report.Items.First(l => l.VoucherType == "Opening");
        Assert.Equal(400m, opening.Balance);
        Assert.Equal(400m, opening.Debit);

        // Period rows must include JE line
        var jeLines = report.Items.Where(l => l.VoucherType == "Journal Entry").ToList();
        Assert.Single(jeLines);
        var jeLine = jeLines[0];
        Assert.Equal(periodJe.Id, jeLine.JournalEntryId);
        Assert.Equal("JE-CUST-PERIOD", jeLine.InvoiceNumber);
        Assert.Equal(250m, jeLine.Debit);
        Assert.Equal(0m, jeLine.Credit);
        Assert.Equal(650m, jeLine.Balance);
    }

    [Fact]
    public async Task SalesRegister_LedgerView_RoundingAdjustment_DebitsRoundedTotal()
    {
        var issueDate = DateTime.UtcNow.Date;

        // Invoice with rounding adjustment (e.g. GrandTotal = 99.40, Rounded = 100.00, RoundingAdjustment = 0.60)
        var siRounded = new SalesInvoice(Guid.NewGuid(), _companyId, _customerCorporate.Id, "SINV-ROUNDED", issueDate);
        siRounded.AddItem(Guid.NewGuid(), "Item A", 1m, 99.40m, 0m);
        siRounded.BaseGrandTotal = 99.40m;
        siRounded.BaseRoundedTotal = 100.00m;
        siRounded.BaseRoundingAdjustment = 0.60m;
        siRounded.Submit();
        siRounded.Post();

        // Invoice without rounding adjustment
        var siExact = new SalesInvoice(Guid.NewGuid(), _companyId, _customerCorporate.Id, "SINV-EXACT", issueDate);
        siExact.AddItem(Guid.NewGuid(), "Item B", 1m, 50.00m, 0m);
        siExact.BaseGrandTotal = 50.00m;
        siExact.BaseRoundedTotal = 0m;
        siExact.BaseRoundingAdjustment = 0m;
        siExact.Submit();
        siExact.Post();

        _siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { siRounded, siExact }.AsQueryable()));

        var report = await _salesRegisterAppService.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = issueDate.AddDays(-1),
            ToDate = issueDate.AddDays(1),
            CustomerId = _customerCorporate.Id,
            IncludePayments = true
        });

        var lineRounded = report.Items.First(l => l.InvoiceNumber == "SINV-ROUNDED");
        var lineExact = report.Items.First(l => l.InvoiceNumber == "SINV-EXACT");

        // When BaseRoundingAdjustment != 0 && BaseRoundedTotal != 0 => Debit is BaseRoundedTotal (100.00)
        Assert.Equal(100.00m, lineRounded.Debit);

        // When no rounding adjustment => Debit is BaseGrandTotal (50.00)
        Assert.Equal(50.00m, lineExact.Debit);
    }
}
