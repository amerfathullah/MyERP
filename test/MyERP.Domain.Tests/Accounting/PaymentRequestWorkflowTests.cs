using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for Payment Request resend email, cancellation, payment link, and summary metrics.
/// Verifies rules from erpnext/accounts/doctype/payment_request (#6012).
/// </summary>
public class PaymentRequestWorkflowTests
{
    private readonly IRepository<PaymentRequest, Guid> _prRepo = Substitute.For<IRepository<PaymentRequest, Guid>>();
    private readonly PaymentRequestAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _partyId = Guid.NewGuid();
    private readonly Guid _referenceId = Guid.NewGuid();

    public PaymentRequestWorkflowTests()
    {
        _appService = new PaymentRequestAppService(_prRepo);
    }

    [Fact]
    public async Task ResendPaymentEmailAsync_InitiatedRequest_Succeeds()
    {
        var prId = Guid.NewGuid();
        var pr = new PaymentRequest(prId, _companyId, "SalesInvoice", _referenceId, _partyId, "Customer", 500m)
        {
            PartyName = "Global Tech Corp",
            EmailTo = "billing@globaltech.com"
        };
        pr.Submit();

        Assert.Equal(PaymentRequestStatus.Initiated, pr.Status);

        _prRepo.GetAsync(prId).Returns(Task.FromResult(pr));

        var result = await _appService.ResendPaymentEmailAsync(prId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("billing@globaltech.com", result.SentTo);
    }

    [Fact]
    public async Task ResendPaymentEmailAsync_DraftRequest_ThrowsValidationException()
    {
        var prId = Guid.NewGuid();
        var pr = new PaymentRequest(prId, _companyId, "SalesInvoice", _referenceId, _partyId, "Customer", 500m);

        _prRepo.GetAsync(prId).Returns(Task.FromResult(pr));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.ResendPaymentEmailAsync(prId));
        Assert.Equal(MyERPDomainErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAccurateSummaryAndActionFlags()
    {
        var prId = Guid.NewGuid();
        var pr = new PaymentRequest(prId, _companyId, "SalesInvoice", _referenceId, _partyId, "Customer", 1250m)
        {
            PartyName = "Apex Solutions",
            PaymentUrl = "https://gateway.example.com/pay/abc-123",
            PaymentGateway = "Stripe"
        };
        pr.Submit();

        _prRepo.GetAsync(prId).Returns(Task.FromResult(pr));

        var summary = await _appService.GetSummaryAsync(prId);

        Assert.NotNull(summary);
        Assert.Equal(prId, summary.Id);
        Assert.Equal(1250m, summary.GrandTotal);
        Assert.Equal(1250m, summary.OutstandingAmount);
        Assert.Equal("Initiated", summary.StatusName);
        Assert.Equal("https://gateway.example.com/pay/abc-123", summary.PaymentUrl);
        Assert.Equal("Stripe", summary.PaymentGateway);
        Assert.True(summary.CanPay);
        Assert.True(summary.CanResendEmail);
        Assert.True(summary.CanCancel);
    }

    [Fact]
    public void PaymentRequest_SubscriptionProperties_CanBeSetAndRead()
    {
        var prId = Guid.NewGuid();
        var subId = Guid.NewGuid();
        var pr = new PaymentRequest(prId, _companyId, "PurchaseInvoice", _referenceId, _partyId, "Supplier", 800m)
        {
            IsASubscription = true,
            SubscriptionId = subId
        };

        Assert.True(pr.IsASubscription);
        Assert.Equal(subId, pr.SubscriptionId);
        Assert.Equal("PurchaseInvoice", pr.ReferenceDoctype);
        Assert.Equal("Supplier", pr.PartyType);
    }

    [Fact]
    public async Task GetSubscriptionDetailsAsync_ReturnsEmpty_ForDoctypeWithoutSubscription()
    {
        var result = await _appService.GetSubscriptionDetailsAsync("SalesOrder", Guid.NewGuid());
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSubscriptionDetailsAsync_ReturnsPlans_ForSubscriptionReference()
    {
        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var authService = Substitute.For<Microsoft.AspNetCore.Authorization.IAuthorizationService, Volo.Abp.Authorization.IAbpAuthorizationService>();
        authService.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<string>())
            .Returns(Task.FromResult(Microsoft.AspNetCore.Authorization.AuthorizationResult.Success()));
        lazyProvider.LazyGetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>().Returns(authService);
        _appService.LazyServiceProvider = lazyProvider;

        var subId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var sub = new MyERP.Sales.Entities.Subscription(subId, _companyId, _partyId, "Customer", DateTime.UtcNow, "Monthly");
        sub.AddPlan(itemId, 2, 50m, "SaaS License");

        var subRepo = Substitute.For<IRepository<MyERP.Sales.Entities.Subscription, Guid>>();
        subRepo.FindAsync(subId).Returns(Task.FromResult<MyERP.Sales.Entities.Subscription?>(sub));
        lazyProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.Subscription, Guid>>().Returns(subRepo);

        var result = await _appService.GetSubscriptionDetailsAsync("Subscription", subId);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(2, result[0].Qty);
        Assert.Equal(50m, result[0].Rate);
        Assert.Equal(100m, result[0].Amount);
        Assert.Equal("SaaS License", result[0].ItemName);
    }
}
