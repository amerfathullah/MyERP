using System;
using Xunit;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Maintenance;
using MyERP.Maintenance.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for WarehouseAccount, WarehouseType, SalesPartner, WarrantyClaim entities,
/// and WarehouseAccountService resolution chain concept.
/// </summary>
public class WarehouseAccountPartnerClaimTests
{
    // --- Warehouse.WarehouseType ---

    [Fact]
    public void Warehouse_WarehouseType_Defaults_Standard()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Main Store");
        Assert.Equal(WarehouseType.Standard, wh.WarehouseType);
        Assert.False(wh.IsTransitWarehouse);
    }

    [Fact]
    public void Warehouse_Transit_IsTransitWarehouse_True()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Goods In Transit");
        wh.WarehouseType = WarehouseType.Transit;
        Assert.True(wh.IsTransitWarehouse);
    }

    [Fact]
    public void Warehouse_DefaultAccountId_Defaults_Null()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Store A");
        Assert.Null(wh.DefaultAccountId);
    }

    [Fact]
    public void Warehouse_DefaultAccountId_CanBeSet()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Store A");
        var accountId = Guid.NewGuid();
        wh.DefaultAccountId = accountId;
        Assert.Equal(accountId, wh.DefaultAccountId);
    }

    [Fact]
    public void Warehouse_AllTypes_Exist()
    {
        Assert.Equal(0, (int)WarehouseType.Standard);
        Assert.Equal(1, (int)WarehouseType.Transit);
        Assert.Equal(2, (int)WarehouseType.Rejected);
        Assert.Equal(3, (int)WarehouseType.SampleRetention);
    }

    // --- WarehouseAccount ---

    [Fact]
    public void WarehouseAccount_Create_SetsProperties()
    {
        var whId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var wa = new WarehouseAccount(Guid.NewGuid(), whId, companyId, accountId);

        Assert.Equal(whId, wa.WarehouseId);
        Assert.Equal(companyId, wa.CompanyId);
        Assert.Equal(accountId, wa.AccountId);
        Assert.Null(wa.StockReceivedButNotBilledAccountId);
        Assert.Null(wa.StockDeliveredButNotBilledAccountId);
        Assert.Null(wa.StockAdjustmentAccountId);
    }

    [Fact]
    public void WarehouseAccount_OptionalAccounts_CanBeSet()
    {
        var wa = new WarehouseAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var srbnbId = Guid.NewGuid();
        var sdbnbId = Guid.NewGuid();
        var adjId = Guid.NewGuid();

        wa.StockReceivedButNotBilledAccountId = srbnbId;
        wa.StockDeliveredButNotBilledAccountId = sdbnbId;
        wa.StockAdjustmentAccountId = adjId;

        Assert.Equal(srbnbId, wa.StockReceivedButNotBilledAccountId);
        Assert.Equal(sdbnbId, wa.StockDeliveredButNotBilledAccountId);
        Assert.Equal(adjId, wa.StockAdjustmentAccountId);
    }

    // --- SalesPartner ---

    [Fact]
    public void SalesPartner_Create_SetsDefaults()
    {
        var sp = new SalesPartner(Guid.NewGuid(), "ABC Trading", PartnerType.Distributor, 10m);
        Assert.Equal("ABC Trading", sp.Name);
        Assert.Equal(PartnerType.Distributor, sp.PartnerType);
        Assert.Equal(10m, sp.CommissionRate);
        Assert.True(sp.IsEnabled);
        Assert.Null(sp.TerritoryId);
        Assert.Null(sp.Website);
        Assert.Null(sp.ReferralCode);
    }

    [Fact]
    public void SalesPartner_CommissionRate_Valid_Accepted()
    {
        var sp = new SalesPartner(Guid.NewGuid(), "Partner A", PartnerType.Reseller, 15m);
        Assert.Equal(15m, sp.CommissionRate);
    }

    [Fact]
    public void SalesPartner_CommissionRate_Zero_Accepted()
    {
        var sp = new SalesPartner(Guid.NewGuid(), "Partner B", PartnerType.Agent, 0m);
        Assert.Equal(0m, sp.CommissionRate);
    }

    [Fact]
    public void SalesPartner_CommissionRate_100_Accepted()
    {
        var sp = new SalesPartner(Guid.NewGuid(), "Partner C", PartnerType.Broker, 100m);
        Assert.Equal(100m, sp.CommissionRate);
    }

    [Fact]
    public void SalesPartner_CommissionRate_Negative_Throws()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new SalesPartner(Guid.NewGuid(), "Bad Partner", PartnerType.Dealer, -5m));
    }

    [Fact]
    public void SalesPartner_CommissionRate_Over100_Throws()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new SalesPartner(Guid.NewGuid(), "Greedy Partner", PartnerType.Referral, 101m));
    }

    [Fact]
    public void SalesPartner_CalculateCommission_Basic()
    {
        var sp = new SalesPartner(Guid.NewGuid(), "Sales Pro", PartnerType.Reseller, 10m);
        Assert.Equal(1000m, sp.CalculateCommission(10000m));
    }

    [Fact]
    public void SalesPartner_CalculateCommission_ZeroRate()
    {
        var sp = new SalesPartner(Guid.NewGuid(), "Free Partner", PartnerType.Referral, 0m);
        Assert.Equal(0m, sp.CalculateCommission(50000m));
    }

    [Fact]
    public void SalesPartner_Disable_SetsEnabled_False()
    {
        var sp = new SalesPartner(Guid.NewGuid(), "Active Partner", PartnerType.Agent, 5m);
        Assert.True(sp.IsEnabled);
        sp.Disable();
        Assert.False(sp.IsEnabled);
    }

    [Fact]
    public void SalesPartner_Enable_After_Disable()
    {
        var sp = new SalesPartner(Guid.NewGuid(), "Partner X", PartnerType.Distributor, 8m);
        sp.Disable();
        sp.Enable();
        Assert.True(sp.IsEnabled);
    }

    [Fact]
    public void SalesPartner_AllTypes_Exist()
    {
        Assert.Equal(0, (int)PartnerType.Reseller);
        Assert.Equal(1, (int)PartnerType.Distributor);
        Assert.Equal(2, (int)PartnerType.Dealer);
        Assert.Equal(3, (int)PartnerType.Agent);
        Assert.Equal(4, (int)PartnerType.Broker);
        Assert.Equal(5, (int)PartnerType.Referral);
    }

    // --- WarrantyClaim ---

    [Fact]
    public void WarrantyClaim_Create_SetsDefaults()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        Assert.Equal(WarrantyClaimStatus.Open, wc.Status);
        Assert.Null(wc.SerialNoId);
        Assert.Null(wc.SalesInvoiceId);
        Assert.Null(wc.WarrantyExpiryDate);
        Assert.Null(wc.AmcExpiryDate);
        Assert.Null(wc.Resolution);
        Assert.Null(wc.ResolutionDate);
    }

    [Fact]
    public void WarrantyClaim_StartWork_FromOpen()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        wc.StartWork();
        Assert.Equal(WarrantyClaimStatus.WorkInProgress, wc.Status);
    }

    [Fact]
    public void WarrantyClaim_StartWork_FromClosed_Throws()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        wc.Close();
        Assert.Throws<Volo.Abp.BusinessException>(() => wc.StartWork());
    }

    [Fact]
    public void WarrantyClaim_Close_FromOpen()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        wc.Close("Replaced part");
        Assert.Equal(WarrantyClaimStatus.Closed, wc.Status);
        Assert.Equal("Replaced part", wc.Resolution);
        Assert.NotNull(wc.ResolutionDate);
    }

    [Fact]
    public void WarrantyClaim_Close_FromWIP()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        wc.StartWork();
        wc.Close("Fixed");
        Assert.Equal(WarrantyClaimStatus.Closed, wc.Status);
    }

    [Fact]
    public void WarrantyClaim_Cancel()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        wc.Cancel();
        Assert.Equal(WarrantyClaimStatus.Cancelled, wc.Status);
    }

    [Fact]
    public void WarrantyClaim_DoubleCancel_Throws()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        wc.Cancel();
        Assert.Throws<Volo.Abp.BusinessException>(() => wc.Cancel());
    }

    [Fact]
    public void WarrantyClaim_IsUnderWarranty_WithinPeriod()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        wc.WarrantyExpiryDate = DateTime.Today.AddMonths(6);
        Assert.True(wc.IsUnderWarranty());
    }

    [Fact]
    public void WarrantyClaim_IsUnderWarranty_Expired()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        wc.WarrantyExpiryDate = DateTime.Today.AddDays(-1);
        Assert.False(wc.IsUnderWarranty());
    }

    [Fact]
    public void WarrantyClaim_IsUnderWarranty_AMC_Covers()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        wc.WarrantyExpiryDate = DateTime.Today.AddDays(-30); // warranty expired
        wc.AmcExpiryDate = DateTime.Today.AddMonths(3); // AMC still active
        Assert.True(wc.IsUnderWarranty());
    }

    [Fact]
    public void WarrantyClaim_IsUnderWarranty_NoDates()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        Assert.False(wc.IsUnderWarranty());
    }

    [Fact]
    public void WarrantyClaim_AllStatuses_Exist()
    {
        Assert.Equal(0, (int)WarrantyClaimStatus.Open);
        Assert.Equal(1, (int)WarrantyClaimStatus.WorkInProgress);
        Assert.Equal(2, (int)WarrantyClaimStatus.Closed);
        Assert.Equal(3, (int)WarrantyClaimStatus.Cancelled);
    }

    // --- Error Code ---

    [Fact]
    public void ErrorCode_InvalidCommissionRate_Exists()
    {
        Assert.Equal("MyERP:03023", MyERPDomainErrorCodes.InvalidCommissionRate);
    }
}
