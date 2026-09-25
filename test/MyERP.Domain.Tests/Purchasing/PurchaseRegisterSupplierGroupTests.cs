using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

/// <summary>
/// Tests verifying ERPNext PR #59430 (commit 75efa2d1cf):
/// Purchase Register filters by supplier group resolved from Supplier master,
/// and populates SupplierName and SupplierGroupName on report lines.
/// </summary>
public class PurchaseRegisterSupplierGroupTests
{
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<SupplierGroup, Guid> _supplierGroupRepo = Substitute.For<IRepository<SupplierGroup, Guid>>();

    private readonly PurchaseRegisterAppService _purchaseRegisterAppService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierGroupIdRaw = Guid.NewGuid();
    private readonly Guid _supplierGroupIdServices = Guid.NewGuid();

    private readonly Supplier _supplierRaw;
    private readonly Supplier _supplierServices;
    private readonly SupplierGroup _groupRaw;
    private readonly SupplierGroup _groupServices;

    public PurchaseRegisterSupplierGroupTests()
    {
        _purchaseRegisterAppService = new PurchaseRegisterAppService(
            _piRepo, _peRepo, _supplierRepo, _supplierGroupRepo);

        _groupRaw = new SupplierGroup(_supplierGroupIdRaw, "Raw Material Suppliers");
        _groupServices = new SupplierGroup(_supplierGroupIdServices, "Services Suppliers");

        _supplierRaw = new Supplier(Guid.NewGuid(), _companyId, "Acme Steel Supplies")
        {
            SupplierGroupId = _supplierGroupIdRaw
        };

        _supplierServices = new Supplier(Guid.NewGuid(), _companyId, "Tech Consulting Corp")
        {
            SupplierGroupId = _supplierGroupIdServices
        };

        var suppliers = new List<Supplier> { _supplierRaw, _supplierServices };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(suppliers.AsQueryable()));
        _supplierRepo.FindAsync(_supplierRaw.Id).Returns(Task.FromResult<Supplier?>(_supplierRaw));
        _supplierRepo.FindAsync(_supplierServices.Id).Returns(Task.FromResult<Supplier?>(_supplierServices));

        var groups = new List<SupplierGroup> { _groupRaw, _groupServices };
        _supplierGroupRepo.GetQueryableAsync().Returns(Task.FromResult(groups.AsQueryable()));

        _peRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentEntry>().AsQueryable()));
        _peRepo.WithDetailsAsync(Arg.Any<Expression<Func<PaymentEntry, object>>[]>())
            .Returns(Task.FromResult(new List<PaymentEntry>().AsQueryable()));
    }

    [Fact]
    public async Task PurchaseRegister_SupplierGroupFilter_UsesSupplierMaster()
    {
        var issueDate = DateTime.UtcNow.Date;

        var pi1 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierRaw.Id, "PINV-001", issueDate);
        pi1.AddItem(Guid.NewGuid(), "Steel Rods", 10m, 100m, 0m);
        pi1.Submit();
        pi1.Post();

        var pi2 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierServices.Id, "PINV-002", issueDate);
        pi2.AddItem(Guid.NewGuid(), "Consulting Hours", 5m, 200m, 0m);
        pi2.Submit();
        pi2.Post();

        _piRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { pi1, pi2 }.AsQueryable()));

        // Filter by Raw Material Suppliers group
        var filter = new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = issueDate.AddDays(-1),
            ToDate = issueDate.AddDays(1),
            SupplierGroupId = _supplierGroupIdRaw
        };

        var report = await _purchaseRegisterAppService.GetReportAsync(filter);

        Assert.Equal(1, report.Count);
        var line = report.Items.Single();
        Assert.Equal("PINV-001", line.InvoiceNumber);
        Assert.Equal(_supplierRaw.Id, line.SupplierId);
        Assert.Equal("Acme Steel Supplies", line.SupplierName);
        Assert.Equal(_supplierGroupIdRaw, line.SupplierGroupId);
        Assert.Equal("Raw Material Suppliers", line.SupplierGroupName);
    }

    [Fact]
    public async Task PurchaseRegister_WhenSupplierChangesGroup_UsesCurrentSupplierMaster()
    {
        var issueDate = DateTime.UtcNow.Date;

        // Invoice created when supplier was in previous group
        var pi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierRaw.Id, "PINV-LEGACY", issueDate);
        pi.AddItem(Guid.NewGuid(), "Misc Supplies", 1m, 500m, 0m);
        pi.Submit();
        pi.Post();

        _piRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { pi }.AsQueryable()));

        // Move supplier to Services group
        _supplierRaw.SupplierGroupId = _supplierGroupIdServices;

        // Query by Services group
        var report = await _purchaseRegisterAppService.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = issueDate.AddDays(-1),
            ToDate = issueDate.AddDays(1),
            SupplierGroupId = _supplierGroupIdServices
        });

        Assert.Equal(1, report.Count);
        Assert.Equal("PINV-LEGACY", report.Items[0].InvoiceNumber);
        Assert.Equal("Services Suppliers", report.Items[0].SupplierGroupName);

        // Query by old group should now return 0
        var reportOld = await _purchaseRegisterAppService.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = issueDate.AddDays(-1),
            ToDate = issueDate.AddDays(1),
            SupplierGroupId = _supplierGroupIdRaw
        });

        Assert.Equal(0, reportOld.Count);
    }

    [Fact]
    public async Task PurchaseRegister_SupplierAndGroupFilter_Mismatch_ReturnsEmpty()
    {
        var issueDate = DateTime.UtcNow.Date;

        var pi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierRaw.Id, "PINV-001", issueDate);
        pi.AddItem(Guid.NewGuid(), "Steel Rods", 10m, 100m, 0m);
        pi.Submit();
        pi.Post();

        _piRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { pi }.AsQueryable()));

        // SupplierRaw belongs to RawMaterial, but filter specifies Services group
        var filter = new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = issueDate.AddDays(-1),
            ToDate = issueDate.AddDays(1),
            SupplierId = _supplierRaw.Id,
            SupplierGroupId = _supplierGroupIdServices
        };

        var report = await _purchaseRegisterAppService.GetReportAsync(filter);
        Assert.Equal(0, report.Count);
    }

    [Fact]
    public async Task PurchaseRegister_WithIncludePayments_PopulatesSupplierDetails()
    {
        var issueDate = DateTime.UtcNow.Date;

        var pi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierRaw.Id, "PINV-PAY", issueDate);
        pi.AddItem(Guid.NewGuid(), "Materials", 1m, 1000m, 0m);
        pi.Submit();
        pi.Post();

        _piRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { pi }.AsQueryable()));

        var filter = new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = issueDate.AddDays(-1),
            ToDate = issueDate.AddDays(1),
            SupplierId = _supplierRaw.Id,
            SupplierGroupId = _supplierGroupIdRaw,
            IncludePayments = true
        };

        var report = await _purchaseRegisterAppService.GetReportAsync(filter);
        Assert.NotEmpty(report.Items);
        foreach (var item in report.Items)
        {
            Assert.Equal("Acme Steel Supplies", item.SupplierName);
            Assert.Equal(_supplierGroupIdRaw, item.SupplierGroupId);
            Assert.Equal("Raw Material Suppliers", item.SupplierGroupName);
        }
    }
}
