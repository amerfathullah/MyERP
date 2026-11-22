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

    [Theory]
    [InlineData(401)]
    [InlineData(500)]
    public async Task EInvoiceService_CancelAsync_RefreshesTokenAndRetriesOnce_On401Or500(int failStatus)
    {
        var apiClient = Substitute.For<ILhdnApiClient>();
        var submissionRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.EInvoiceSubmission, System.Guid>>();
        var successLogRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.LhdnSuccessLog, System.Guid>>();
        var subId = System.Guid.NewGuid();
        var submission = new MyERP.EInvoice.Entities.EInvoiceSubmission(
            subId, System.Guid.NewGuid(), "SalesInvoice", System.Guid.NewGuid());
        submission.MarkAccepted("SUB-UID-1", "DOC-UUID-1", "LONG-1", null, "{}");

        submissionRepo.GetAsync(subId).Returns(submission);
        apiClient.CancelDocumentAsync("expired-token", "DOC-UUID-1", "Mistake", LhdnEnvironment.Sandbox)
            .Returns(new LhdnCancelResponse { IsSuccess = false, StatusCode = failStatus, ErrorMessage = "Expired" });
        apiClient.CancelDocumentAsync("refreshed-token", "DOC-UUID-1", "Mistake", LhdnEnvironment.Sandbox)
            .Returns(new LhdnCancelResponse { IsSuccess = true, StatusCode = 200 });

        var svc = new EInvoiceService(apiClient, submissionRepo, successLogRepo);
        var refreshCalls = 0;
        var cancelled = await svc.CancelAsync(
            subId, "Mistake", "expired-token", LhdnEnvironment.Sandbox,
            refreshTokenAsync: () => { refreshCalls++; return Task.FromResult<string?>("refreshed-token"); });

        cancelled.Status.ShouldBe("Cancelled");
        refreshCalls.ShouldBe(1);
        await apiClient.Received(1).CancelDocumentAsync("refreshed-token", "DOC-UUID-1", "Mistake", LhdnEnvironment.Sandbox);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(500)]
    public async Task EInvoiceService_SubmitAsync_RefreshesTokenAndRetriesOnce_On401Or500(int failStatus)
    {
        var apiClient = Substitute.For<ILhdnApiClient>();
        var submissionRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.EInvoiceSubmission, System.Guid>>();
        var successLogRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.LhdnSuccessLog, System.Guid>>();

        apiClient.SubmitDocumentAsync("expired-token", "<Invoice/>", LhdnEnvironment.Sandbox)
            .Returns(new LhdnSubmissionResponse { IsSuccess = false, StatusCode = failStatus, ErrorMessage = "Expired" });
        apiClient.SubmitDocumentAsync("fresh-token", "<Invoice/>", LhdnEnvironment.Sandbox)
            .Returns(new LhdnSubmissionResponse
            {
                IsSuccess = true,
                StatusCode = 202,
                SubmissionUid = "SUB-1",
                DocumentUuid = "UUID-1",
                LongId = "LONG-1",
                RawJson = "{}"
            });

        var svc = new EInvoiceService(apiClient, submissionRepo, successLogRepo);
        var refreshCalls = 0;
        var sub = await svc.SubmitAsync(
            System.Guid.NewGuid(), "SalesInvoice", System.Guid.NewGuid(), "<Invoice/>",
            "expired-token", LhdnEnvironment.Sandbox,
            refreshTokenAsync: () => { refreshCalls++; return Task.FromResult<string?>("fresh-token"); });

        sub.Status.ShouldBe("Valid");
        sub.DocumentUuid.ShouldBe("UUID-1");
        sub.QrCodeUrl.ShouldBe("https://preprod.myinvois.hasil.gov.my/UUID-1/share/LONG-1");
        refreshCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(500)]
    public async Task EInvoiceService_RefreshStatusAsync_RetriesOn401Or500_AndBuildsQrUrl(int failStatus)
    {
        var apiClient = Substitute.For<ILhdnApiClient>();
        var submissionRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.EInvoiceSubmission, System.Guid>>();
        var successLogRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.EInvoice.Entities.LhdnSuccessLog, System.Guid>>();
        var subId = System.Guid.NewGuid();
        var submission = new MyERP.EInvoice.Entities.EInvoiceSubmission(
            subId, System.Guid.NewGuid(), "SalesInvoice", System.Guid.NewGuid())
        {
            SubmissionUid = "SUB-UID-99",
            Status = "Pending"
        };

        submissionRepo.GetAsync(subId).Returns(submission);
        apiClient.GetDocumentStatusAsync("expired-token", "SUB-UID-99", LhdnEnvironment.Production)
            .Returns(new LhdnStatusResponse { IsSuccess = false, StatusCode = failStatus, Status = "Unknown", ErrorMessage = "Unauthorized" });
        apiClient.GetDocumentStatusAsync("fresh-token", "SUB-UID-99", LhdnEnvironment.Production)
            .Returns(new LhdnStatusResponse
            {
                IsSuccess = true,
                StatusCode = 200,
                Status = "Valid",
                DocumentUuid = "UUID-99",
                LongId = "LONG-99"
            });

        var svc = new EInvoiceService(apiClient, submissionRepo, successLogRepo);
        var updated = await svc.RefreshStatusAsync(
            subId, "expired-token", LhdnEnvironment.Production,
            refreshTokenAsync: () => Task.FromResult<string?>("fresh-token"));

        updated.Status.ShouldBe("Valid");
        updated.ValidatedAt.ShouldNotBeNull();
        updated.QrCodeUrl.ShouldBe("https://myinvois.hasil.gov.my/UUID-99/share/LONG-99");
    }

    [Fact]
    public async Task LhdnApiClient_SubmitDocumentAsync_MarksRejected202PayloadAsFailure()
    {
        var handler = new StubHttpMessageHandler(System.Net.HttpStatusCode.Accepted,
            """{"submissionUid":"SUB-REJ","acceptedDocuments":[],"rejectedDocuments":[{"invoiceCodeNumber":"INV-1","error":{"code":"CF321","message":"Invalid TIN"}}]}""");
        var httpClient = new System.Net.Http.HttpClient(handler);
        var factory = Substitute.For<System.Net.Http.IHttpClientFactory>();
        factory.CreateClient("LhdnApi").Returns(httpClient);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<LhdnApiClient>>();

        var client = new LhdnApiClient(factory, logger);
        var res = await client.SubmitDocumentAsync("tok", "<Invoice/>", LhdnEnvironment.Sandbox);

        res.IsSuccess.ShouldBeFalse();
        res.StatusCode.ShouldBe(202);
        res.ErrorMessage.ShouldBe("Invalid TIN");
    }

    private sealed class StubHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly System.Net.HttpStatusCode _status;
        private readonly string _body;
        public StubHttpMessageHandler(System.Net.HttpStatusCode status, string body) { _status = status; _body = body; }
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            => Task.FromResult(new System.Net.Http.HttpResponseMessage(_status)
            {
                Content = new System.Net.Http.StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
