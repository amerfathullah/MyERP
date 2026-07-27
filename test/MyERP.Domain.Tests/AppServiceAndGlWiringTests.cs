using System;
using Xunit;
using MyERP.Accounting;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Maintenance;
using MyERP.Maintenance.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Permissions;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for SalesPartner AppService, WarrantyClaim AppService, WarehouseAccount AppService,
/// WarehouseStock account source, and new permissions.
/// </summary>
public class AppServiceAndGlWiringTests
{
    // --- SalesPartner AppService DTO Tests ---

    [Fact]
    public void SalesPartner_CreateDto_Properties()
    {
        var dto = new CreateSalesPartnerDto
        {
            Name = "Tech Partner",
            PartnerType = 1, // Distributor
            CommissionRate = 12.5m,
            Website = "https://partner.com",
            ReferralCode = "REF123"
        };

        Assert.Equal("Tech Partner", dto.Name);
        Assert.Equal(1, dto.PartnerType);
        Assert.Equal(12.5m, dto.CommissionRate);
        Assert.Equal("https://partner.com", dto.Website);
        Assert.Equal("REF123", dto.ReferralCode);
    }

    [Fact]
    public void SalesPartner_Dto_Maps_All_Fields()
    {
        var dto = new SalesPartnerDto
        {
            Id = Guid.NewGuid(),
            Name = "ABC Corp",
            PartnerType = 2,
            CommissionRate = 5.0m,
            IsEnabled = true,
            Website = "https://abc.com",
            ReferralCode = "ABC"
        };

        Assert.Equal("ABC Corp", dto.Name);
        Assert.True(dto.IsEnabled);
        Assert.Equal(5.0m, dto.CommissionRate);
    }

    [Fact]
    public void SalesPartner_Toggle_Changes_IsEnabled()
    {
        var entity = new SalesPartner(Guid.NewGuid(), "Partner A", PartnerType.Agent, 10m);
        Assert.True(entity.IsEnabled);

        entity.Disable();
        Assert.False(entity.IsEnabled);

        entity.Enable();
        Assert.True(entity.IsEnabled);
    }

    // --- WarrantyClaim AppService DTO Tests ---

    [Fact]
    public void WarrantyClaim_CreateDto_Properties()
    {
        var dto = new CreateWarrantyClaimDto
        {
            CompanyId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            ComplaintDate = DateTime.UtcNow,
            Complaint = "Product not working"
        };

        Assert.NotEqual(Guid.Empty, dto.CompanyId);
        Assert.NotEqual(Guid.Empty, dto.CustomerId);
        Assert.Equal("Product not working", dto.Complaint);
    }

    [Fact]
    public void WarrantyClaim_Dto_Includes_IsUnderWarranty()
    {
        var dto = new WarrantyClaimDto
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "WC-001",
            Status = 0,
            IsUnderWarranty = true
        };

        Assert.True(dto.IsUnderWarranty);
        Assert.Equal(0, dto.Status);
    }

    [Fact]
    public void WarrantyClaim_Lifecycle_Open_To_Close()
    {
        var claim = new WarrantyClaim(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.Equal(WarrantyClaimStatus.Open, claim.Status);

        claim.StartWork();
        Assert.Equal(WarrantyClaimStatus.WorkInProgress, claim.Status);

        claim.Close("Replaced part");
        Assert.Equal(WarrantyClaimStatus.Closed, claim.Status);
        Assert.Equal("Replaced part", claim.Resolution);
        Assert.NotNull(claim.ResolutionDate);
    }

    [Fact]
    public void WarrantyClaim_Cancel_From_Open()
    {
        var claim = new WarrantyClaim(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);

        claim.Cancel();
        Assert.Equal(WarrantyClaimStatus.Cancelled, claim.Status);
    }

    [Fact]
    public void WarrantyClaim_Filter_By_Status()
    {
        var filter = new GetWarrantyClaimListDto
        {
            CompanyId = Guid.NewGuid(),
            Status = 1,
            Filter = "broken"
        };

        Assert.Equal(1, filter.Status);
        Assert.Equal("broken", filter.Filter);
    }

    // --- WarehouseAccount AppService DTO Tests ---

    [Fact]
    public void WarehouseAccount_CreateDto_Properties()
    {
        var dto = new CreateWarehouseAccountDto
        {
            WarehouseId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            StockReceivedButNotBilledAccountId = Guid.NewGuid()
        };

        Assert.NotEqual(Guid.Empty, dto.WarehouseId);
        Assert.NotEqual(Guid.Empty, dto.AccountId);
        Assert.NotNull(dto.StockReceivedButNotBilledAccountId);
    }

    [Fact]
    public void WarehouseAccount_Dto_Has_Name_Fields()
    {
        var dto = new WarehouseAccountDto
        {
            Id = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            WarehouseName = "Main Store",
            CompanyId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            AccountName = "Stock In Hand"
        };

        Assert.Equal("Main Store", dto.WarehouseName);
        Assert.Equal("Stock In Hand", dto.AccountName);
    }

    // --- WarehouseStock Account Source ---

    [Fact]
    public void AccountSource_WarehouseStock_Enum_Value()
    {
        Assert.Equal(6, (int)AccountSource.WarehouseStock);
    }

    [Fact]
    public void AccountSource_Has_All_7_Values()
    {
        var values = Enum.GetValues<AccountSource>();
        Assert.Equal(7, values.Length);
        Assert.Contains(AccountSource.WarehouseStock, values);
    }

    // --- Permission Constants ---

    [Fact]
    public void SalesPartners_Permissions_Defined()
    {
        Assert.Equal("MyERP.SalesPartners", MyERPPermissions.SalesPartners.Default);
        Assert.Equal("MyERP.SalesPartners.Create", MyERPPermissions.SalesPartners.Create);
        Assert.Equal("MyERP.SalesPartners.Edit", MyERPPermissions.SalesPartners.Edit);
        Assert.Equal("MyERP.SalesPartners.Delete", MyERPPermissions.SalesPartners.Delete);
    }

    [Fact]
    public void WarrantyClaims_Permissions_Defined()
    {
        Assert.Equal("MyERP.WarrantyClaims", MyERPPermissions.WarrantyClaims.Default);
        Assert.Equal("MyERP.WarrantyClaims.Create", MyERPPermissions.WarrantyClaims.Create);
        Assert.Equal("MyERP.WarrantyClaims.Edit", MyERPPermissions.WarrantyClaims.Edit);
        Assert.Equal("MyERP.WarrantyClaims.Delete", MyERPPermissions.WarrantyClaims.Delete);
    }

    [Fact]
    public void WarehouseAccounts_Permissions_Defined()
    {
        Assert.Equal("MyERP.WarehouseAccounts", MyERPPermissions.WarehouseAccounts.Default);
        Assert.Equal("MyERP.WarehouseAccounts.Create", MyERPPermissions.WarehouseAccounts.Create);
    }

    // --- WarehouseAccount Entity Integration ---

    [Fact]
    public void WarehouseAccount_Optional_Accounts_Nullable()
    {
        var wa = new WarehouseAccount(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(wa.StockReceivedButNotBilledAccountId);
        Assert.Null(wa.StockDeliveredButNotBilledAccountId);
        Assert.Null(wa.StockAdjustmentAccountId);
    }

    [Fact]
    public void WarehouseAccount_Optional_Accounts_Can_Set()
    {
        var wa = new WarehouseAccount(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var srbnb = Guid.NewGuid();
        var sdbnb = Guid.NewGuid();
        var adj = Guid.NewGuid();

        wa.StockReceivedButNotBilledAccountId = srbnb;
        wa.StockDeliveredButNotBilledAccountId = sdbnb;
        wa.StockAdjustmentAccountId = adj;

        Assert.Equal(srbnb, wa.StockReceivedButNotBilledAccountId);
        Assert.Equal(sdbnb, wa.StockDeliveredButNotBilledAccountId);
        Assert.Equal(adj, wa.StockAdjustmentAccountId);
    }

    // --- SalesPartner Commission Calculation ---

    [Fact]
    public void SalesPartner_Commission_Calculation()
    {
        var partner = new SalesPartner(Guid.NewGuid(), "Partner", PartnerType.Distributor, 15m);
        Assert.Equal(150m, partner.CalculateCommission(1000m));
    }

    [Fact]
    public void SalesPartner_Zero_Commission()
    {
        var partner = new SalesPartner(Guid.NewGuid(), "Free Partner", PartnerType.Referral, 0m);
        Assert.Equal(0m, partner.CalculateCommission(5000m));
    }

    // --- WarrantyClaim Under Warranty Logic ---

    [Fact]
    public void WarrantyClaim_Under_Warranty_When_Not_Expired()
    {
        var claim = new WarrantyClaim(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);
        claim.WarrantyExpiryDate = DateTime.UtcNow.AddMonths(6);

        Assert.True(claim.IsUnderWarranty());
    }

    [Fact]
    public void WarrantyClaim_Not_Under_Warranty_When_Expired()
    {
        var claim = new WarrantyClaim(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);
        claim.WarrantyExpiryDate = DateTime.UtcNow.AddDays(-1);

        Assert.False(claim.IsUnderWarranty());
    }

    [Fact]
    public void WarrantyClaim_AMC_Covers_When_Warranty_Expired()
    {
        var claim = new WarrantyClaim(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);
        claim.WarrantyExpiryDate = DateTime.UtcNow.AddDays(-30);
        claim.AmcExpiryDate = DateTime.UtcNow.AddMonths(6);

        Assert.True(claim.IsUnderWarranty());
    }

    // --- SalesPartner PartnerType enum ---

    [Fact]
    public void PartnerType_All_Values_Exist()
    {
        Assert.Equal(0, (int)PartnerType.Reseller);
        Assert.Equal(1, (int)PartnerType.Distributor);
        Assert.Equal(2, (int)PartnerType.Dealer);
        Assert.Equal(3, (int)PartnerType.Agent);
        Assert.Equal(4, (int)PartnerType.Broker);
        Assert.Equal(5, (int)PartnerType.Referral);
    }

    // --- WarrantyClaimStatus enum ---

    [Fact]
    public void WarrantyClaimStatus_All_Values()
    {
        Assert.Equal(0, (int)WarrantyClaimStatus.Open);
        Assert.Equal(1, (int)WarrantyClaimStatus.WorkInProgress);
        Assert.Equal(2, (int)WarrantyClaimStatus.Closed);
        Assert.Equal(3, (int)WarrantyClaimStatus.Cancelled);
    }
}
