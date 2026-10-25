using System;
using System.Collections.Generic;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity sync (ERPNext PRs #62244, #61667, #62249, #62218, #61468).
/// </summary>
public class UpstreamParityBatch20261025Tests
{
    [Fact]
    public void Dunning_AddOverduePayment_ThrowsWhenNonPositive()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 1);
        
        var ex = Assert.Throws<BusinessException>(() =>
            dunning.AddOverduePayment(Guid.NewGuid(), 0m, DateTime.UtcNow.AddDays(-10), 10));
        
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void PaymentOrder_AddReference_EnforcesMatchingReferenceType()
    {
        var companyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();

        // 1. PaymentRequest order accepts PaymentRequest, rejects PaymentEntry
        var prOrder = new PaymentOrder(Guid.NewGuid(), companyId, PaymentOrderType.PaymentRequest,
            DateTime.UtcNow, bankAccountId);

        var ref1 = prOrder.AddReference("PaymentRequest", Guid.NewGuid(), 100m, Guid.NewGuid(), "Bank Transfer", bankAccountId);
        Assert.NotNull(ref1);
        Assert.Single(prOrder.References);

        var ex1 = Assert.Throws<BusinessException>(() =>
            prOrder.AddReference("PaymentEntry", Guid.NewGuid(), 50m, Guid.NewGuid(), "Bank Transfer", bankAccountId));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex1.Code);

        // 2. PaymentEntry order accepts PaymentEntry, rejects PaymentRequest
        var peOrder = new PaymentOrder(Guid.NewGuid(), companyId, PaymentOrderType.PaymentEntry,
            DateTime.UtcNow, bankAccountId);

        var ref2 = peOrder.AddReference("PaymentEntry", Guid.NewGuid(), 200m, Guid.NewGuid(), "Bank Transfer", bankAccountId);
        Assert.NotNull(ref2);
        Assert.Single(peOrder.References);

        var ex2 = Assert.Throws<BusinessException>(() =>
            peOrder.AddReference("PaymentRequest", Guid.NewGuid(), 75m, Guid.NewGuid(), "Bank Transfer", bankAccountId));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex2.Code);
    }

    [Fact]
    public void MaterialRequest_ClearSourceWarehouseIfNotTransfer_ClearsWarehouseWhenNotTransfer()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-0001",
            MaterialRequestType.Purchase, DateTime.UtcNow)
        {
            SourceWarehouseId = Guid.NewGuid(),
            TargetWarehouseId = Guid.NewGuid()
        };

        mr.ClearSourceWarehouseIfNotTransfer();
        Assert.Null(mr.SourceWarehouseId);
        Assert.NotNull(mr.TargetWarehouseId);

        // When MaterialTransfer, keeps SourceWarehouseId
        var mrTransfer = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-0002",
            MaterialRequestType.MaterialTransfer, DateTime.UtcNow)
        {
            SourceWarehouseId = Guid.NewGuid(),
            TargetWarehouseId = Guid.NewGuid()
        };

        var sourceWh = mrTransfer.SourceWarehouseId;
        mrTransfer.ClearSourceWarehouseIfNotTransfer();
        Assert.Equal(sourceWh, mrTransfer.SourceWarehouseId);
    }
}
