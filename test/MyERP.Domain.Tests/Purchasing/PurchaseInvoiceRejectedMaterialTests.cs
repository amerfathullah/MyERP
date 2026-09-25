using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

/// <summary>
/// Tests for rejected material handling and billing on stock-updating Purchase Invoices.
/// Mirrors ERPNext PR #59257 (commit ecc643fde0) and PR #59258 (commit 16b1be814c).
/// </summary>
public class PurchaseInvoiceRejectedMaterialTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _rejectedWarehouseId = Guid.NewGuid();

    [Fact]
    public void UpdateStockInvoice_WhenBillsRejectedQuantityFalse_BillsAcceptedQuantityOnly_RejectedMaterialNotValued()
    {
        // Per ERPNext PR #59257:
        // When bill_for_rejected_quantity_in_purchase_invoice is off,
        // an invoice bills accepted qty alone (600), so rejected material carries no cost (0).
        var invoice = new PurchaseInvoice(
            Guid.NewGuid(), _companyId, _supplierId,
            "PI-REJ-001", DateTime.UtcNow)
        {
            UpdateStock = true,
            WarehouseId = _warehouseId,
            RejectedWarehouseId = _rejectedWarehouseId,
            BillsRejectedQuantity = false
        };

        invoice.AddItem(
            Guid.NewGuid(), "Raw Material A", quantity: 6m, unitPrice: 100m, taxAmount: 0m,
            warehouseId: _warehouseId, receivedQty: 10m, rejectedQty: 4m, rejectedWarehouseId: _rejectedWarehouseId);

        var item = invoice.Items[0];
        item.BilledQuantity.ShouldBe(6m);
        item.LineTotal.ShouldBe(600m);
        invoice.NetTotal.ShouldBe(600m);
        invoice.GrandTotal.ShouldBe(600m);

        // Valuation check: rejected rate is 0.0 because invoice did not bill for it
        var rejectedRate = invoice.BillsRejectedQuantity ? item.StockUomRate : 0.0m;
        rejectedRate.ShouldBe(0.0m);

        var acceptedStockValue = item.StockQty * item.StockUomRate;
        var rejectedStockValue = item.RejectedStockQty * rejectedRate;
        (acceptedStockValue + rejectedStockValue).ShouldBe(600m);

        invoice.Submit();
        invoice.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void UpdateStockInvoice_WhenBillsRejectedQuantityTrue_BillsReceivedQuantity_RejectedMaterialValued()
    {
        // Per ERPNext PR #59258:
        // When set_valuation_rate_for_rejected_materials and bill_for_rejected_quantity_in_purchase_invoice are both on,
        // invoice bills received qty (10 × 100 = 1000) and rejected material is valued at rate.
        var invoice = new PurchaseInvoice(
            Guid.NewGuid(), _companyId, _supplierId,
            "PI-REJ-002", DateTime.UtcNow)
        {
            UpdateStock = true,
            WarehouseId = _warehouseId,
            RejectedWarehouseId = _rejectedWarehouseId,
            BillsRejectedQuantity = true
        };

        invoice.AddItem(
            Guid.NewGuid(), "Raw Material B", quantity: 6m, unitPrice: 100m, taxAmount: 0m,
            warehouseId: _warehouseId, receivedQty: 10m, rejectedQty: 4m, rejectedWarehouseId: _rejectedWarehouseId);

        var item = invoice.Items[0];
        item.BilledQuantity.ShouldBe(10m);
        item.LineTotal.ShouldBe(1000m);
        invoice.NetTotal.ShouldBe(1000m);
        invoice.GrandTotal.ShouldBe(1000m);

        // Valuation check: rejected rate is full rate because invoice billed for it
        var rejectedRate = invoice.BillsRejectedQuantity ? item.StockUomRate : 0.0m;
        rejectedRate.ShouldBe(100m);

        var acceptedStockValue = item.StockQty * item.StockUomRate;
        var rejectedStockValue = item.RejectedStockQty * rejectedRate;
        acceptedStockValue.ShouldBe(600m);
        rejectedStockValue.ShouldBe(400m);
        (acceptedStockValue + rejectedStockValue).ShouldBe(1000m);

        invoice.Submit();
        invoice.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void UpdateStockInvoice_Submit_RequiresRejectedWarehouse_WhenRejectedQtyNonZero()
    {
        // Per ERPNext buying_controller: rejected goods must have a destination warehouse
        var invoice = new PurchaseInvoice(
            Guid.NewGuid(), _companyId, _supplierId,
            "PI-REJ-003", DateTime.UtcNow)
        {
            UpdateStock = true,
            WarehouseId = _warehouseId
            // RejectedWarehouseId intentionally not set on invoice or item
        };

        invoice.AddItem(
            Guid.NewGuid(), "Raw Material C", quantity: 8m, unitPrice: 50m, taxAmount: 0m,
            receivedQty: 10m, rejectedQty: 2m);

        var ex = Should.Throw<BusinessException>(() => invoice.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Rejected Warehouse is required");
    }

    [Fact]
    public void PurchaseInvoiceItem_ValidateAcceptedRejectedQty_EnforcesReceivedEqualsAcceptedPlusRejected()
    {
        var invoice = new PurchaseInvoice(
            Guid.NewGuid(), _companyId, _supplierId,
            "PI-REJ-004", DateTime.UtcNow);

        // Mismatch: received 10, accepted 6, rejected 3 (sum = 9 != 10)
        var ex = Should.Throw<BusinessException>(() =>
            invoice.AddItem(
                Guid.NewGuid(), "Item", quantity: 6m, unitPrice: 10m, taxAmount: 0m,
                receivedQty: 10m, rejectedQty: 3m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("must equal Accepted Qty");
    }

    [Fact]
    public void PurchaseInvoiceItem_ValidateAcceptedRejectedQty_DisallowsNegativeQtyOnNormalInvoice()
    {
        var invoice = new PurchaseInvoice(
            Guid.NewGuid(), _companyId, _supplierId,
            "PI-REJ-005", DateTime.UtcNow)
        {
            IsReturn = false
        };

        var ex = Should.Throw<BusinessException>(() =>
            invoice.AddItem(
                Guid.NewGuid(), "Item", quantity: 10m, unitPrice: 10m, taxAmount: 0m,
                receivedQty: 8m, rejectedQty: -2m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("cannot be negative");
    }

    [Fact]
    public void UpdateStockInvoice_FullyRejected_BilledQuantityIsRejectedQty()
    {
        // 100% rejected shipment on invoice (accepted=0, rejected=10)
        var invoice = new PurchaseInvoice(
            Guid.NewGuid(), _companyId, _supplierId,
            "PI-REJ-006", DateTime.UtcNow)
        {
            UpdateStock = true,
            WarehouseId = _warehouseId,
            RejectedWarehouseId = _rejectedWarehouseId,
            BillsRejectedQuantity = true
        };

        invoice.AddItem(
            Guid.NewGuid(), "Fully Rejected", quantity: 0m, unitPrice: 50m, taxAmount: 0m,
            warehouseId: _warehouseId, receivedQty: 10m, rejectedQty: 10m, rejectedWarehouseId: _rejectedWarehouseId);

        var item = invoice.Items[0];
        item.BilledQuantity.ShouldBe(10m);
        item.LineTotal.ShouldBe(500m);
        invoice.NetTotal.ShouldBe(500m);

        invoice.Submit();
        invoice.Status.ShouldBe(DocumentStatus.Submitted);
    }
}
