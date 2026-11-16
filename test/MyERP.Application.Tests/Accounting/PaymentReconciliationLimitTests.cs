using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class PaymentReconciliationLimitTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetOutstandingInvoicesAsync_NegativeLimit_ThrowsValidationFailed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var svc = GetRequiredService<IPaymentReconciliationAppService>();
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                svc.GetOutstandingInvoicesAsync("Customer", Guid.NewGuid(), invoiceLimit: -1));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Invoice Limit");
        });
    }

    [Fact]
    public async Task GetUnreconciledPaymentsAsync_NegativeLimit_ThrowsValidationFailed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var svc = GetRequiredService<IPaymentReconciliationAppService>();
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                svc.GetUnreconciledPaymentsAsync("Customer", Guid.NewGuid(), paymentLimit: -5));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Payment Limit");
        });
    }
}
