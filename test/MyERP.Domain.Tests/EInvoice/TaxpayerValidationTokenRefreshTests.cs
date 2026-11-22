using System.Threading.Tasks;
using MyERP.EInvoice;
using MyERP.EInvoice.Services;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.EInvoice;

/// <summary>
/// Verifies MyInvois search_taxpayer.py / taxpayerlogin.py token auto-fetch and 401/500 retry semantics.
/// </summary>
public class TaxpayerValidationTokenRefreshTests
{
    [Fact]
    public async Task ValidateTaxpayerAsync_AutoFetchesToken_WhenAccessTokenIsMissing()
    {
        var apiClient = Substitute.For<ILhdnApiClient>();
        var settings = Substitute.For<ISettingProvider>();

        settings.GetOrNullAsync("EInvoice.Environment").Returns("Sandbox");
        settings.GetOrNullAsync("EInvoice.AccessToken").Returns((string?)null);
        settings.GetOrNullAsync("EInvoice.ClientId").Returns("cid");
        settings.GetOrNullAsync("EInvoice.ClientSecret").Returns("csecret");

        apiClient.GetAccessTokenAsync("cid", "csecret", LhdnEnvironment.Sandbox).Returns("fresh-token");
        apiClient.SearchTaxpayerAsync("fresh-token", "BRN", "202001012345", LhdnEnvironment.Sandbox, null)
            .Returns(new LhdnTaxpayerSearchResponse { IsFound = true, StatusCode = 200, Tin = "C1234567890" });

        var svc = new TaxpayerValidationService(apiClient, settings);
        var result = await svc.ValidateTaxpayerAsync("BRN", "202001012345");

        result.IsFound.ShouldBeTrue();
        result.Tin.ShouldBe("C1234567890");
        await apiClient.Received(1).GetAccessTokenAsync("cid", "csecret", LhdnEnvironment.Sandbox);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(500)]
    public async Task ValidateTaxpayerAsync_RefreshesTokenAndRetriesOnce_On401Or500(int failStatus)
    {
        var apiClient = Substitute.For<ILhdnApiClient>();
        var settings = Substitute.For<ISettingProvider>();

        settings.GetOrNullAsync("EInvoice.Environment").Returns("Production");
        settings.GetOrNullAsync("EInvoice.AccessToken").Returns("expired-token");
        settings.GetOrNullAsync("EInvoice.ClientId").Returns("cid");
        settings.GetOrNullAsync("EInvoice.ClientSecret").Returns("csecret");

        apiClient.SearchTaxpayerAsync("expired-token", "BRN", "202001012345", LhdnEnvironment.Production, null)
            .Returns(new LhdnTaxpayerSearchResponse { IsFound = false, StatusCode = failStatus, ErrorMessage = "Unauthorized" });
        apiClient.GetAccessTokenAsync("cid", "csecret", LhdnEnvironment.Production).Returns("new-token");
        apiClient.SearchTaxpayerAsync("new-token", "BRN", "202001012345", LhdnEnvironment.Production, null)
            .Returns(new LhdnTaxpayerSearchResponse { IsFound = true, StatusCode = 200, Tin = "C9999999999" });

        var svc = new TaxpayerValidationService(apiClient, settings);
        var result = await svc.ValidateTaxpayerAsync("BRN", "202001012345");

        result.IsFound.ShouldBeTrue();
        result.Tin.ShouldBe("C9999999999");
        await apiClient.Received(1).GetAccessTokenAsync("cid", "csecret", LhdnEnvironment.Production);
    }

    [Fact]
    public async Task ValidateTaxpayerAsync_ThrowsUserFriendly_WhenNoTokenAndNoClientCredentials()
    {
        var apiClient = Substitute.For<ILhdnApiClient>();
        var settings = Substitute.For<ISettingProvider>();

        settings.GetOrNullAsync("EInvoice.Environment").Returns("Sandbox");
        settings.GetOrNullAsync("EInvoice.AccessToken").Returns((string?)null);
        settings.GetOrNullAsync("EInvoice.ClientId").Returns((string?)null);
        settings.GetOrNullAsync("EInvoice.ClientSecret").Returns((string?)null);

        var svc = new TaxpayerValidationService(apiClient, settings);
        await Should.ThrowAsync<UserFriendlyException>(() => svc.ValidateTaxpayerAsync("BRN", "202001012345"));
    }
}
