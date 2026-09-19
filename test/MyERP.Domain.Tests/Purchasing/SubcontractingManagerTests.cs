using MyERP.Purchasing;
using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.SubcontractingAndWiringTests;

public class SubcontractingAndWiringTests
{
    // ========== SubcontractingManager Tests ==========

    private static SubcontractingOrder CreateSco()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-001",
            DateTime.UtcNow, Guid.NewGuid());
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id,
            Guid.NewGuid(), "FG Widget", 100m, 50m));
        return sco;
    }

    [Fact]
    public void CalculateRmConsumption_ProportionalRatio()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 50m);

        result.Length.ShouldBe(1);
        result[0].ConsumedQty.ShouldBe(100m); // 200 × (50/100) = 100
    }

    [Fact]
    public void CalculateRmConsumption_FullReceipt()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 100m);

        result[0].ConsumedQty.ShouldBe(200m); // Full consumption
    }

    [Fact]
    public void CalculateRmConsumption_MultipleRm()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Paint", 50m));

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 25m);

        result.Length.ShouldBe(2);
        result[0].ConsumedQty.ShouldBe(50m);  // 200 × (25/100)
        result[1].ConsumedQty.ShouldBe(12.5m); // 50 × (25/100)
    }

    [Fact]
    public void CalculateRmConsumption_ZeroFgQty_ReturnsEmpty()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-001",
            DateTime.UtcNow, Guid.NewGuid());
        // No FG items → total qty is 0

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 10m);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SubcontractingOrder_PerReceived_DefaultsZero()
    {
        var sco = CreateSco();
        sco.PerReceived.ShouldBe(0);
    }

    [Fact]
    public void SubcontractingOrder_PartialReceipt_TracksQty()
    {
        var sco = CreateSco();
        sco.Submit();
        sco.Items.First().ReceivedQty = 40;

        var pending = sco.Items.First().Qty - sco.Items.First().ReceivedQty;
        pending.ShouldBe(60m);
    }

    // ========== SubcontractingRmConsumption DTO ==========

    [Fact]
    public void SubcontractingRmConsumption_StoresValues()
    {
        var c = new SubcontractingRmConsumption
        {
            ItemId = Guid.NewGuid(),
            RequiredQty = 200m,
            ConsumedQty = 100m,
            WarehouseId = Guid.NewGuid()
        };
        c.ConsumedQty.ShouldBe(100m);
        c.WarehouseId.ShouldNotBeNull();
    }

    // ========== SCO Status Transitions ==========

    [Fact]
    public void SubcontractingOrder_Submit_FromDraft()
    {
        var sco = CreateSco();
        sco.Submit();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Open);
    }

    [Fact]
    public void SubcontractingOrder_Close_FromOpen()
    {
        var sco = CreateSco();
        sco.Submit();
        sco.MarkPartiallyReceived();
        sco.Close();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Closed);
    }

    [Fact]
    public void SubcontractingOrder_Cancel_FromDraft()
    {
        var sco = CreateSco();
        sco.Cancel();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Cancelled);
    }

    [Fact]
    public void SubcontractingOrder_DoubleCancel_Throws()
    {
        var sco = CreateSco();
        sco.Cancel();
        Should.Throw<BusinessException>(() => sco.Cancel());
    }

    [Fact]
    public async Task ValidateReceiptAgainstOrderAsync_DifferentHeaderProject_Throws()
    {
        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        var scrRepo = Substitute.For<IRepository<SubcontractingReceipt, Guid>>();
        var mgr = new SubcontractingManager(scoRepo, scrRepo);

        var sco = CreateSco();
        sco.ProjectId = Guid.NewGuid();
        scoRepo.GetAsync(sco.Id).Returns(Task.FromResult(sco));

        var receipt = new SubcontractingReceipt(
            Guid.NewGuid(), sco.CompanyId, "SCR-001", DateTime.UtcNow, sco.SupplierId, sco.Id)
        {
            ProjectId = Guid.NewGuid() // Different project
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => mgr.ValidateReceiptAgainstOrderAsync(receipt));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Subcontracting Receipt Project cannot differ from Subcontracting Order Project");
    }

    [Fact]
    public async Task ValidateReceiptAgainstOrderAsync_DifferentItemProject_Throws()
    {
        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        var scrRepo = Substitute.For<IRepository<SubcontractingReceipt, Guid>>();
        var mgr = new SubcontractingManager(scoRepo, scrRepo);

        var projectId = Guid.NewGuid();
        var sco = CreateSco();
        sco.ProjectId = projectId;
        sco.Items.First().ProjectId = projectId;
        scoRepo.GetAsync(sco.Id).Returns(Task.FromResult(sco));

        var receipt = new SubcontractingReceipt(
            Guid.NewGuid(), sco.CompanyId, "SCR-001", DateTime.UtcNow, sco.SupplierId, sco.Id)
        {
            ProjectId = projectId
        };
        receipt.AddItem(new SubcontractingReceiptItem(
            Guid.NewGuid(), receipt.Id, sco.Items.First().ItemId, "FG Widget", 10m, 50m, Guid.NewGuid())); // Different item project

        var ex = await Should.ThrowAsync<BusinessException>(() => mgr.ValidateReceiptAgainstOrderAsync(receipt));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Subcontracting Receipt Item Project cannot differ from Subcontracting Order Item Project");
    }

    [Fact]
    public async Task ValidateReceiptAgainstOrderAsync_MatchingProjects_Passes()
    {
        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        var scrRepo = Substitute.For<IRepository<SubcontractingReceipt, Guid>>();
        var mgr = new SubcontractingManager(scoRepo, scrRepo);

        var projectId = Guid.NewGuid();
        var sco = CreateSco();
        sco.ProjectId = projectId;
        sco.Items.First().ProjectId = projectId;
        scoRepo.GetAsync(sco.Id).Returns(Task.FromResult(sco));

        var receipt = new SubcontractingReceipt(
            Guid.NewGuid(), sco.CompanyId, "SCR-001", DateTime.UtcNow, sco.SupplierId, sco.Id)
        {
            ProjectId = projectId
        };
        receipt.AddItem(new SubcontractingReceiptItem(
            Guid.NewGuid(), receipt.Id, sco.Items.First().ItemId, "FG Widget", 10m, 50m, projectId));

        await mgr.ValidateReceiptAgainstOrderAsync(receipt);
    }
}
