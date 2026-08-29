using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.DomainServices;

public class ChartOfAccountsImportTests
{
    [Fact]
    public void GetMalaysianTemplate_ReturnsNonEmptyList()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();
        template.ShouldNotBeEmpty();
        template.Count.ShouldBeGreaterThan(50);
    }

    [Fact]
    public void GetMalaysianTemplate_Has5RootAccounts()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();
        var roots = template.Where(r => r.ParentCode == null).ToList();
        roots.Count.ShouldBe(5); // Assets, Liabilities, Equity, Revenue, Expenses
    }

    [Fact]
    public void GetMalaysianTemplate_AllCodesUnique()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();
        var codes = template.Select(r => r.AccountCode).ToList();
        codes.Distinct().Count().ShouldBe(codes.Count);
    }

    [Fact]
    public void GetMalaysianTemplate_AllParentCodesExistInTemplate()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();
        var codes = template.Select(r => r.AccountCode).ToHashSet();

        foreach (var row in template.Where(r => r.ParentCode != null))
        {
            codes.ShouldContain(row.ParentCode!,
                $"Parent code '{row.ParentCode}' for account '{row.AccountCode}' not found in template");
        }
    }

    [Fact]
    public void GetMalaysianTemplate_HasMalaysianPayrollAccounts()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();

        // Verify Malaysian statutory payroll accounts exist
        template.ShouldContain(r => r.AccountName == "EPF Payable");
        template.ShouldContain(r => r.AccountName == "SOCSO Payable");
        template.ShouldContain(r => r.AccountName == "EIS Payable");
        template.ShouldContain(r => r.AccountName == "PCB/MTD Payable");
        template.ShouldContain(r => r.AccountName == "Employer EPF Contribution");
        template.ShouldContain(r => r.AccountName == "Employer SOCSO Contribution");
        template.ShouldContain(r => r.AccountName == "Employer EIS Contribution");
    }

    [Fact]
    public void GetMalaysianTemplate_HasSSTAccount()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();
        template.ShouldContain(r => r.AccountName == "SST Payable");
    }

    [Fact]
    public void GetMalaysianTemplate_GroupAccountsHaveChildren()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();
        var groups = template.Where(r => r.IsGroup).ToList();

        foreach (var group in groups)
        {
            var children = template.Where(r => r.ParentCode == group.AccountCode).ToList();
            children.ShouldNotBeEmpty(
                $"Group account '{group.AccountCode} - {group.AccountName}' has no children");
        }
    }

    [Fact]
    public void GetMalaysianTemplate_LeafAccountsNotGroups()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();
        var parentCodes = template.Where(r => r.ParentCode != null).Select(r => r.ParentCode).ToHashSet();

        var leafAccounts = template.Where(r => !parentCodes.Contains(r.AccountCode)).ToList();
        foreach (var leaf in leafAccounts)
        {
            leaf.IsGroup.ShouldBeFalse(
                $"Account '{leaf.AccountCode} - {leaf.AccountName}' is a leaf but marked as group");
        }
    }

    [Fact]
    public void GetMalaysianTemplate_AccountTypesCorrect()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();

        // 1xxx = Assets
        template.Where(r => r.AccountCode.StartsWith("1"))
            .All(r => r.AccountType == AccountType.Asset).ShouldBeTrue();

        // 2xxx = Liabilities
        template.Where(r => r.AccountCode.StartsWith("2"))
            .All(r => r.AccountType == AccountType.Liability).ShouldBeTrue();

        // 3xxx = Equity
        template.Where(r => r.AccountCode.StartsWith("3"))
            .All(r => r.AccountType == AccountType.Equity).ShouldBeTrue();

        // 4xxx = Revenue
        template.Where(r => r.AccountCode.StartsWith("4"))
            .All(r => r.AccountType == AccountType.Revenue).ShouldBeTrue();

        // 5xxx = Expenses
        template.Where(r => r.AccountCode.StartsWith("5"))
            .All(r => r.AccountType == AccountType.Expense).ShouldBeTrue();
    }

    [Fact]
    public void CoaTemplateRow_Record_Properties()
    {
        var row = new CoaTemplateRow("1000", "Assets", AccountType.Asset, isGroup: true);

        row.AccountCode.ShouldBe("1000");
        row.AccountName.ShouldBe("Assets");
        row.AccountType.ShouldBe(AccountType.Asset);
        row.IsGroup.ShouldBeTrue();
        row.ParentCode.ShouldBeNull();
        row.AccountSubType.ShouldBeNull();
    }

    [Fact]
    public void CoaTemplateRow_WithSubType()
    {
        var row = new CoaTemplateRow("1130", "Accounts Receivable", AccountType.Asset,
            parentCode: "1100", subType: AccountSubType.AccountsReceivable);

        row.AccountSubType.ShouldBe(AccountSubType.AccountsReceivable);
        row.ParentCode.ShouldBe("1100");
        row.IsGroup.ShouldBeFalse();
    }

    [Fact]
    public void GetMalaysianTemplate_HasInventoryAndCOGS()
    {
        var template = ChartOfAccountsImportService.GetMalaysianTemplate();

        var inventory = template.FirstOrDefault(r => r.AccountCode == "1140");
        inventory.ShouldNotBeNull();
        inventory!.AccountSubType.ShouldBe(AccountSubType.Stock);

        var cogs = template.FirstOrDefault(r => r.AccountCode == "5100");
        cogs.ShouldNotBeNull();
        cogs!.AccountName.ShouldBe("Cost of Goods Sold");
    }

    [Fact]
    public async Task ImportAsync_WithCompany_UnsetsAndReassignsCompanyDefaults()
    {
        var companyId = Guid.NewGuid();
        var company = new MyERP.Core.Entities.Company(companyId, "Test Co");

        var accountRepo = Substitute.For<IRepository<Account, Guid>>();
        var journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var companyRepo = Substitute.For<IRepository<MyERP.Core.Entities.Company, Guid>>();

        journalRepo.AnyAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(false));

        companyRepo.FindAsync(companyId).Returns(Task.FromResult<MyERP.Core.Entities.Company?>(company));

        var importService = new ChartOfAccountsImportService(accountRepo, journalRepo, companyRepo);
        var rows = new List<CoaTemplateRow>
        {
            new("1000", "Assets", AccountType.Asset, isGroup: true),
            new("1100", "Current Assets", AccountType.Asset, isGroup: true, parentCode: "1000"),
            new("1130", "Accounts Receivable", AccountType.Asset, parentCode: "1100", subType: AccountSubType.AccountsReceivable),
            new("2000", "Liabilities", AccountType.Liability, isGroup: true),
            new("2100", "Current Liabilities", AccountType.Liability, isGroup: true, parentCode: "2000"),
            new("2110", "Accounts Payable", AccountType.Liability, parentCode: "2100", subType: AccountSubType.AccountsPayable),
        };

        var count = await importService.ImportAsync(companyId, rows);
        count.ShouldBe(6);

        company.DefaultReceivableAccountId.ShouldNotBeNull();
        company.DefaultPayableAccountId.ShouldNotBeNull();
    }
}
