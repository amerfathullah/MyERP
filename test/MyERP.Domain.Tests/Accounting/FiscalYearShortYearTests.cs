using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting;
using MyERP.Accounting.BackgroundJobs;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for Fiscal Year short year invariants, custom duration rules,
/// and auto-creation skipping. Verifies rules from erpnext/accounts/doctype/fiscal_year/fiscal_year.py (#5979).
/// </summary>
public class FiscalYearShortYearTests
{
    private readonly IRepository<FiscalYear, Guid> _fyRepository = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly FiscalYearCloseService _closeService;
    private readonly FiscalYearAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public FiscalYearShortYearTests()
    {
        _closeService = new FiscalYearCloseService(_fyRepository);
        _appService = new FiscalYearAppService(_fyRepository, _closeService);
    }

    [Fact]
    public async Task CreateAsync_NormalYear_WithInvalidDuration_ThrowsValidationException()
    {
        var allFys = new List<FiscalYear>();
        _fyRepository.GetQueryableAsync().Returns(Task.FromResult(allFys.AsQueryable()));

        var startDate = new DateTime(2026, 1, 1);
        var invalidEndDate = new DateTime(2026, 6, 30); // 6 months, not 1 full year

        var input = new CreateFiscalYearDto
        {
            CompanyId = _companyId,
            Name = "2026-Short",
            StartDate = startDate,
            EndDate = invalidEndDate,
            IsShortYear = false // Normal year requires 1 full year
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.CreateAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_NormalYear_WithExactOneYearDuration_Succeeds()
    {
        var allFys = new List<FiscalYear>();
        _fyRepository.GetQueryableAsync().Returns(Task.FromResult(allFys.AsQueryable()));

        var startDate = new DateTime(2026, 1, 1);
        var expectedEndDate = new DateTime(2026, 12, 31); // exactly 1 year - 1 day

        var input = new CreateFiscalYearDto
        {
            CompanyId = _companyId,
            Name = "2026",
            StartDate = startDate,
            EndDate = expectedEndDate,
            IsShortYear = false
        };

        FiscalYear? inserted = null;
        await _fyRepository.InsertAsync(Arg.Do<FiscalYear>(f => inserted = f));

        var result = await _appService.CreateAsync(input);

        Assert.NotNull(result);
        Assert.Equal("2026", result.Name);
        Assert.False(result.IsShortYear);
    }

    [Fact]
    public async Task CreateAsync_ShortYear_WithCustomDuration_Succeeds()
    {
        var allFys = new List<FiscalYear>();
        _fyRepository.GetQueryableAsync().Returns(Task.FromResult(allFys.AsQueryable()));

        var startDate = new DateTime(2026, 7, 1);
        var shortEndDate = new DateTime(2026, 12, 31); // 6 months custom duration

        var input = new CreateFiscalYearDto
        {
            CompanyId = _companyId,
            Name = "2026-H2",
            StartDate = startDate,
            EndDate = shortEndDate,
            IsShortYear = true // Flag bypasses 1-year rule
        };

        FiscalYear? inserted = null;
        await _fyRepository.InsertAsync(Arg.Do<FiscalYear>(f => inserted = f));

        var result = await _appService.CreateAsync(input);

        Assert.NotNull(result);
        Assert.Equal("2026-H2", result.Name);
        Assert.True(result.IsShortYear);
    }

    [Fact]
    public async Task FiscalYearAutoCreationJob_SkipsWhenLatestYearIsShortYear()
    {
        var latestShortFy = new FiscalYear(
            Guid.NewGuid(), _companyId, "2026-Short",
            new DateTime(2026, 7, 1), new DateTime(2026, 12, 31), isShortYear: true);

        var allFys = new List<FiscalYear> { latestShortFy };
        _fyRepository.GetQueryableAsync().Returns(Task.FromResult(allFys.AsQueryable()));

        var guidGen = Substitute.For<IGuidGenerator>();
        var logger = Substitute.For<ILogger<FiscalYearAutoCreationJob>>();
        var job = new FiscalYearAutoCreationJob(_fyRepository, guidGen, logger);

        // Run as of 3 days before short FY ends
        var args = new FiscalYearAutoCreationJobArgs
        {
            CompanyId = _companyId,
            AsOfDate = new DateTime(2026, 12, 28)
        };

        await job.ExecuteAsync(args);

        // Must not insert a new fiscal year because short year is skipped
        await _fyRepository.DidNotReceive().InsertAsync(Arg.Any<FiscalYear>());
    }

    [Fact]
    public async Task FiscalYearAutoCreationJob_CreatesNextYearForStandardFiscalYear()
    {
        var standardFy = new FiscalYear(
            Guid.NewGuid(), _companyId, "2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), isShortYear: false);

        var allFys = new List<FiscalYear> { standardFy };
        _fyRepository.GetQueryableAsync().Returns(Task.FromResult(allFys.AsQueryable()));

        var guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(Guid.NewGuid());
        var logger = Substitute.For<ILogger<FiscalYearAutoCreationJob>>();
        var job = new FiscalYearAutoCreationJob(_fyRepository, guidGen, logger);

        // Run as of 3 days before standard FY ends
        var args = new FiscalYearAutoCreationJobArgs
        {
            CompanyId = _companyId,
            AsOfDate = new DateTime(2026, 12, 28)
        };

        FiscalYear? createdFy = null;
        await _fyRepository.InsertAsync(Arg.Do<FiscalYear>(f => createdFy = f));

        await job.ExecuteAsync(args);

        Assert.NotNull(createdFy);
        Assert.Equal("2027-2027", createdFy.Name);
        Assert.Equal(new DateTime(2027, 1, 1), createdFy.StartDate);
        Assert.Equal(new DateTime(2027, 12, 31), createdFy.EndDate);
    }
}
