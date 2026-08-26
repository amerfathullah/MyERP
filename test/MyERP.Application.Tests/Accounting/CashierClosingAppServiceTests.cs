using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class CashierClosingAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICashierClosingAppService _appService;

    protected CashierClosingAppServiceTests()
    {
        _appService = GetRequiredService<ICashierClosingAppService>();
    }

    [Fact]
    public async Task CashierClosing_Should_Create_Update_And_Submit()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var created = await _appService.CreateAsync(new CreateCashierClosingDto
            {
                Date = DateTime.UtcNow.Date,
                FromTime = new TimeSpan(8, 0, 0),
                ToTime = new TimeSpan(16, 0, 0),
                Custody = 100,
                Expense = 20,
                Returns = 10,
                Payments = new List<CreateUpdateCashierClosingPaymentDto>
                {
                    new() { ModeOfPayment = "Cash", Amount = 400 },
                    new() { ModeOfPayment = "Card", Amount = 250 }
                }
            });

            created.ShouldNotBeNull();
            created.ClosingNumber.ShouldStartWith("POS-CLO-");
            created.Payments.Count.ShouldBe(2);
            // net_amount = 650 + 0 + 20 - 100 + 10 = 580
            created.NetAmount.ShouldBe(580);
            created.IsSubmitted.ShouldBeFalse();

            var updated = await _appService.UpdateAsync(created.Id, new UpdateCashierClosingDto
            {
                Date = DateTime.UtcNow.Date,
                FromTime = new TimeSpan(8, 0, 0),
                ToTime = new TimeSpan(17, 0, 0),
                Custody = 100,
                Expense = 30,
                Returns = 10,
                Payments = new List<CreateUpdateCashierClosingPaymentDto>
                {
                    new() { ModeOfPayment = "Cash", Amount = 500 }
                }
            });

            // net_amount = 500 + 0 + 30 - 100 + 10 = 440
            updated.NetAmount.ShouldBe(440);

            var submitted = await _appService.SubmitAsync(created.Id);
            submitted.IsSubmitted.ShouldBeTrue();
        });
    }
}
