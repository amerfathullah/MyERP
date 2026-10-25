using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting.Tests;

public class CostCenterAppServiceTests
{
    [Fact]
    public void FormatCostCenterName_WithNumberAndAbbreviation_FormatsCorrectly()
    {
        // Enforces parity with ERPNext PR #62103 / commit cf4e3a0715 & 4eafaf3371:
        // get_autoname_with_number appends prefix as number and suffix as company abbreviation
        var result = CostCenterAppService.FormatCostCenterName("Main Operations", "100", "_TC");
        Assert.Equal("100 - Main Operations - _TC", result);
    }

    [Fact]
    public void FormatCostCenterName_WithoutNumber_FormatsTitleAndAbbreviation()
    {
        var result = CostCenterAppService.FormatCostCenterName("Main Operations", "", "_TC");
        Assert.Equal("Main Operations - _TC", result);
    }

    [Fact]
    public void FormatCostCenterName_AlreadyHasAbbreviationSuffix_DoesNotDuplicateSuffix()
    {
        var result = CostCenterAppService.FormatCostCenterName("Main Operations - _TC", null, "_TC");
        Assert.Equal("Main Operations - _TC", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatCostCenterName_MissingAbbreviation_ThrowsValidation(string? abbr)
    {
        var ex = Assert.Throws<BusinessException>(() =>
            CostCenterAppService.FormatCostCenterName("Main", null, abbr!));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_CompanyMissingAbbreviation_ThrowsValidation()
    {
        var repo = Substitute.For<IRepository<CostCenter, Guid>>();
        var appService = new CostCenterAppService(repo);

        var companyId = Guid.NewGuid();
        var companyWithoutAbbr = new Company(companyId, "Company Without Abbr")
        {
            ShortName = null
        };

        var compRepo = Substitute.For<IRepository<Company, Guid>>();
        compRepo.FindAsync(companyId).Returns(Task.FromResult<Company?>(companyWithoutAbbr));

        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IRepository<Company, Guid>)).Returns(compRepo);
        var lazySp = new Volo.Abp.DependencyInjection.AbpLazyServiceProvider(sp);
        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider")!
            .SetValue(appService, lazySp);

        var input = new CreateCostCenterDto
        {
            CompanyId = companyId,
            Name = "Production Plant",
            CostCenterNumber = "200"
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(async () =>
            await appService.CreateAsync(input));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }
}
