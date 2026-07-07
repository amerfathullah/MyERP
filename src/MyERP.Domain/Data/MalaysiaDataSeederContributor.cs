using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;
using MyERP.Tax;
using MyERP.Tax.Entities;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Data;

/// <summary>
/// Seeds Malaysian Chart of Accounts, default SST tax rules, and statutory contribution rates.
/// Runs automatically via MyERP.DbMigrator or on first startup.
/// </summary>
public class MalaysiaDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<TaxCategory, Guid> _taxCategoryRepository;
    private readonly IRepository<TaxRule, Guid> _taxRuleRepository;
    private readonly IRepository<ContributionRule, Guid> _contributionRuleRepository;
    private readonly IGuidGenerator _guidGenerator;

    public MalaysiaDataSeederContributor(
        IRepository<Company, Guid> companyRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<TaxCategory, Guid> taxCategoryRepository,
        IRepository<TaxRule, Guid> taxRuleRepository,
        IRepository<ContributionRule, Guid> contributionRuleRepository,
        IGuidGenerator guidGenerator)
    {
        _companyRepository = companyRepository;
        _accountRepository = accountRepository;
        _taxCategoryRepository = taxCategoryRepository;
        _taxRuleRepository = taxRuleRepository;
        _contributionRuleRepository = contributionRuleRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _taxCategoryRepository.GetCountAsync() > 0)
            return; // Already seeded

        await SeedTaxCategoriesAsync();
        await SeedContributionRulesAsync();
    }

    private async Task SeedTaxCategoriesAsync()
    {
        // SST Tax Categories
        var salesTax = new TaxCategory(_guidGenerator.Create(), "SST-S", "Sales Tax (SST)", TaxType.Sales);
        var serviceTax = new TaxCategory(_guidGenerator.Create(), "SST-SV", "Service Tax (SST)", TaxType.Service);
        var exempt = new TaxCategory(_guidGenerator.Create(), "SST-E", "SST Exempt", TaxType.Exempt);
        var zeroRated = new TaxCategory(_guidGenerator.Create(), "SST-ZR", "Zero Rated", TaxType.ZeroRated);
        var outOfScope = new TaxCategory(_guidGenerator.Create(), "SST-OS", "Out of Scope", TaxType.OutOfScope);

        await _taxCategoryRepository.InsertAsync(salesTax, autoSave: true);
        await _taxCategoryRepository.InsertAsync(serviceTax, autoSave: true);
        await _taxCategoryRepository.InsertAsync(exempt, autoSave: true);
        await _taxCategoryRepository.InsertAsync(zeroRated, autoSave: true);
        await _taxCategoryRepository.InsertAsync(outOfScope, autoSave: true);

        // Tax Rules (effective rates)
        var effectiveDate = new DateTime(2024, 3, 1); // SST rate change effective date

        // Sales Tax: 10% (standard manufactured goods)
        await _taxRuleRepository.InsertAsync(new TaxRule(
            _guidGenerator.Create(), salesTax.Id, 10m, effectiveDate), autoSave: true);

        // Service Tax: 8%
        await _taxRuleRepository.InsertAsync(new TaxRule(
            _guidGenerator.Create(), serviceTax.Id, 8m, effectiveDate), autoSave: true);

        // Exempt / Zero Rated: 0%
        await _taxRuleRepository.InsertAsync(new TaxRule(
            _guidGenerator.Create(), exempt.Id, 0m, effectiveDate), autoSave: true);

        await _taxRuleRepository.InsertAsync(new TaxRule(
            _guidGenerator.Create(), zeroRated.Id, 0m, effectiveDate), autoSave: true);
    }

    private async Task SeedContributionRulesAsync()
    {
        var effectiveDate = new DateTime(2024, 1, 1);

        // EPF — Employee 11%, Employer 12% (salary <= RM5000) / 13% (salary > RM5000)
        await _contributionRuleRepository.InsertAsync(new ContributionRule(
            _guidGenerator.Create(), ContributionType.EPF, employeeRate: 11m, employerRate: 13m, effectiveDate)
        {
            MinimumSalary = 5001m,
            MaxAge = 60,
            CitizenshipFilter = CitizenshipType.Malaysian,
            IsActive = true
        }, autoSave: true);

        await _contributionRuleRepository.InsertAsync(new ContributionRule(
            _guidGenerator.Create(), ContributionType.EPF, employeeRate: 11m, employerRate: 12m, effectiveDate)
        {
            MaximumSalary = 5000m,
            MaxAge = 60,
            CitizenshipFilter = CitizenshipType.Malaysian,
            IsActive = true
        }, autoSave: true);

        // SOCSO — Employee 0.5%, Employer 1.75% (ceiling RM5000)
        await _contributionRuleRepository.InsertAsync(new ContributionRule(
            _guidGenerator.Create(), ContributionType.SOCSO, employeeRate: 0.5m, employerRate: 1.75m, effectiveDate)
        {
            SalaryCeiling = 5000m,
            MaxAge = 60,
            IsActive = true
        }, autoSave: true);

        // EIS — Employee 0.2%, Employer 0.2% (ceiling RM5000)
        await _contributionRuleRepository.InsertAsync(new ContributionRule(
            _guidGenerator.Create(), ContributionType.EIS, employeeRate: 0.2m, employerRate: 0.2m, effectiveDate)
        {
            SalaryCeiling = 5000m,
            MaxAge = 57,
            IsActive = true
        }, autoSave: true);

        // PCB (simplified flat estimate — real PCB uses graduated schedule)
        await _contributionRuleRepository.InsertAsync(new ContributionRule(
            _guidGenerator.Create(), ContributionType.PCB, employeeRate: 3m, employerRate: 0m, effectiveDate)
        {
            MinimumSalary = 2501m,
            CitizenshipFilter = CitizenshipType.Malaysian,
            IsActive = true
        }, autoSave: true);
    }
}
