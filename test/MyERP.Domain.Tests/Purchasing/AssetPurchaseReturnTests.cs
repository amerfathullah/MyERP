using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Unit tests for partial returns of asset purchases per ERPNext PR #63606 / commit bb1ef484dc &amp; de0b2088b8.
/// - Allows partial returns as long as active (draft/submitted) assets &lt;= kept quantity.
/// - Blocks return when active assets &gt; kept quantity.
/// - Allows return once the excess submitted asset is cancelled.
/// - Blocks return when an unlinked submitted asset exists.
/// </summary>
public class AssetPurchaseReturnTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _macbookItemId = Guid.NewGuid();

    private Asset CreateAsset(string number, string name, Guid? itemId, Guid? prId = null, Guid? piId = null)
    {
        return new Asset(Guid.NewGuid(), _companyId, number, name, DateTime.UtcNow.Date, 1000m)
        {
            ItemId = itemId,
            PurchaseReceiptId = prId,
            PurchaseInvoiceId = piId,
            AssetQuantity = 1
        };
    }

    [Fact]
    public async Task ValidateAssetReturnAsync_PurchaseReceipt_PartialReturnWithinRemainingQty_Succeeds()
    {
        var originalPr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-ASSET-001", DateTime.UtcNow.Date);
        originalPr.AddItem(_macbookItemId, "Macbook Pro", 3m, 1000m, 0m);

        var returnPr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-RET-001", DateTime.UtcNow.Date)
        {
            IsReturn = true,
            ReturnAgainstId = originalPr.Id
        };
        returnPr.AddItem(_macbookItemId, "Macbook Pro", -1m, 1000m, 0m);

        var asset1 = CreateAsset("AST-001", "Macbook 1", _macbookItemId, prId: originalPr.Id);
        asset1.Submit();

        var asset2 = CreateAsset("AST-002", "Macbook 2", _macbookItemId, prId: originalPr.Id); // Draft

        var asset3 = CreateAsset("AST-003", "Macbook 3", _macbookItemId, prId: originalPr.Id);
        asset3.Submit();
        asset3.Cancel();

        // Active = asset1 (Submitted) + asset2 (Draft) = 2 <= kept qty (3 - 1 = 2)
        var assets = new List<Asset> { asset1, asset2, asset3 };

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.GetQueryableAsync().Returns(Task.FromResult(assets.AsQueryable()));

        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        prRepo.FindAsync(originalPr.Id).Returns(Task.FromResult<PurchaseReceipt?>(originalPr));
        prRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseReceipt> { originalPr, returnPr }.AsQueryable()));

        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var manager = new PurchaseReceiptManager(poRepo, prRepo);

        await Should.NotThrowAsync(async () =>
            await manager.ValidateAssetReturnAsync(returnPr, assetRepo));
    }

    [Fact]
    public async Task ValidateAssetReturnAsync_PurchaseReceipt_ActiveAssetsExceedKeptQty_Throws()
    {
        var originalPr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-ASSET-002", DateTime.UtcNow.Date);
        originalPr.AddItem(_macbookItemId, "Macbook Pro", 3m, 1000m, 0m);

        var returnPr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-RET-002", DateTime.UtcNow.Date)
        {
            IsReturn = true,
            ReturnAgainstId = originalPr.Id
        };
        returnPr.AddItem(_macbookItemId, "Macbook Pro", -1m, 1000m, 0m);

        // 3 submitted assets -> active = 3 > kept qty (3 - 1 = 2)
        var asset1 = CreateAsset("AST-001", "Macbook 1", _macbookItemId, prId: originalPr.Id);
        asset1.Submit();
        var asset2 = CreateAsset("AST-002", "Macbook 2", _macbookItemId, prId: originalPr.Id);
        asset2.Submit();
        var asset3 = CreateAsset("AST-003", "Macbook 3", _macbookItemId, prId: originalPr.Id);
        asset3.Submit();

        var assets = new List<Asset> { asset1, asset2, asset3 };

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.GetQueryableAsync().Returns(Task.FromResult(assets.AsQueryable()));

        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        prRepo.FindAsync(originalPr.Id).Returns(Task.FromResult<PurchaseReceipt?>(originalPr));
        prRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseReceipt> { originalPr, returnPr }.AsQueryable()));

        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var manager = new PurchaseReceiptManager(poRepo, prRepo);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateAssetReturnAsync(returnPr, assetRepo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AssetExistsOnReturnDocument);
    }

    [Fact]
    public async Task ValidateAssetReturnAsync_PurchaseReceipt_CancelExtraAsset_ThenSucceeds()
    {
        var originalPr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-ASSET-003", DateTime.UtcNow.Date);
        originalPr.AddItem(_macbookItemId, "Macbook Pro", 3m, 1000m, 0m);

        var returnPr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-RET-003", DateTime.UtcNow.Date)
        {
            IsReturn = true,
            ReturnAgainstId = originalPr.Id
        };
        returnPr.AddItem(_macbookItemId, "Macbook Pro", -1m, 1000m, 0m);

        var asset1 = CreateAsset("AST-001", "Macbook 1", _macbookItemId, prId: originalPr.Id);
        asset1.Submit();
        var asset2 = CreateAsset("AST-002", "Macbook 2", _macbookItemId, prId: originalPr.Id);
        asset2.Submit();
        var asset3 = CreateAsset("AST-003", "Macbook 3", _macbookItemId, prId: originalPr.Id);
        asset3.Submit();
        asset3.Cancel(); // Cancelled -> active becomes 2 <= kept qty (2)

        var assets = new List<Asset> { asset1, asset2, asset3 };

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.GetQueryableAsync().Returns(Task.FromResult(assets.AsQueryable()));

        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        prRepo.FindAsync(originalPr.Id).Returns(Task.FromResult<PurchaseReceipt?>(originalPr));
        prRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseReceipt> { originalPr, returnPr }.AsQueryable()));

        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var manager = new PurchaseReceiptManager(poRepo, prRepo);

        await Should.NotThrowAsync(async () =>
            await manager.ValidateAssetReturnAsync(returnPr, assetRepo));
    }

    [Fact]
    public async Task ValidateAssetReturnAsync_PurchaseReceipt_UnlinkedSubmittedAsset_Throws()
    {
        var originalPr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-ASSET-004", DateTime.UtcNow.Date);
        originalPr.AddItem(_macbookItemId, "Macbook Pro", 3m, 1000m, 0m);

        var returnPr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-RET-004", DateTime.UtcNow.Date)
        {
            IsReturn = true,
            ReturnAgainstId = originalPr.Id
        };
        returnPr.AddItem(_macbookItemId, "Macbook Pro", -1m, 1000m, 0m);

        // Submitted asset with unknown/unlinked ItemId
        var unlinkedAsset = CreateAsset("AST-UNLINKED", "Unlinked Asset", Guid.NewGuid(), prId: originalPr.Id);
        unlinkedAsset.Submit();

        var assets = new List<Asset> { unlinkedAsset };

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.GetQueryableAsync().Returns(Task.FromResult(assets.AsQueryable()));

        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        prRepo.FindAsync(originalPr.Id).Returns(Task.FromResult<PurchaseReceipt?>(originalPr));
        prRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseReceipt> { originalPr, returnPr }.AsQueryable()));

        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var manager = new PurchaseReceiptManager(poRepo, prRepo);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateAssetReturnAsync(returnPr, assetRepo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AssetExistsOnReturnDocument);
    }

    [Fact]
    public async Task ValidateAssetReturnAsync_PurchaseInvoice_PartialReturnWithinRemainingQty_Succeeds()
    {
        var originalPi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PI-ASSET-001", DateTime.UtcNow.Date)
        {
            UpdateStock = true
        };
        originalPi.AddItem(_macbookItemId, "Server", 2m, 2000m, 0m);

        var returnPi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PI-RET-001", DateTime.UtcNow.Date)
        {
            IsReturn = true,
            ReturnAgainstId = originalPi.Id,
            UpdateStock = true
        };
        returnPi.AddItem(_macbookItemId, "Server", -1m, 2000m, 0m);

        // 1 submitted asset linked to PI -> active = 1 <= kept qty (2 - 1 = 1)
        var asset1 = CreateAsset("AST-SRV-001", "Server 1", _macbookItemId, piId: originalPi.Id);
        asset1.Submit();

        var assets = new List<Asset> { asset1 };

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.GetQueryableAsync().Returns(Task.FromResult(assets.AsQueryable()));

        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        piRepo.FindAsync(originalPi.Id).Returns(Task.FromResult<PurchaseInvoice?>(originalPi));
        piRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { originalPi, returnPi }.AsQueryable()));

        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var manager = new PurchaseInvoiceManager(supplierRepo, piRepo, poRepo);

        await Should.NotThrowAsync(async () =>
            await manager.ValidateAssetReturnAsync(returnPi, assetRepo));
    }

    [Fact]
    public async Task ValidateAssetReturnAsync_PurchaseInvoice_ActiveAssetsExceedKeptQty_Throws()
    {
        var originalPi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PI-ASSET-002", DateTime.UtcNow.Date)
        {
            UpdateStock = true
        };
        originalPi.AddItem(_macbookItemId, "Server", 2m, 2000m, 0m);

        var returnPi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PI-RET-002", DateTime.UtcNow.Date)
        {
            IsReturn = true,
            ReturnAgainstId = originalPi.Id,
            UpdateStock = true
        };
        returnPi.AddItem(_macbookItemId, "Server", -1m, 2000m, 0m);

        // 2 submitted assets -> active = 2 > kept qty (2 - 1 = 1)
        var asset1 = CreateAsset("AST-SRV-001", "Server 1", _macbookItemId, piId: originalPi.Id);
        asset1.Submit();
        var asset2 = CreateAsset("AST-SRV-002", "Server 2", _macbookItemId, piId: originalPi.Id);
        asset2.Submit();

        var assets = new List<Asset> { asset1, asset2 };

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.GetQueryableAsync().Returns(Task.FromResult(assets.AsQueryable()));

        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        piRepo.FindAsync(originalPi.Id).Returns(Task.FromResult<PurchaseInvoice?>(originalPi));
        piRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { originalPi, returnPi }.AsQueryable()));

        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var manager = new PurchaseInvoiceManager(supplierRepo, piRepo, poRepo);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateAssetReturnAsync(returnPi, assetRepo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AssetExistsOnReturnDocument);
    }
}
