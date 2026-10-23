using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class SerialNoAndBatchTraceabilityAppServiceTests
{
    private readonly IRepository<SerialNo, Guid> _serialNoRepo = Substitute.For<IRepository<SerialNo, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<StockEntry, Guid> _stockEntryRepo = Substitute.For<IRepository<StockEntry, Guid>>();
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<WorkOrder, Guid> _workOrderRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();

    private readonly SerialNoAndBatchTraceabilityAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _rmItemId = Guid.NewGuid();

    public SerialNoAndBatchTraceabilityAppServiceTests()
    {
        _appService = new SerialNoAndBatchTraceabilityAppService(
            _serialNoRepo,
            _batchRepo,
            _bundleRepo,
            _itemRepo,
            _warehouseRepo,
            _stockEntryRepo,
            _purchaseReceiptRepo,
            _deliveryNoteRepo,
            _salesInvoiceRepo,
            _workOrderRepo,
            _supplierRepo,
            _customerRepo);
    }

    [Fact]
    public async Task GetReportAsync_WhenNoFiltersMatch_ReturnsEmptyReport()
    {
        // Act
        var result = await _appService.GetReportAsync(new SerialNoAndBatchTraceabilityFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalNodes.ShouldBe(0);
        result.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_BackwardTraceability_TracesFromManufactureToPurchaseReceiptAndSupplier()
    {
        // Arrange
        var fgSerialNo = "FG-SN-001";
        var rmBatchNo = "RM-BATCH-001";
        var rmBatchId = Guid.NewGuid();

        var fgItem = new Item(_fgItemId, _companyId, "ITEM-FG", "Finished Laptop", ItemType.Goods) { HasSerialNo = true };
        var rmItem = new Item(_rmItemId, _companyId, "ITEM-RM", "RAM Chipset", ItemType.Goods) { HasBatchNo = true };
        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { fgItem, rmItem }));

        var wh = new Warehouse(_warehouseId, _companyId, "Plant WH");
        _warehouseRepo.GetListAsync().Returns(Task.FromResult(new List<Warehouse> { wh }));

        var batch = new Batch(rmBatchId, _rmItemId, rmBatchNo);
        _batchRepo.GetListAsync().Returns(Task.FromResult(new List<Batch> { batch }));

        var serial = new SerialNo(Guid.NewGuid(), _fgItemId, fgSerialNo, _companyId);
        _serialNoRepo.GetListAsync().Returns(Task.FromResult(new List<SerialNo> { serial }));

        var supplierId = Guid.NewGuid();
        var supplier = new Supplier(supplierId, _companyId, "Acme Semiconductor");
        _supplierRepo.GetListAsync().Returns(Task.FromResult(new List<Supplier> { supplier }));

        var prId = Guid.NewGuid();
        var pr = new PurchaseReceipt(prId, _companyId, supplierId, _warehouseId, "PR-2026-001", DateTime.UtcNow);
        _purchaseReceiptRepo.GetListAsync().Returns(Task.FromResult(new List<PurchaseReceipt> { pr }));

        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, _companyId, "WO-2026-001", _fgItemId, Guid.NewGuid(), 1m);
        _workOrderRepo.GetListAsync().Returns(Task.FromResult(new List<WorkOrder> { wo }));

        var seId = Guid.NewGuid();
        var se = new StockEntry(seId, _companyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            EntryNumber = "STE-MFG-001",
            WorkOrderId = woId
        };
        _stockEntryRepo.GetListAsync().Returns(Task.FromResult(new List<StockEntry> { se }));

        // 1. Inward bundle on PurchaseReceipt (received RM Batch from Acme)
        var prBundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _rmItemId, _warehouseId,
            BundleTransactionType.Inward, "PurchaseReceipt", prId, DateTime.UtcNow.AddDays(-5));
        prBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), prBundle.Id, 10, 50m, batchId: rmBatchId));

        // 2. Outward bundle on StockEntry (consumed RM Batch into FG Serial)
        var seOutBundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _rmItemId, _warehouseId,
            BundleTransactionType.Outward, "StockEntry", seId, DateTime.UtcNow.AddDays(-2));
        seOutBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), seOutBundle.Id, 1, 50m, batchId: rmBatchId, fgSerialNo: fgSerialNo));

        // 3. Inward bundle on StockEntry (produced FG Serial)
        var seInBundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _fgItemId, _warehouseId,
            BundleTransactionType.Inward, "StockEntry", seId, DateTime.UtcNow.AddDays(-2));
        seInBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), seInBundle.Id, 1, 500m, serialNo: fgSerialNo));

        var allBundles = new List<SerialAndBatchBundle> { prBundle, seOutBundle, seInBundle };
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(allBundles.AsQueryable()));

        _customerRepo.GetListAsync().Returns(Task.FromResult(new List<Customer>()));
        _deliveryNoteRepo.GetListAsync().Returns(Task.FromResult(new List<DeliveryNote>()));
        _salesInvoiceRepo.GetListAsync().Returns(Task.FromResult(new List<SalesInvoice>()));

        // Act
        var result = await _appService.GetReportAsync(new SerialNoAndBatchTraceabilityFilterDto
        {
            CompanyId = _companyId,
            SerialNo = fgSerialNo,
            TraceabilityDirection = "Backward"
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalNodes.ShouldBeGreaterThanOrEqualTo(2);

        // Root node: FG production at Stock Entry
        var root = result.Nodes.FirstOrDefault(n => n.Indent == 0);
        root.ShouldNotBeNull();
        root.ItemCode.ShouldBe("ITEM-FG");
        root.SerialNo.ShouldBe(fgSerialNo);
        root.ReferenceDocType.ShouldBe("StockEntry");
        root.ReferenceDocNo.ShouldBe("STE-MFG-001");
        root.WorkOrderNo.ShouldBe("WO-2026-001");

        // Child node: RM Batch consumed
        var child = result.Nodes.FirstOrDefault(n => n.Indent == 1);
        child.ShouldNotBeNull();
        child.ItemCode.ShouldBe("ITEM-RM");
        child.BatchNo.ShouldBe(rmBatchNo);
        child.ReferenceDocType.ShouldBe("PurchaseReceipt");
        child.ReferenceDocNo.ShouldBe("PR-2026-001");
        child.SupplierName.ShouldBe("Acme Semiconductor");
    }

    [Fact]
    public async Task GetReportAsync_ForwardTraceability_TracesFromRawMaterialToDeliveryNoteAndCustomer()
    {
        // Arrange
        var fgSerialNo = "FG-SN-002";
        var rmBatchNo = "RM-BATCH-002";
        var rmBatchId = Guid.NewGuid();

        var fgItem = new Item(_fgItemId, _companyId, "ITEM-FG", "Finished Laptop", ItemType.Goods) { HasSerialNo = true };
        var rmItem = new Item(_rmItemId, _companyId, "ITEM-RM", "RAM Chipset", ItemType.Goods) { HasBatchNo = true };
        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { fgItem, rmItem }));

        var wh = new Warehouse(_warehouseId, _companyId, "Plant WH");
        _warehouseRepo.GetListAsync().Returns(Task.FromResult(new List<Warehouse> { wh }));

        var batch = new Batch(rmBatchId, _rmItemId, rmBatchNo);
        _batchRepo.GetListAsync().Returns(Task.FromResult(new List<Batch> { batch }));

        var serial = new SerialNo(Guid.NewGuid(), _fgItemId, fgSerialNo, _companyId);
        _serialNoRepo.GetListAsync().Returns(Task.FromResult(new List<SerialNo> { serial }));

        var customerId = Guid.NewGuid();
        var customer = new Customer(customerId, _companyId, "Global Logistics Corp");
        _customerRepo.GetListAsync().Returns(Task.FromResult(new List<Customer> { customer }));

        var dnId = Guid.NewGuid();
        var dn = new DeliveryNote(dnId, _companyId, customerId, _warehouseId, "DN-2026-001", DateTime.UtcNow);
        _deliveryNoteRepo.GetListAsync().Returns(Task.FromResult(new List<DeliveryNote> { dn }));

        var seId = Guid.NewGuid();
        var se = new StockEntry(seId, _companyId, StockEntryType.Manufacture, DateTime.UtcNow) { EntryNumber = "STE-MFG-002" };
        _stockEntryRepo.GetListAsync().Returns(Task.FromResult(new List<StockEntry> { se }));

        // 1. Outward bundle on StockEntry (consumed RM Batch into FG Serial)
        var seOutBundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _rmItemId, _warehouseId,
            BundleTransactionType.Outward, "StockEntry", seId, DateTime.UtcNow.AddDays(-2));
        seOutBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), seOutBundle.Id, 1, 50m, batchId: rmBatchId, fgSerialNo: fgSerialNo));

        // 2. Inward bundle on StockEntry (produced FG Serial)
        var seInBundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _fgItemId, _warehouseId,
            BundleTransactionType.Inward, "StockEntry", seId, DateTime.UtcNow.AddDays(-2));
        seInBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), seInBundle.Id, 1, 500m, serialNo: fgSerialNo));

        // 3. Outward bundle on DeliveryNote (delivered FG Serial to Customer)
        var dnBundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _fgItemId, _warehouseId,
            BundleTransactionType.Outward, "DeliveryNote", dnId, DateTime.UtcNow.AddDays(-1));
        dnBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), dnBundle.Id, 1, 500m, serialNo: fgSerialNo));

        var allBundles = new List<SerialAndBatchBundle> { seOutBundle, seInBundle, dnBundle };
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(allBundles.AsQueryable()));

        _supplierRepo.GetListAsync().Returns(Task.FromResult(new List<Supplier>()));
        _purchaseReceiptRepo.GetListAsync().Returns(Task.FromResult(new List<PurchaseReceipt>()));
        _salesInvoiceRepo.GetListAsync().Returns(Task.FromResult(new List<SalesInvoice>()));
        _workOrderRepo.GetListAsync().Returns(Task.FromResult(new List<WorkOrder>()));

        // Act
        var result = await _appService.GetReportAsync(new SerialNoAndBatchTraceabilityFilterDto
        {
            CompanyId = _companyId,
            BatchId = rmBatchId,
            TraceabilityDirection = "Forward"
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalNodes.ShouldBeGreaterThanOrEqualTo(2);

        // Root node: Raw material consumption at Stock Entry
        var root = result.Nodes.FirstOrDefault(n => n.Indent == 0);
        root.ShouldNotBeNull();
        root.ItemCode.ShouldBe("ITEM-RM");
        root.BatchNo.ShouldBe(rmBatchNo);
        root.ReferenceDocType.ShouldBe("StockEntry");

        // Child node: Finished Good delivered to Customer
        var child = result.Nodes.FirstOrDefault(n => n.Indent == 1);
        child.ShouldNotBeNull();
        child.ItemCode.ShouldBe("ITEM-FG");
        child.SerialNo.ShouldBe(fgSerialNo);
        child.ReferenceDocType.ShouldBe("DeliveryNote");
        child.ReferenceDocNo.ShouldBe("DN-2026-001");
        child.CustomerName.ShouldBe("Global Logistics Corp");
    }

    [Fact]
    public async Task GetReportAsync_BackwardTraceability_ProratesMultiReceiptBatchAndTracesSharedComponentAcrossBranches()
    {
        // Per ERPNext PR #61428 (commit bfa9be5a30):
        // When a component batch is received across multiple inward bundles (e.g. 60 + 40 = 100),
        // backward traceability prorates the consumed quantity (50 -> 30 and 20) across both receipts.
        var fgBatchId = Guid.NewGuid();
        var rmBatchId = Guid.NewGuid();

        var fgItem = new Item(_fgItemId, _companyId, "ITEM-FG", "Finished Good", ItemType.Goods) { HasBatchNo = true };
        var rmItem = new Item(_rmItemId, _companyId, "ITEM-RM", "Raw Material", ItemType.Goods) { HasBatchNo = true };
        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { fgItem, rmItem }));

        var wh = new Warehouse(_warehouseId, _companyId, "Plant WH");
        _warehouseRepo.GetListAsync().Returns(Task.FromResult(new List<Warehouse> { wh }));

        var fgBatch = new Batch(fgBatchId, _fgItemId, "FG-B-100");
        var rmBatch = new Batch(rmBatchId, _rmItemId, "RM-B-100");
        _batchRepo.GetListAsync().Returns(Task.FromResult(new List<Batch> { fgBatch, rmBatch }));
        _serialNoRepo.GetListAsync().Returns(Task.FromResult(new List<SerialNo>()));

        var supplierId = Guid.NewGuid();
        var supplier = new Supplier(supplierId, _companyId, "Supplier A");
        _supplierRepo.GetListAsync().Returns(Task.FromResult(new List<Supplier> { supplier }));

        var pr1Id = Guid.NewGuid();
        var pr2Id = Guid.NewGuid();
        var pr1 = new PurchaseReceipt(pr1Id, _companyId, supplierId, _warehouseId, "PR-001", DateTime.UtcNow.AddDays(-5));
        var pr2 = new PurchaseReceipt(pr2Id, _companyId, supplierId, _warehouseId, "PR-002", DateTime.UtcNow.AddDays(-4));
        _purchaseReceiptRepo.GetListAsync().Returns(Task.FromResult(new List<PurchaseReceipt> { pr1, pr2 }));

        var seId = Guid.NewGuid();
        var se = new StockEntry(seId, _companyId, StockEntryType.Manufacture, DateTime.UtcNow.AddDays(-1)) { EntryNumber = "STE-100" };
        _stockEntryRepo.GetListAsync().Returns(Task.FromResult(new List<StockEntry> { se }));

        var pr1Bundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _rmItemId, _warehouseId,
            BundleTransactionType.Inward, "PurchaseReceipt", pr1Id, DateTime.UtcNow.AddDays(-5));
        pr1Bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), pr1Bundle.Id, 60m, 10m, batchId: rmBatchId));

        var pr2Bundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _rmItemId, _warehouseId,
            BundleTransactionType.Inward, "PurchaseReceipt", pr2Id, DateTime.UtcNow.AddDays(-4));
        pr2Bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), pr2Bundle.Id, 40m, 10m, batchId: rmBatchId));

        var seOutBundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _rmItemId, _warehouseId,
            BundleTransactionType.Outward, "StockEntry", seId, DateTime.UtcNow.AddDays(-1));
        seOutBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), seOutBundle.Id, 50m, 10m, batchId: rmBatchId));

        var seInBundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _fgItemId, _warehouseId,
            BundleTransactionType.Inward, "StockEntry", seId, DateTime.UtcNow.AddDays(-1));
        seInBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), seInBundle.Id, 10m, 50m, batchId: fgBatchId));

        var allBundles = new List<SerialAndBatchBundle> { pr1Bundle, pr2Bundle, seOutBundle, seInBundle };
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(allBundles.AsQueryable()));

        _customerRepo.GetListAsync().Returns(Task.FromResult(new List<Customer>()));
        _deliveryNoteRepo.GetListAsync().Returns(Task.FromResult(new List<DeliveryNote>()));
        _salesInvoiceRepo.GetListAsync().Returns(Task.FromResult(new List<SalesInvoice>()));
        _workOrderRepo.GetListAsync().Returns(Task.FromResult(new List<WorkOrder>()));

        var result = await _appService.GetReportAsync(new SerialNoAndBatchTraceabilityFilterDto
        {
            CompanyId = _companyId,
            BatchId = fgBatchId,
            TraceabilityDirection = "Backward"
        });

        var rmNodes = result.Nodes.Where(n => n.Indent == 1).OrderBy(n => n.ReferenceDocNo).ToList();
        rmNodes.Count.ShouldBe(2);
        rmNodes[0].ReferenceDocNo.ShouldBe("PR-001");
        rmNodes[0].Quantity.ShouldBe(30m); // 50 * (60 / 100)
        rmNodes[1].ReferenceDocNo.ShouldBe("PR-002");
        rmNodes[1].Quantity.ShouldBe(20m); // 50 * (40 / 100)
    }
}
