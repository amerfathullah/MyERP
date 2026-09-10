using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Support;

/// <summary>
/// Regression coverage for a real gap found via a CompanyRestrictionValidationService spot-check
/// sweep: IssueAppService.CreateAsync accepted an optional CustomerId with no check that the
/// customer belonged to the Issue's Company, unlike other AppServices fixed this session.
/// </summary>
public abstract class IssueCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var issueAppService = GetRequiredService<IIssueAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Issue Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Issue Guard Other Co"), autoSave: true);

            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "Issue Guard Cross-Co Customer"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                issueAppService.CreateAsync(new CreateIssueDto
                {
                    CompanyId = ownerCompany.Id,
                    Subject = "Cross-company issue",
                    CustomerId = customer.Id,
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_NoCustomer_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var issueAppService = GetRequiredService<IIssueAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Issue Guard Happy Co"), autoSave: true);

            var dto = await issueAppService.CreateAsync(new CreateIssueDto
            {
                CompanyId = company.Id,
                Subject = "Internal issue, no customer",
            });

            dto.Subject.ShouldBe("Internal issue, no customer");
        });
    }

    [Fact]
    public async Task CreateAsync_SameCompanyCustomer_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var issueAppService = GetRequiredService<IIssueAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Issue Guard Same Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Issue Guard Same-Co Customer"), autoSave: true);

            var dto = await issueAppService.CreateAsync(new CreateIssueDto
            {
                CompanyId = company.Id,
                Subject = "Same-company issue",
                CustomerId = customer.Id,
            });

            dto.CustomerId.ShouldBe(customer.Id);
        });
    }
}
