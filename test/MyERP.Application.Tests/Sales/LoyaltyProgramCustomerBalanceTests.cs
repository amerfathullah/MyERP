using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Regression coverage for LoyaltyProgramAppService's customer-facing methods
/// (GetCustomerBalanceAsync/GetPointHistoryAsync/RedeemPointsAsync): the Loyalty Program pages only
/// ever showed program configuration (conversion factor, tiers) — there was no way to look up an
/// individual customer's balance, history, or redeem points, despite the backend (tier determination,
/// FIFO-ish available-points calculation, redemption) being fully implemented with zero test coverage
/// anywhere. Added a customer balance lookup panel to the program detail page; this test covers the
/// AppService layer.
/// </summary>
public abstract class LoyaltyProgramCustomerBalanceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetCustomerBalanceAsync_ReflectsEarnedPoints_AndRedeemPointsAsync_DeductsThem()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var programRepository = GetRequiredService<IRepository<LoyaltyProgram, Guid>>();
            var entryRepository = GetRequiredService<IRepository<LoyaltyPointEntry, Guid>>();
            var loyaltyProgramAppService = GetRequiredService<ILoyaltyProgramAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Loyalty Balance Test Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Loyalty Balance Test Customer"), autoSave: true);

            var program = new LoyaltyProgram(Guid.NewGuid(), company.Id, "Test Loyalty Program", conversionFactor: 10m);
            program.AddTier("Bronze", minSpent: 0, collectionFactor: 1m, redemptionFactor: 0.01m);
            program.AddTier("Silver", minSpent: 1000, collectionFactor: 2m, redemptionFactor: 0.02m);
            await programRepository.InsertAsync(program, autoSave: true);

            // 50 earned points (Bronze tier: totalSpent from earned points × conversionFactor = 500 < 1000 Silver threshold).
            await entryRepository.InsertAsync(
                new LoyaltyPointEntry(Guid.NewGuid(), company.Id, customer.Id, program.Id, points: 50, postingDate: DateTime.UtcNow)
                {
                    TierName = "Bronze",
                },
                autoSave: true);

            var balance = await loyaltyProgramAppService.GetCustomerBalanceAsync(customer.Id, program.Id);
            balance.AvailablePoints.ShouldBe(50);
            balance.CurrentTier.ShouldBe("Bronze");
            balance.RedemptionValue.ShouldBe(0.5m);

            var history = await loyaltyProgramAppService.GetPointHistoryAsync(customer.Id, program.Id);
            history.Count.ShouldBe(1);
            history[0].Points.ShouldBe(50);
            history[0].IsEarning.ShouldBeTrue();

            var redemptionValue = await loyaltyProgramAppService.RedeemPointsAsync(customer.Id, program.Id, 20, company.Id);
            redemptionValue.ShouldBe(0.2m);

            var balanceAfterRedeem = await loyaltyProgramAppService.GetCustomerBalanceAsync(customer.Id, program.Id);
            balanceAfterRedeem.AvailablePoints.ShouldBe(30);
        });
    }

    [Fact]
    public async Task RedeemPointsAsync_MoreThanAvailable_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var programRepository = GetRequiredService<IRepository<LoyaltyProgram, Guid>>();
            var loyaltyProgramAppService = GetRequiredService<ILoyaltyProgramAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Loyalty Balance Test Co 2"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Loyalty Balance Test Customer 2"), autoSave: true);

            var program = new LoyaltyProgram(Guid.NewGuid(), company.Id, "Test Loyalty Program 2", conversionFactor: 10m);
            program.AddTier("Bronze", minSpent: 0, collectionFactor: 1m, redemptionFactor: 0.01m);
            await programRepository.InsertAsync(program, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                loyaltyProgramAppService.RedeemPointsAsync(customer.Id, program.Id, 100, company.Id));
        });
    }
}
