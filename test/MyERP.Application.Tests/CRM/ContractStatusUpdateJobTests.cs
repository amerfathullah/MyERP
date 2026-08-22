using System;
using System.Threading.Tasks;
using MyERP.CRM.BackgroundJobs;
using MyERP.CRM.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

/// <summary>
/// Regression coverage for a bug found while investigating why
/// ContractRenewalService.ProcessRenewalsAsync had zero callers: it turned out to be a THIRD,
/// unused implementation of renew-or-deactivate logic already fully duplicated (correctly) in
/// ContractExpiryJob. But ContractStatusUpdateJob is a separate, ALSO-nightly-enqueued job that
/// queried the exact same "expired, still Active" contract set and unconditionally deactivated
/// every one — including IsAutoRenewal contracts that ContractExpiryJob would otherwise renew.
/// With no execution-order guarantee between the two jobs, an auto-renewal contract could be
/// incorrectly deactivated if this job happened to run first. Fixed by excluding IsAutoRenewal
/// contracts here — ContractExpiryJob owns those exclusively.
/// </summary>
public abstract class ContractStatusUpdateJobTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ExecuteAsync_AutoRenewalContract_NotDeactivated()
    {
        Guid companyId = default, contractId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var contractRepository = GetRequiredService<IRepository<Contract, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Contract Status Test Co 1"), autoSave: true);
            var contract = new Contract(Guid.NewGuid(), company.Id, "CT-AUTO-1", "Customer", Guid.NewGuid(), DateTime.Today.AddYears(-1))
            {
                EndDate = DateTime.Today.AddDays(-5), // already expired
                IsAutoRenewal = true,
            };
            contract.Sign(DateTime.Today.AddYears(-1));
            await contractRepository.InsertAsync(contract, autoSave: true);

            companyId = company.Id;
            contractId = contract.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var job = GetRequiredService<ContractStatusUpdateJob>();
            await job.ExecuteAsync(new ContractStatusUpdateJobArgs { CompanyId = companyId, AsOfDate = DateTime.Today });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var contractRepository = GetRequiredService<IRepository<Contract, Guid>>();
            var contract = await contractRepository.GetAsync(contractId);
            // Still Active — left for ContractExpiryJob to renew, not deactivated here.
            contract.Status.ShouldBe(ContractStatus.Active);
        });
    }

    [Fact]
    public async Task ExecuteAsync_NonAutoRenewalExpiredContract_Deactivated()
    {
        Guid companyId = default, contractId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var contractRepository = GetRequiredService<IRepository<Contract, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Contract Status Test Co 2"), autoSave: true);
            var contract = new Contract(Guid.NewGuid(), company.Id, "CT-MANUAL-1", "Customer", Guid.NewGuid(), DateTime.Today.AddYears(-1))
            {
                EndDate = DateTime.Today.AddDays(-5),
                IsAutoRenewal = false,
            };
            contract.Sign(DateTime.Today.AddYears(-1));
            await contractRepository.InsertAsync(contract, autoSave: true);

            companyId = company.Id;
            contractId = contract.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var job = GetRequiredService<ContractStatusUpdateJob>();
            await job.ExecuteAsync(new ContractStatusUpdateJobArgs { CompanyId = companyId, AsOfDate = DateTime.Today });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var contractRepository = GetRequiredService<IRepository<Contract, Guid>>();
            var contract = await contractRepository.GetAsync(contractId);
            contract.Status.ShouldNotBe(ContractStatus.Active);
        });
    }
}
