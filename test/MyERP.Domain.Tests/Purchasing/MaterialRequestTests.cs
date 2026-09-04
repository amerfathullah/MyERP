using System;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class MaterialRequestTests
{
    [Fact]
    public void Create_ShouldSetDraftStatus()
    {
        var mr = CreateMaterialRequest();

        mr.Status.ShouldBe(Core.DocumentStatus.Draft);
        mr.RequestType.ShouldBe(MaterialRequestType.Purchase);
    }

    [Fact]
    public void AddItem_ShouldAddToCollection()
    {
        var mr = CreateMaterialRequest();

        mr.AddItem(Guid.NewGuid(), "Steel Bar", 10, "Kg");

        mr.Items.Count.ShouldBe(1);
        mr.Items[0].Quantity.ShouldBe(10);
    }

    [Fact]
    public void Submit_WithItems_ShouldSucceed()
    {
        var mr = CreateMaterialRequest();
        mr.AddItem(Guid.NewGuid(), "Steel Bar", 10, "Kg");

        mr.Submit();

        mr.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_ShouldThrow()
    {
        var mr = CreateMaterialRequest();

        Assert.Throws<BusinessException>(() => mr.Submit());
    }

    [Fact]
    public void Cancel_FromSubmitted_ShouldSucceed()
    {
        var mr = CreateMaterialRequest();
        mr.AddItem(Guid.NewGuid(), "Steel Bar", 10, "Kg");
        mr.Submit();

        mr.Cancel();

        mr.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_ShouldThrow()
    {
        var mr = CreateMaterialRequest();

        Assert.Throws<BusinessException>(() => mr.Cancel());
    }

    [Fact]
    public void AddItem_AfterSubmit_ShouldThrow()
    {
        var mr = CreateMaterialRequest();
        mr.AddItem(Guid.NewGuid(), "Steel Bar", 10, "Kg");
        mr.Submit();

        Assert.Throws<BusinessException>(() =>
            mr.AddItem(Guid.NewGuid(), "Bolt M8", 50, "Unit"));
    }

    [Fact]
    public void AddItem_WithSalesOrderLine_ShouldSetProperties()
    {
        var mr = CreateMaterialRequest();
        var soId = Guid.NewGuid();
        var soItemId = Guid.NewGuid();

        mr.AddItem(Guid.NewGuid(), "Steel Bar", 10, "Kg", salesOrderId: soId, salesOrderItemId: soItemId);

        mr.Items.Count.ShouldBe(1);
        mr.Items[0].SalesOrderId.ShouldBe(soId);
        mr.Items[0].SalesOrderItemId.ShouldBe(soItemId);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateWithSalesOrderAsync_ThrowsWhenItemMismatch()
    {
        var companyId = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-SO-001",
            MaterialRequestType.Purchase, DateTime.UtcNow);

        var soId = Guid.NewGuid();
        var actualItemId = Guid.NewGuid();
        var differentItemId = Guid.NewGuid();

        var so = new MyERP.Sales.Entities.SalesOrder(soId, companyId, Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AddItem(actualItemId, "Correct Item", 5, 100, 0, "Unit");
        var soItemId = so.Items[0].Id;

        mr.AddItem(differentItemId, "Wrong Item", 5, "Unit", salesOrderId: soId, salesOrderItemId: soItemId);

        var soRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
        soRepo.FindAsync(soId).Returns(so);

        var manager = new MyERP.Purchasing.DomainServices.MaterialRequestManager(
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MaterialRequest, Guid>>()
        );

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateWithSalesOrderAsync(mr, soRepo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateWithSalesOrderAsync_SucceedsWhenItemMatches()
    {
        var companyId = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-SO-002",
            MaterialRequestType.Purchase, DateTime.UtcNow);

        var soId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var so = new MyERP.Sales.Entities.SalesOrder(soId, companyId, Guid.NewGuid(), "SO-002", DateTime.UtcNow);
        so.AddItem(itemId, "Correct Item", 5, 100, 0, "Unit");
        var soItemId = so.Items[0].Id;

        mr.AddItem(itemId, "Correct Item", 5, "Unit", salesOrderId: soId, salesOrderItemId: soItemId);

        var soRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
        soRepo.FindAsync(soId).Returns(so);

        var manager = new MyERP.Purchasing.DomainServices.MaterialRequestManager(
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MaterialRequest, Guid>>()
        );

        await manager.ValidateWithSalesOrderAsync(mr, soRepo); // Should not throw
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateWithSalesOrderAsync_ThrowsWhenUomMismatch()
    {
        var companyId = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-SO-003",
            MaterialRequestType.Purchase, DateTime.UtcNow);

        var soId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var so = new MyERP.Sales.Entities.SalesOrder(soId, companyId, Guid.NewGuid(), "SO-003", DateTime.UtcNow);
        so.AddItem(itemId, "Correct Item", 5, 100, 0, "Box");
        var soItemId = so.Items[0].Id;

        // MR item with mismatched UOM (Unit vs Box)
        mr.AddItem(itemId, "Correct Item", 5, "Unit", salesOrderId: soId, salesOrderItemId: soItemId);

        var soRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
        soRepo.FindAsync(soId).Returns(so);

        var manager = new MyERP.Purchasing.DomainServices.MaterialRequestManager(
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MaterialRequest, Guid>>()
        );

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateWithSalesOrderAsync(mr, soRepo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateWithSalesOrderAsync_ThrowsWhenConversionFactorMismatch()
    {
        var companyId = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-SO-004",
            MaterialRequestType.Purchase, DateTime.UtcNow);

        var soId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var so = new MyERP.Sales.Entities.SalesOrder(soId, companyId, Guid.NewGuid(), "SO-004", DateTime.UtcNow);
        so.AddItem(itemId, "Correct Item", 5, 100, 0, "Box");
        so.Items[0].ConversionFactor = 12m;
        var soItemId = so.Items[0].Id;

        // MR item with mismatched conversion factor (6 vs 12)
        mr.AddItem(itemId, "Correct Item", 5, "Box", salesOrderId: soId, salesOrderItemId: soItemId, conversionFactor: 6m);

        var soRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
        soRepo.FindAsync(soId).Returns(so);

        var manager = new MyERP.Purchasing.DomainServices.MaterialRequestManager(
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MaterialRequest, Guid>>()
        );

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateWithSalesOrderAsync(mr, soRepo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    private static MaterialRequest CreateMaterialRequest() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "MR-0001",
            MaterialRequestType.Purchase, DateTime.UtcNow, Guid.NewGuid());
}
