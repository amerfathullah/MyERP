using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Projects;

public abstract class ProjectCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Owner"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Other"), autoSave: true);

            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "Cross Co Cust"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                projectAppService.CreateAsync(new CreateProjectDto
                {
                    CompanyId = ownerCompany.Id,
                    ProjectName = "Alpha Project",
                    CustomerId = otherCustomer.Id
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SalesOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Owner 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Other 2"), autoSave: true);

            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "Cross Cust 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "ITEM-X", "Item X", ItemType.Goods), autoSave: true);

            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "SO-PROJ-X", DateTime.UtcNow);
            crossSo.AddItem(item.Id, "Item X", 1, 10, 0);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                projectAppService.CreateAsync(new CreateProjectDto
                {
                    CompanyId = ownerCompany.Id,
                    ProjectName = "Beta Project",
                    SalesOrderId = crossSo.Id
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_CostCenterFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Owner 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Other 3"), autoSave: true);

            var otherCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Cross CC"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                projectAppService.CreateAsync(new CreateProjectDto
                {
                    CompanyId = ownerCompany.Id,
                    ProjectName = "Gamma Project",
                    CostCenterId = otherCc.Id
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Owner 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Other 4"), autoSave: true);

            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "Cross Co Cust 4"), autoSave: true);

            var project = new Project(Guid.NewGuid(), ownerCompany.Id, "PROJ-UPD-004", "Delta Project");
            await projectRepo.InsertAsync(project, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                projectAppService.UpdateAsync(project.Id, new UpdateProjectDto
                {
                    ProjectName = "Delta Project Updated",
                    CustomerId = otherCustomer.Id
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_CostCenterFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var projectAppService = GetRequiredService<IProjectAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Owner 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Proj Co Other 5"), autoSave: true);

            var otherCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Cross CC 5"), autoSave: true);

            var project = new Project(Guid.NewGuid(), ownerCompany.Id, "PROJ-UPD-005", "Epsilon Project");
            await projectRepo.InsertAsync(project, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                projectAppService.UpdateAsync(project.Id, new UpdateProjectDto
                {
                    ProjectName = "Epsilon Project Updated",
                    CostCenterId = otherCc.Id
                }));
        });
    }
}
