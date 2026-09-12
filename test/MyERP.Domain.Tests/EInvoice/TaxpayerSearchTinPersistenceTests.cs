using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MyERP.EInvoice;
using MyERP.EInvoice.Services;
using NSubstitute;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests.EInvoice;

/// <summary>
/// Unit tests for LHDN Taxpayer TIN lookup, response parsing (Gotcha #3957),
/// and taxpayer validation service behavior.
/// </summary>
public class TaxpayerSearchTinPersistenceTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private class MockHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public MockHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    [Fact]
    public async Task SearchTaxpayerAsync_ParsesRootTinAndName()
    {
        var jsonResponse = "{\"tin\":\"C1234567890\",\"name\":\"ACME MALAYSIA SDN BHD\"}";
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler);
        var factory = new MockHttpClientFactory(client);
        var apiClient = new LhdnApiClient(factory, NullLogger<LhdnApiClient>.Instance);

        var response = await apiClient.SearchTaxpayerAsync("test-token", "BRN", "202001001234", LhdnEnvironment.Sandbox);

        Assert.True(response.IsFound);
        Assert.Equal("C1234567890", response.Tin);
        Assert.Equal("ACME MALAYSIA SDN BHD", response.TaxpayerName);
    }

    [Fact]
    public async Task SearchTaxpayerAsync_ParsesNestedDataTinAndName_Gotcha3957()
    {
        // Per Gotcha #3957: LHDN response may wrap attributes in a 'data' object
        var jsonResponse = "{\"data\":{\"tin\":\"C9876543210\",\"name\":\"NESTED CORP BHD\"}}";
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler);
        var factory = new MockHttpClientFactory(client);
        var apiClient = new LhdnApiClient(factory, NullLogger<LhdnApiClient>.Instance);

        var response = await apiClient.SearchTaxpayerAsync("test-token", "BRN", "202001009999", LhdnEnvironment.Sandbox);

        Assert.True(response.IsFound);
        Assert.Equal("C9876543210", response.Tin);
        Assert.Equal("NESTED CORP BHD", response.TaxpayerName);
    }

    [Fact]
    public async Task SearchTaxpayerAsync_UsesTaxpayerName_WhenIdTypeAndValueAreEmpty()
    {
        Uri? requestedUri = null;
        var jsonResponse = "{\"tin\":\"C5555555555\",\"name\":\"QUERY BY NAME SDN BHD\"}";
        var handler = new MockHttpMessageHandler(req =>
        {
            requestedUri = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };
        });
        var client = new HttpClient(handler);
        var factory = new MockHttpClientFactory(client);
        var apiClient = new LhdnApiClient(factory, NullLogger<LhdnApiClient>.Instance);

        var response = await apiClient.SearchTaxpayerAsync("test-token", null, null, LhdnEnvironment.Sandbox, "QUERY BY NAME SDN BHD");

        Assert.True(response.IsFound);
        Assert.NotNull(requestedUri);
        Assert.Contains("taxpayerName=QUERY%20BY%20NAME%20SDN%20BHD", requestedUri.Query);
        Assert.Equal("C5555555555", response.Tin);
    }

    [Fact]
    public async Task SearchTaxpayerAsync_ReturnsError_WhenNeitherIdNorNameProvided()
    {
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = new HttpClient(handler);
        var factory = new MockHttpClientFactory(client);
        var apiClient = new LhdnApiClient(factory, NullLogger<LhdnApiClient>.Instance);

        var response = await apiClient.SearchTaxpayerAsync("test-token", null, null, LhdnEnvironment.Sandbox, null);

        Assert.False(response.IsFound);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("either ID Type and Value or Taxpayer Name must be present", response.ErrorMessage);
    }

    [Fact]
    public async Task TaxpayerValidationService_ForwardsParametersToApiClient()
    {
        var mockApiClient = Substitute.For<ILhdnApiClient>();
        var mockSettings = Substitute.For<ISettingProvider>();
        mockSettings.GetOrNullAsync("EInvoice.AccessToken").Returns("access-token-xyz");
        mockSettings.GetOrNullAsync("EInvoice.Environment").Returns("Sandbox");

        mockApiClient.SearchTaxpayerAsync("access-token-xyz", "BRN", "12345", LhdnEnvironment.Sandbox, "ACME")
            .Returns(new LhdnTaxpayerSearchResponse
            {
                IsFound = true,
                StatusCode = 200,
                Tin = "C1122334455",
                TaxpayerName = "ACME"
            });

        var validationService = new TaxpayerValidationService(mockApiClient, mockSettings);
        var result = await validationService.ValidateTaxpayerAsync("BRN", "12345", "ACME");

        Assert.True(result.IsFound);
        Assert.Equal("C1122334455", result.Tin);
        await mockApiClient.Received(1).SearchTaxpayerAsync("access-token-xyz", "BRN", "12345", LhdnEnvironment.Sandbox, "ACME");
    }
}
