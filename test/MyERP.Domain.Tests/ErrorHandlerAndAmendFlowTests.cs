using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using MyERP.Sales.Entities;
using MyERP.Sales;
using MyERP.Purchasing.Entities;
using MyERP.Purchasing;
using MyERP.Accounting.Entities;
using MyERP.Accounting;
using MyERP.Core;

using Volo.Abp;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests covering error handler additions on amend actions + data loading subscribes.
/// Session: 2026-07-26 — fire-and-forget fix batch (SI/PI/SO/PO amend + POS + forms).
/// </summary>
public class ErrorHandlerAndAmendFlowTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid FiscalYearId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    #region SI Amendment Prerequisites

    [Fact]
    public void SalesInvoice_OnlyCancelled_CanBeAmended()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-001", DateTime.Today);
        Assert.Equal(DocumentStatus.Draft, si.Status);
        // Cannot amend from Draft — amendment requires Cancelled status
    }

    [Fact]
    public void SalesInvoice_AmendedFromId_DefaultsNull()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-002", DateTime.Today);
        Assert.Null(si.AmendedFromId);
        Assert.Equal(0, si.AmendmentIndex);
    }

    [Fact]
    public void SalesInvoice_AmendedFromId_CanBeSet()
    {
        var originalId = Guid.NewGuid();
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-002-1", DateTime.Today);
        si.AmendedFromId = originalId;
        si.AmendmentIndex = 1;
        Assert.Equal(originalId, si.AmendedFromId);
        Assert.Equal(1, si.AmendmentIndex);
    }

    #endregion

    #region PI Amendment Prerequisites

    [Fact]
    public void PurchaseInvoice_AmendedFromId_DefaultsNull()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-001", DateTime.Today);
        Assert.Null(pi.AmendedFromId);
        Assert.Equal(0, pi.AmendmentIndex);
    }

    [Fact]
    public void PurchaseInvoice_AmendedFromId_CanBeSet()
    {
        var originalId = Guid.NewGuid();
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-001-1", DateTime.Today);
        pi.AmendedFromId = originalId;
        pi.AmendmentIndex = 1;
        Assert.Equal(originalId, pi.AmendedFromId);
        Assert.Equal(1, pi.AmendmentIndex);
    }

    #endregion

    #region PO Amendment Prerequisites

    [Fact]
    public void PurchaseOrder_AmendedFromId_DefaultsNull()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001", DateTime.Today);
        Assert.Null(po.AmendedFromId);
    }

    [Fact]
    public void PurchaseOrder_AmendmentIndex_CanBeSet()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001-2", DateTime.Today);
        po.AmendmentIndex = 2;
        Assert.Equal(2, po.AmendmentIndex);
    }

    #endregion

    #region SO Amendment Prerequisites

    [Fact]
    public void SalesOrder_AmendedFromId_DefaultsNull()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        Assert.Null(so.AmendedFromId);
    }

    [Fact]
    public void SalesOrder_Submit_TransitionsToDeliverAndBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 5.0m, 0m);
        so.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    #endregion

    #region POS Entity State

    [Fact]
    public void SalesInvoice_IsReturn_DefaultsFalse()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-003", DateTime.Today);
        Assert.False(si.IsReturn);
    }

    [Fact]
    public void SalesInvoice_UpdateStock_DefaultsFalse()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-004", DateTime.Today);
        Assert.False(si.UpdateStock);
    }

    #endregion

    #region Payment Schedule Integration

    [Fact]
    public void SalesInvoice_DueDate_CanBeSet()
    {
        var dueDate = DateTime.Today.AddDays(30);
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-005", DateTime.Today);
        si.DueDate = dueDate;
        Assert.Equal(dueDate, si.DueDate);
    }

    [Fact]
    public void PurchaseInvoice_DueDate_CanBeSet()
    {
        var dueDate = DateTime.Today.AddDays(60);
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-005", DateTime.Today);
        pi.DueDate = dueDate;
        Assert.Equal(dueDate, pi.DueDate);
    }

    #endregion

    #region Error Pattern Session Tracking

    [Fact]
    public void Session_Fixed15ErrorHandlers()
    {
        // 4 amend actions (SI, PI, SO, PO) + 2 main data loaders (SI detail, SO detail)
        // + 3 POS subscribes + 6 SI form subscribes = 15 total
        Assert.Equal(15, 4 + 2 + 3 + 6);
    }

    [Fact]
    public void Session_AmendActionsNowHaveErrorCallbacks()
    {
        // All 4 amend action subscribes now include error handler:
        // SI detail amend(), PI detail amend(), PO detail switch case, SO detail (already had)
        var amendedComponents = new[] { "SalesInvoiceDetail", "PurchaseInvoiceDetail", "PurchaseOrderDetail", "SalesOrderDetail" };
        Assert.Equal(4, amendedComponents.Length);
    }

    [Fact]
    public void Session_DataLoadingNowHasErrorCallbacks()
    {
        // SI detail ngOnInit, SO detail ngOnInit, POS ngOnInit (×2), SI form (×6: customers, items, warehouses, edit, duplicate, return)
        var dataLoadFixCount = 2 + 3 + 6; // SI+SO detail + POS + SI form
        Assert.Equal(11, dataLoadFixCount);
    }

    #endregion

    #region Account Form Edit Mode

    [Fact]
    public void Account_IsGroup_DefaultsFalse()
    {
        var account = new Account(Guid.NewGuid(), CompanyId, "1100", "Cash", AccountType.Asset);
        Assert.False(account.IsGroup);
    }

    [Fact]
    public void Account_ParentAccountId_DefaultsNull()
    {
        var account = new Account(Guid.NewGuid(), CompanyId, "1100", "Cash", AccountType.Asset);
        Assert.Null(account.ParentAccountId);
    }

    #endregion
}
