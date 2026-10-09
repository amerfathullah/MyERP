using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.CRM.Entities;
using MyERP.EInvoice;
using MyERP.EInvoice.Services;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

public class UpstreamSyncPR59914_59928_60318Tests
{
    // =========================================================================
    // ERPNext PR #59914 / commit 69b028e3cb & dd78e6c0ed:
    // Positive appointment duration & refuse overlapping availability slots
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(-60)]
    public void AppointmentBookingSettings_SetAppointmentDurationMinutes_ThrowsWhenNonPositive(int duration)
    {
        var settings = new AppointmentBookingSettings(Guid.NewGuid(), Guid.NewGuid());

        var ex = Should.Throw<BusinessException>(() => settings.SetAppointmentDurationMinutes(duration));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("greater than 0 minutes");
    }

    [Fact]
    public void AppointmentBookingSettings_SetAppointmentDurationMinutes_SucceedsWhenPositive()
    {
        var settings = new AppointmentBookingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.SetAppointmentDurationMinutes(45);
        settings.AppointmentDurationMinutes.ShouldBe(45);
    }

    [Fact]
    public void AppointmentBookingSettings_ValidateAvailabilitySlots_ThrowsWhenOverlappingOnSameDay()
    {
        var settings = new AppointmentBookingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.AddAvailability(new AppointmentAvailability(
            Guid.NewGuid(), settings.Id, DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0)));
        settings.AddAvailability(new AppointmentAvailability(
            Guid.NewGuid(), settings.Id, DayOfWeek.Monday, new TimeSpan(11, 30, 0), new TimeSpan(14, 0, 0)));

        var ex = Should.Throw<BusinessException>(() => settings.ValidateAvailabilitySlots());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("cannot overlap");
    }

    [Fact]
    public void AppointmentBookingSettings_ValidateAvailabilitySlots_SucceedsWhenNonOverlappingOnSameDay()
    {
        var settings = new AppointmentBookingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.AddAvailability(new AppointmentAvailability(
            Guid.NewGuid(), settings.Id, DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0)));
        settings.AddAvailability(new AppointmentAvailability(
            Guid.NewGuid(), settings.Id, DayOfWeek.Monday, new TimeSpan(12, 30, 0), new TimeSpan(15, 30, 0)));

        Should.NotThrow(() => settings.ValidateAvailabilitySlots());
    }

    [Fact]
    public void AppointmentBookingSettings_ValidateAvailabilitySlots_SucceedsWhenSameTimeOnDifferentDays()
    {
        var settings = new AppointmentBookingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.AddAvailability(new AppointmentAvailability(
            Guid.NewGuid(), settings.Id, DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0)));
        settings.AddAvailability(new AppointmentAvailability(
            Guid.NewGuid(), settings.Id, DayOfWeek.Tuesday, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0)));

        Should.NotThrow(() => settings.ValidateAvailabilitySlots());
    }

    // =========================================================================
    // ERPNext PR #59928 / commit 7e4cabcd5c:
    // Keep value of assets disposed after reporting period
    // =========================================================================

    [Fact]
    public void Asset_IsDisposedBy_ReturnsFalseWhenActiveOrDisposedAfterDate()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Macbook Pro", new DateTime(2025, 1, 1), 100000m);
        asset.Submit();

        // Not disposed
        asset.IsDisposedBy(new DateTime(2025, 12, 31)).ShouldBeFalse();

        // Disposed after reporting period
        asset.Scrap(new DateTime(2026, 6, 30));
        asset.IsDisposedBy(new DateTime(2025, 12, 31)).ShouldBeFalse();
    }

    [Fact]
    public void Asset_IsDisposedBy_ReturnsTrueWhenDisposedOnOrBeforeDate()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Macbook Pro", new DateTime(2025, 1, 1), 100000m);
        asset.Submit();
        asset.Scrap(new DateTime(2026, 6, 30));

        asset.IsDisposedBy(new DateTime(2026, 6, 30)).ShouldBeTrue();
        asset.IsDisposedBy(new DateTime(2026, 12, 31)).ShouldBeTrue();
    }

    [Fact]
    public void Asset_GetAssetValueAsOf_RetainsValueWhenDisposedAfterPeriod()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Macbook Pro", new DateTime(2025, 1, 1), 100000m);
        asset.Submit();
        asset.Scrap(new DateTime(2026, 6, 30));

        // Prior to disposal date, asset still holds book value
        var value2025 = asset.GetAssetValueAsOf(new DateTime(2025, 12, 31), bookedDepreciationAmount: 20000m);
        value2025.ShouldBe(80000m);

        // After disposal date, asset value is 0
        var value2026 = asset.GetAssetValueAsOf(new DateTime(2026, 12, 31), bookedDepreciationAmount: 20000m);
        value2026.ShouldBe(0m);
    }

    // =========================================================================
    // ERPNext PR #60318 / commit 56d058f26c:
    // UOM Conversion Factor must be strictly greater than zero
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public void UomConversion_ValidateConversionFactor_ThrowsWhenZeroOrNegative(decimal factor)
    {
        var conv = new UomConversion(Guid.NewGuid(), "Box", "Unit", 1m)
        {
            ConversionFactor = factor
        };

        var ex = Should.Throw<BusinessException>(() => conv.ValidateConversionFactor());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("greater than zero");
    }

    [Fact]
    public void UomConversion_ValidateConversionFactor_SucceedsWhenPositive()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Box", "Unit", 12m);
        Should.NotThrow(() => conv.ValidateConversionFactor());
    }

    // =========================================================================
    // MyInvois commit 7552df2:
    // Distinguish idType error from generic taxpayer error in LhdnApiClient
    // =========================================================================

    [Fact]
    public async Task LhdnApiClient_SearchTaxpayer_ReturnsDetailedMessageForIdType()
    {
        var fakeHandler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"invalid id\"}")
        });
        var httpClient = new HttpClient(fakeHandler);
        var factory = new FakeHttpClientFactory(httpClient);
        var client = new LhdnApiClient(factory, NullLogger<LhdnApiClient>.Instance);

        var response = await client.SearchTaxpayerAsync("token", "BRN", "12345", LhdnEnvironment.Sandbox);

        response.IsFound.ShouldBeFalse();
        (response.ErrorMessage ?? string.Empty).ShouldContain("either idType or idValue is wrong");
    }

    private class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public FakeHttpMessageHandler(HttpResponseMessage response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }
}
