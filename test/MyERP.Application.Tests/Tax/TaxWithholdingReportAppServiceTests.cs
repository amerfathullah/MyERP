using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Tax.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Tax;

public abstract class TaxWithholdingReportAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ITaxWithholdingReportAppService _reportService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly IRepository<FiscalYear, Guid> _fyRepository;
    private readonly IRepository<TaxWithholdingEntry, Guid> _entryRepository;

    protected TaxWithholdingReportAppServiceTests()
    {
        _reportService = GetRequiredService<ITaxWithholdingReportAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
        _piRepository = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
        _fyRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        _entryRepository = GetRequiredService<IRepository<TaxWithholdingEntry, Guid>>();
    }

    [Fact]
    public async Task Should_Return_WithholdingDetails_And_Keep_Ldc_Rates_Separate_In_Summary()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "TDS Test Co"), autoSave: true);

        await _fyRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY2025", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)),
            autoSave: true);
        await _fyRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)),
            autoSave: true);

        var supplier = await _supplierRepository.InsertAsync(
            new Supplier(Guid.NewGuid(), company.Id, "Overseas Consultancy") { Tin = "C987654321" },
            autoSave: true);

        var pi1 = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PINV-TDS-1", new DateTime(2026, 3, 10));
        await _piRepository.InsertAsync(pi1, autoSave: true);
        var pi2 = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PINV-TDS-2", new DateTime(2026, 4, 15));
        await _piRepository.InsertAsync(pi2, autoSave: true);
        var pi3 = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PINV-TDS-3", new DateTime(2026, 5, 20));
        await _piRepository.InsertAsync(pi3, autoSave: true);

        var taxAccId = Guid.NewGuid();
        var e1 = new TaxWithholdingEntry(
            Guid.NewGuid(), company.Id, supplier.Id, "PurchaseInvoice", pi1.Id,
            taxAccId, 10m, 1000m, new DateTime(2026, 3, 10))
        {
            TaxCategory = "Section 107A",
        };
        e1.Submit();
        await _entryRepository.InsertAsync(e1, autoSave: true);

        var e2 = new TaxWithholdingEntry(
            Guid.NewGuid(), company.Id, supplier.Id, "PurchaseInvoice", pi2.Id,
            taxAccId, 10m, 2000m, new DateTime(2026, 4, 15))
        {
            TaxCategory = "Section 107A",
        };
        e2.Submit();
        await _entryRepository.InsertAsync(e2, autoSave: true);

        // LDC entry with reduced 5% rate must NOT merge with 10% rows in summary
        var e3 = new TaxWithholdingEntry(
            Guid.NewGuid(), company.Id, supplier.Id, "PurchaseInvoice", pi3.Id,
            taxAccId, 10m, 4000m, new DateTime(2026, 5, 20))
        {
            TaxCategory = "Section 107A",
        };
        e3.ApplyLDC(5m, "LDC-2026-01");
        e3.Submit();
        await _entryRepository.InsertAsync(e3, autoSave: true);

        // Draft entry must be excluded
        var draft = new TaxWithholdingEntry(
            Guid.NewGuid(), company.Id, supplier.Id, "PurchaseInvoice", pi3.Id,
            taxAccId, 10m, 500m, new DateTime(2026, 5, 21))
        {
            TaxCategory = "Section 107A",
        };
        await _entryRepository.InsertAsync(draft, autoSave: true);

        var details = await _reportService.GetDetailsAsync(new TaxWithholdingReportFilterDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
        });
        details.TotalCount.ShouldBe(3);
        details.TotalTaxableAmount.ShouldBe(7000m);
        details.TotalWithheldAmount.ShouldBe(500m); // 100 + 200 + 200
        details.Rows.ShouldContain(r => r.VoucherNumber == "PINV-TDS-3" && r.Rate == 5m && r.HasLdc && r.Tin == "C987654321");

        var summary = await _reportService.GetTdsComputationSummaryAsync(new TaxWithholdingReportFilterDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
        });
        summary.FiscalYearName.ShouldBe("FY2026");
        summary.TotalCount.ShouldBe(2); // 10% group + 5% LDC group
        summary.Rows.Single(r => r.Rate == 10m).TransactionCount.ShouldBe(2);
        summary.Rows.Single(r => r.Rate == 10m).TotalTaxableAmount.ShouldBe(3000m);
        summary.Rows.Single(r => r.Rate == 10m).TotalWithheldAmount.ShouldBe(300m);
        summary.Rows.Single(r => r.Rate == 5m).HasLdc.ShouldBeTrue();
        summary.Rows.Single(r => r.Rate == 5m).TotalWithheldAmount.ShouldBe(200m);

        // Cross-fiscal-year range rejected per ERPNext tds_computation_summary
        await Should.ThrowAsync<BusinessException>(() =>
            _reportService.GetTdsComputationSummaryAsync(new TaxWithholdingReportFilterDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2025, 12, 1),
                ToDate = new DateTime(2026, 2, 1),
            }));
    }
}
