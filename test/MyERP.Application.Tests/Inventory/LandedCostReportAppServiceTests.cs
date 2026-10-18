using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

public class LandedCostReportAppServiceTests
{
    private readonly IRepository<LandedCostVoucher, Guid> _lcvRepo = Substitute.For<IRepository<LandedCostVoucher, Guid>>();
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<StockEntry, Guid> _stockEntryRepo = Substitute.For<IRepository<StockEntry, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private readonly LandedCostReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public LandedCostReportAppServiceTests()
    {
        _appService = new LandedCostReportAppService(
            _lcvRepo,
            _purchaseReceiptRepo,
            _purchaseInvoiceRepo,
            _stockEntryRepo,
            _supplierRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReceiptMatches_ReturnsAllocatedCostsAndReceiptInfo()
    {
        // Arrange (corresponds to ERPNext test_landed_cost_applied_to_receipt)
        var supplierId = Guid.NewGuid();
        var supplier = new Supplier(supplierId, _companyId, "Acme Logistics");
        _supplierRepo.GetListAsync().Returns(Task.FromResult(new List<Supplier> { supplier }));

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-001", "Raw Steel Plate", ItemType.Goods);
        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { item }));

        var prId = Guid.NewGuid();
        var pr = new PurchaseReceipt(prId, _companyId, supplierId, Guid.NewGuid(), "PR-2026-0010", DateTime.UtcNow)
        {
            GrandTotal = 1000m
        };
        _purchaseReceiptRepo.GetListAsync().Returns(Task.FromResult(new List<PurchaseReceipt> { pr }));
        _purchaseInvoiceRepo.GetListAsync().Returns(Task.FromResult(new List<PurchaseInvoice>()));
        _stockEntryRepo.GetListAsync().Returns(Task.FromResult(new List<StockEntry>()));

        var lcv = new LandedCostVoucher(Guid.NewGuid(), _companyId, DateTime.UtcNow)
        {
            VoucherNumber = "LCV-2026-001",
            DistributionMethod = LandedCostDistributionMethod.BasedOnAmount
        };
        lcv.AddItem(prId, "PurchaseReceipt", itemId, 10m, 1000m, "Raw Steel Plate");
        lcv.AddCharge("Customs Duty & Shipping", Guid.NewGuid(), 75m);
        lcv.Submit();

        _lcvRepo.WithDetailsAsync().Returns(Task.FromResult(new List<LandedCostVoucher> { lcv }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new LandedCostReportFilterDto
        {
            CompanyId = _companyId,
            ReceiptDocumentNo = "PR-2026-0010"
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalVouchersCount.ShouldBe(1);
        result.TotalLandedCost.ShouldBe(75m);
        result.TotalReceiptsAffected.ShouldBe(1);
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.LandedCostVoucherNo.ShouldBe("LCV-2026-001");
        row.ReceiptDocumentType.ShouldBe("PurchaseReceipt");
        row.ReceiptDocumentNo.ShouldBe("PR-2026-0010");
        row.SupplierName.ShouldBe("Acme Logistics");
        row.TotalLandedCost.ShouldBe(75m);
        row.AllocatedCharges.ShouldBe(75m);
        row.ReceiptTotalAmount.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetReportAsync_FilterByDocumentType_FiltersCorrectly()
    {
        // Arrange
        var prId = Guid.NewGuid();
        var piId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var pr = new PurchaseReceipt(prId, _companyId, Guid.NewGuid(), Guid.NewGuid(), "PR-01", DateTime.UtcNow) { GrandTotal = 500m };
        var pi = new PurchaseInvoice(piId, _companyId, Guid.NewGuid(), "PI-01", DateTime.UtcNow) { GrandTotal = 300m };

        _purchaseReceiptRepo.GetListAsync().Returns(Task.FromResult(new List<PurchaseReceipt> { pr }));
        _purchaseInvoiceRepo.GetListAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { pi }));
        _stockEntryRepo.GetListAsync().Returns(Task.FromResult(new List<StockEntry>()));
        _supplierRepo.GetListAsync().Returns(Task.FromResult(new List<Supplier>()));
        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item>()));

        var lcv = new LandedCostVoucher(Guid.NewGuid(), _companyId, DateTime.UtcNow)
        {
            VoucherNumber = "LCV-01",
            DistributionMethod = LandedCostDistributionMethod.BasedOnAmount
        };
        lcv.AddItem(prId, "PurchaseReceipt", itemId, 5m, 500m);
        lcv.AddItem(piId, "PurchaseInvoice", itemId, 3m, 300m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 80m);
        lcv.Submit();

        _lcvRepo.WithDetailsAsync().Returns(Task.FromResult(new List<LandedCostVoucher> { lcv }.AsQueryable()));

        // Act: Filter only PurchaseInvoice
        var result = await _appService.GetReportAsync(new LandedCostReportFilterDto
        {
            CompanyId = _companyId,
            ReceiptDocumentType = "PurchaseInvoice"
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ReceiptDocumentType.ShouldBe("PurchaseInvoice");
        result.Rows[0].ReceiptDocumentNo.ShouldBe("PI-01");
        result.Rows[0].AllocatedCharges.ShouldBe(30m);
    }
}
