using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

public class MultiCurrencyAndDimensionTests
{
    #region JournalEntryLine Multi-Currency

    [Fact]
    public void JournalEntryLine_DefaultExchangeRate_IsOne()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);

        line.ExchangeRate.ShouldBe(1m);
        line.AmountInAccountCurrency.ShouldBe(100m); // Same as Amount for same-currency
    }

    [Fact]
    public void JournalEntryLine_MultiCurrencyConstructor_SetsAllFields()
    {
        var lineId = Guid.NewGuid();
        var jeId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var line = new JournalEntryLine(lineId, jeId, accountId,
            amountInCompanyCurrency: 4720m,
            isDebit: true,
            accountCurrency: "USD",
            amountInAccountCurrency: 1000m,
            exchangeRate: 4.72m,
            description: "USD receivable");

        line.Amount.ShouldBe(4720m); // Company currency (MYR)
        line.AmountInAccountCurrency.ShouldBe(1000m); // Account currency (USD)
        line.AccountCurrency.ShouldBe("USD");
        line.ExchangeRate.ShouldBe(4.72m);
        line.IsDebit.ShouldBeTrue();
        line.Description.ShouldBe("USD receivable");
    }

    [Fact]
    public void JournalEntryLine_SameCurrency_AmountsEqual()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            amountInCompanyCurrency: 500m,
            isDebit: false,
            accountCurrency: "MYR",
            amountInAccountCurrency: 500m,
            exchangeRate: 1m);

        line.Amount.ShouldBe(line.AmountInAccountCurrency);
        line.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void JournalEntryLine_ProjectId_DefaultNull()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        line.ProjectId.ShouldBeNull();
    }

    [Fact]
    public void JournalEntryLine_ProjectId_CanBeSet()
    {
        var projectId = Guid.NewGuid();
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        line.ProjectId = projectId;
        line.ProjectId.ShouldBe(projectId);
    }

    [Fact]
    public void JournalEntryLine_AgainstVoucher_DefaultNull()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        line.AgainstVoucherType.ShouldBeNull();
        line.AgainstVoucherId.ShouldBeNull();
    }

    [Fact]
    public void JournalEntryLine_AgainstVoucher_CanBeSet()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        line.AgainstVoucherType = "SalesInvoice";
        line.AgainstVoucherId = Guid.NewGuid();
        line.AgainstVoucherType.ShouldBe("SalesInvoice");
        line.AgainstVoucherId.ShouldNotBeNull();
    }

    [Fact]
    public void JournalEntryLine_IsAdvance_DefaultFalse()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        line.IsAdvance.ShouldBeFalse();
    }

    [Fact]
    public void JournalEntryLine_FinanceBook_DefaultNull()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        line.FinanceBook.ShouldBeNull();
    }

    [Fact]
    public void JournalEntryLine_FinanceBook_CanBeSet()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        line.FinanceBook = "TaxBook";
        line.FinanceBook.ShouldBe("TaxBook");
    }

    #endregion

    #region AccountingDimension

    [Fact]
    public void AccountingDimension_Create_SetsFieldName()
    {
        var dim = new AccountingDimension(Guid.NewGuid(), "CostCenter", "Cost Center");

        dim.DocumentType.ShouldBe("CostCenter");
        dim.Label.ShouldBe("Cost Center");
        dim.FieldName.ShouldBe("cost_center_id");
        dim.IsEnabled.ShouldBeTrue();
        dim.IsMandatory.ShouldBeFalse();
    }

    [Fact]
    public void AccountingDimension_FieldName_MultiWordType()
    {
        var dim = new AccountingDimension(Guid.NewGuid(), "BusinessUnit", "Business Unit");
        dim.FieldName.ShouldBe("business_unit_id");
    }

    [Fact]
    public void AccountingDimension_FieldName_SingleWordType()
    {
        var dim = new AccountingDimension(Guid.NewGuid(), "Branch", "Branch");
        dim.FieldName.ShouldBe("branch_id");
    }

    [Fact]
    public void AccountingDimension_Disable_SetsFlag()
    {
        var dim = new AccountingDimension(Guid.NewGuid(), "Department", "Department");
        dim.Disable();
        dim.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void AccountingDimension_Enable_RestoresFlag()
    {
        var dim = new AccountingDimension(Guid.NewGuid(), "Department", "Department");
        dim.Disable();
        dim.Enable();
        dim.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void AccountingDimension_CompanyId_DefaultNull()
    {
        var dim = new AccountingDimension(Guid.NewGuid(), "Region", "Region");
        dim.CompanyId.ShouldBeNull(); // Global dimension
    }

    [Fact]
    public void AccountingDimension_CompanyId_CanBeScoped()
    {
        var companyId = Guid.NewGuid();
        var dim = new AccountingDimension(Guid.NewGuid(), "Region", "Region");
        dim.CompanyId = companyId;
        dim.CompanyId.ShouldBe(companyId);
    }

    #endregion

    #region AccountingDimensionFilter

    [Fact]
    public void DimensionFilter_AllowList_PermitsIncludedValue()
    {
        var valueId = Guid.NewGuid();
        var filter = new AccountingDimensionFilter(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isAllowList: true);
        filter.DimensionValueIds = valueId.ToString();

        filter.IsValuePermitted(valueId).ShouldBeTrue();
    }

    [Fact]
    public void DimensionFilter_AllowList_BlocksExcludedValue()
    {
        var allowedId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var filter = new AccountingDimensionFilter(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isAllowList: true);
        filter.DimensionValueIds = allowedId.ToString();

        filter.IsValuePermitted(otherId).ShouldBeFalse();
    }

    [Fact]
    public void DimensionFilter_BlockList_PermitsNonBlockedValue()
    {
        var blockedId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var filter = new AccountingDimensionFilter(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isAllowList: false);
        filter.DimensionValueIds = blockedId.ToString();

        filter.IsValuePermitted(otherId).ShouldBeTrue();
    }

    [Fact]
    public void DimensionFilter_BlockList_BlocksBlockedValue()
    {
        var blockedId = Guid.NewGuid();
        var filter = new AccountingDimensionFilter(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isAllowList: false);
        filter.DimensionValueIds = blockedId.ToString();

        filter.IsValuePermitted(blockedId).ShouldBeFalse();
    }

    [Fact]
    public void DimensionFilter_EmptyAllowList_BlocksAll()
    {
        var filter = new AccountingDimensionFilter(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isAllowList: true);
        filter.DimensionValueIds = "";

        filter.IsValuePermitted(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void DimensionFilter_EmptyBlockList_AllowsAll()
    {
        var filter = new AccountingDimensionFilter(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isAllowList: false);
        filter.DimensionValueIds = "";

        filter.IsValuePermitted(Guid.NewGuid()).ShouldBeTrue();
    }

    [Fact]
    public void DimensionFilter_MultipleValues_PermitsAny()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var filter = new AccountingDimensionFilter(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isAllowList: true);
        filter.DimensionValueIds = $"{id1},{id2}";

        filter.IsValuePermitted(id1).ShouldBeTrue();
        filter.IsValuePermitted(id2).ShouldBeTrue();
        filter.IsValuePermitted(Guid.NewGuid()).ShouldBeFalse();
    }

    #endregion

    #region GlDimensionValue

    [Fact]
    public void GlDimensionValue_Create_SetsAllProperties()
    {
        var lineId = Guid.NewGuid();
        var dimId = Guid.NewGuid();
        var valueId = Guid.NewGuid();

        var entry = new GlDimensionValue(Guid.NewGuid(), lineId, dimId, "branch_id", valueId);

        entry.JournalEntryLineId.ShouldBe(lineId);
        entry.AccountingDimensionId.ShouldBe(dimId);
        entry.DimensionFieldName.ShouldBe("branch_id");
        entry.DimensionValueId.ShouldBe(valueId);
    }

    [Fact]
    public void GlDimensionValue_TenantId_DefaultNull()
    {
        var entry = new GlDimensionValue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "cost_center_id", Guid.NewGuid());
        entry.TenantId.ShouldBeNull();
    }

    [Fact]
    public void GlDimensionValue_TenantId_CanBeSet()
    {
        var tenantId = Guid.NewGuid();
        var entry = new GlDimensionValue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "branch_id", Guid.NewGuid(), tenantId);
        entry.TenantId.ShouldBe(tenantId);
    }

    #endregion
}
