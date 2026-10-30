using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class CouponCodeAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_AvoidsPromotionalCodeCollisions_AndHandlesDigitOnlyName()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var ruleRepo = GetRequiredService<IRepository<PricingRule, Guid>>();
            var couponSvc = GetRequiredService<ICouponCodeAppService>();

            var rule = new PricingRule(Guid.NewGuid(), "Promo Rule", PricingRuleApplyOn.TransactionTotal, PricingRuleType.Discount)
            {
                DiscountPercentage = 10m
            };
            await ruleRepo.InsertAsync(rule, autoSave: true);

            var first = await couponSvc.CreateAsync(new CreateCouponCodeDto
            {
                CouponName = "Summer Savings 2026",
                CouponType = CouponType.Promotional,
                PricingRuleId = rule.Id,
            });
            first.Code.ShouldBe("SUMMERSA");

            // Same 8-char non-digit prefix ("SUMMERSA") must receive a 6-char hex suffix instead of colliding (ERPNext commit 8fa3f79311)
            var second = await couponSvc.CreateAsync(new CreateCouponCodeDto
            {
                CouponName = "Summer Savings 2027",
                CouponType = CouponType.Promotional,
                PricingRuleId = rule.Id,
            });
            second.Code.ShouldNotBe("SUMMERSA");
            second.Code.ShouldStartWith("SUMMERSA");
            second.Code.Length.ShouldBe(14);

            // Digit-only name yields empty prefix -> 6-char uppercase hex code
            var digitsOnly = await couponSvc.CreateAsync(new CreateCouponCodeDto
            {
                CouponName = "2026 2027",
                CouponType = CouponType.Promotional,
                PricingRuleId = rule.Id,
            });
            digitsOnly.Code.Length.ShouldBe(6);
        });
    }

    [Fact]
    public async Task CreateAsync_RejectsDisabledPricingRule_AndGiftCardWithoutCustomer()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<PricingRule, Guid>>();
            var couponSvc = GetRequiredService<ICouponCodeAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Coupon Guard Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Coupon Guard Cust"), autoSave: true);

            var disabledRule = new PricingRule(Guid.NewGuid(), "Disabled Rule", PricingRuleApplyOn.TransactionTotal, PricingRuleType.Discount)
            {
                DiscountPercentage = 5m,
                IsDisabled = true
            };
            var activeRule = new PricingRule(Guid.NewGuid(), "Active Rule", PricingRuleApplyOn.TransactionTotal, PricingRuleType.Discount)
            {
                DiscountPercentage = 5m,
                IsDisabled = false
            };
            await ruleRepo.InsertAsync(disabledRule, autoSave: true);
            await ruleRepo.InsertAsync(activeRule, autoSave: true);

            // ERPNext commit 1db5df0d6d: disabled Pricing Rule rejected
            var disabledEx = await Should.ThrowAsync<BusinessException>(() =>
                couponSvc.CreateAsync(new CreateCouponCodeDto
                {
                    CouponName = "Disabled Rule Promo",
                    CouponType = CouponType.Promotional,
                    PricingRuleId = disabledRule.Id,
                }));
            disabledEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Gift Card without CustomerId rejected
            var missingCustEx = await Should.ThrowAsync<BusinessException>(() =>
                couponSvc.CreateAsync(new CreateCouponCodeDto
                {
                    CouponName = "Gift Card No Customer",
                    CouponType = CouponType.GiftCard,
                    PricingRuleId = activeRule.Id,
                }));
            missingCustEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Gift Card with CustomerId succeeds and forces MaximumUse = 1 even if caller passes 50
            var giftCard = await couponSvc.CreateAsync(new CreateCouponCodeDto
            {
                CouponName = "Gift Card Valid",
                CouponType = CouponType.GiftCard,
                PricingRuleId = activeRule.Id,
                CustomerId = customer.Id,
                MaximumUse = 50,
            });
            giftCard.MaximumUse.ShouldBe(1);
            giftCard.CustomerId.ShouldBe(customer.Id);
        });
    }
}
