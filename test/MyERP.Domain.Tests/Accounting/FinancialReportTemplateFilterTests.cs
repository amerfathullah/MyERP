using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class FinancialReportTemplateFilterTests
{
    private readonly IRepository<FinancialReportTemplate, Guid> _templateRepo;
    private readonly IRepository<Account, Guid> _accountRepo;
    private readonly IRepository<AccountCategory> _categoryRepo;
    private readonly IRepository<JournalEntry, Guid> _journalRepo;
    private readonly FinancialReportFormulaEngine _formulaEngine;
    private readonly FinancialReportTemplateAppService _service;

    public FinancialReportTemplateFilterTests()
    {
        _templateRepo = Substitute.For<IRepository<FinancialReportTemplate, Guid>>();
        _accountRepo = Substitute.For<IRepository<Account, Guid>>();
        _categoryRepo = Substitute.For<IRepository<AccountCategory>>();
        _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        _formulaEngine = new FinancialReportFormulaEngine(
            _journalRepo,
            _categoryRepo,
            _accountRepo);
        _service = new FinancialReportTemplateAppService(
            _templateRepo,
            _formulaEngine,
            _accountRepo,
            _categoryRepo);
    }

    [Fact]
    public async Task GetFilteredAccounts_Throws_WhenCompanyIdEmpty()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.GetFilteredAccountsAsync(new GetFilteredAccountsDto
            {
                CompanyId = Guid.Empty
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldBe("Company is required");
    }

    [Fact]
    public async Task GetFilteredAccounts_Throws_WhenFieldNotWhitelisted()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.GetFilteredAccountsAsync(new GetFilteredAccountsDto
            {
                CompanyId = Guid.NewGuid(),
                AccountRows = new List<AccountFilterRowDto>
                {
                    new() { Field = "tax_rate", Operator = "=", Value = "0.06" }
                }
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Field 'tax_rate' is not a valid Account field");
    }

    [Fact]
    public async Task GetFilteredAccounts_EscapesHtml_InInvalidField()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.GetFilteredAccountsAsync(new GetFilteredAccountsDto
            {
                CompanyId = Guid.NewGuid(),
                AccountRows = new List<AccountFilterRowDto>
                {
                    new() { Field = "<script>alert(1)</script>", Operator = "=", Value = "test" }
                }
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Fact]
    public async Task GetFilteredAccounts_Throws_WhenOperatorNotAllowed()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.GetFilteredAccountsAsync(new GetFilteredAccountsDto
            {
                CompanyId = Guid.NewGuid(),
                AccountRows = new List<AccountFilterRowDto>
                {
                    new() { Field = "root_type", Operator = "DROP TABLE", Value = "Income" }
                }
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Invalid operator 'DROP TABLE'");
    }

    [Fact]
    public async Task GetFilteredAccounts_Parses_JsonArray_Formula()
    {
        var companyId = Guid.NewGuid();
        var accounts = new List<Account>
        {
            new(Guid.NewGuid(), companyId, "4001", "Sales Revenue", AccountType.Revenue) { IsActive = true, IsGroup = false, Currency = "MYR" },
            new(Guid.NewGuid(), companyId, "5001", "Cost of Goods Sold", AccountType.Expense) { IsActive = true, IsGroup = false, Currency = "MYR" }
        };
        _accountRepo.GetQueryableAsync().Returns(Task.FromResult(accounts.AsQueryable()));
        _categoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<AccountCategory>().AsQueryable()));

        var result = await _service.GetFilteredAccountsAsync(new GetFilteredAccountsDto
        {
            CompanyId = companyId,
            AccountRows = new List<AccountFilterRowDto>
            {
                new() { CalculationFormula = "[\"root_type\", \"=\", \"Income\"]" }
            }
        });

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].AccountCode.ShouldBe("4001");
        result[0].RootType.ShouldBe("Income");
    }

    [Fact]
    public async Task GetFilteredAccounts_Parses_JsonObject_Formula()
    {
        var companyId = Guid.NewGuid();
        var accounts = new List<Account>
        {
            new(Guid.NewGuid(), companyId, "1001", "Bank MYR", AccountType.Asset) { IsActive = true, IsGroup = false, Currency = "MYR" },
            new(Guid.NewGuid(), companyId, "1002", "Bank USD", AccountType.Asset) { IsActive = true, IsGroup = false, Currency = "USD" }
        };
        _accountRepo.GetQueryableAsync().Returns(Task.FromResult(accounts.AsQueryable()));
        _categoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<AccountCategory>().AsQueryable()));

        var result = await _service.GetFilteredAccountsAsync(new GetFilteredAccountsDto
        {
            CompanyId = companyId,
            AccountRows = new List<AccountFilterRowDto>
            {
                new() { CalculationFormula = "{\"field\": \"currency\", \"operator\": \"=\", \"value\": \"USD\"}" }
            }
        });

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].AccountCode.ShouldBe("1002");
        result[0].Currency.ShouldBe("USD");
    }

    [Fact]
    public async Task GetFilteredAccounts_FiltersLeafAccountsOnly()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var accounts = new List<Account>
        {
            new(Guid.NewGuid(), companyId, "4000", "Revenue Group", AccountType.Revenue) { IsActive = true, IsGroup = true },
            new(Guid.NewGuid(), companyId, "4001", "Product Sales", AccountType.Revenue) { IsActive = true, IsGroup = false, Currency = "MYR" },
            new(Guid.NewGuid(), companyId, "4002", "Service Revenue", AccountType.Revenue) { IsActive = false, IsGroup = false, Currency = "MYR" },
            new(Guid.NewGuid(), otherCompanyId, "4003", "Other Co Sales", AccountType.Revenue) { IsActive = true, IsGroup = false, Currency = "MYR" }
        };
        _accountRepo.GetQueryableAsync().Returns(Task.FromResult(accounts.AsQueryable()));
        _categoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<AccountCategory>().AsQueryable()));

        var result = await _service.GetFilteredAccountsAsync(new GetFilteredAccountsDto
        {
            CompanyId = companyId,
            AccountRows = new List<AccountFilterRowDto>
            {
                new() { Field = "root_type", Operator = "=", Value = "Income" }
            }
        });

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].AccountCode.ShouldBe("4001");
        result[0].AccountName.ShouldBe("Product Sales");
    }
}
