using System;
using MyERP.EInvoice;
using MyERP.EInvoice.Entities;
using MyERP.EInvoice.Services;
using Xunit;

namespace MyERP.Domain.Tests.EInvoice;

/// <summary>
/// Tests for E-Invoice entity lifecycle, DTOs, and settings validation.
/// </summary>
public class EInvoiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    #region EInvoiceSubmission Entity

    [Fact]
    public void Submission_DefaultStatus_IsPending()
    {
        var sub = new EInvoiceSubmission(Guid.NewGuid(), CompanyId, "SalesInvoice", Guid.NewGuid());
        Assert.Equal("Pending", sub.Status);
        Assert.Null(sub.SubmittedAt);
        Assert.Null(sub.DocumentUuid);
    }

    [Fact]
    public void Submission_MarkAccepted_SetsAllFields()
    {
        var sub = new EInvoiceSubmission(Guid.NewGuid(), CompanyId, "SalesInvoice", Guid.NewGuid());
        
        sub.MarkAccepted("SUB-123", "UUID-456", "LONG-789", "https://qr.example.com", "{\"ok\":true}");
        
        Assert.Equal("Valid", sub.Status);
        Assert.Equal("SUB-123", sub.SubmissionUid);
        Assert.Equal("UUID-456", sub.DocumentUuid);
        Assert.Equal("LONG-789", sub.LongId);
        Assert.Equal("https://qr.example.com", sub.QrCodeUrl);
        Assert.NotNull(sub.SubmittedAt);
        Assert.NotNull(sub.ValidatedAt);
    }

    [Fact]
    public void Submission_MarkRejected_SetsReasonAndStatus()
    {
        var sub = new EInvoiceSubmission(Guid.NewGuid(), CompanyId, "SalesInvoice", Guid.NewGuid());
        
        sub.MarkRejected("Invalid TIN format", "{\"error\":\"TIN\"}");
        
        Assert.Equal("Invalid", sub.Status);
        Assert.Equal("Invalid TIN format", sub.Reason);
        Assert.Equal("{\"error\":\"TIN\"}", sub.RawResponse);
    }

    [Fact]
    public void Submission_MarkCancelled_SetsStatusAndTimestamp()
    {
        var sub = new EInvoiceSubmission(Guid.NewGuid(), CompanyId, "SalesInvoice", Guid.NewGuid());
        sub.MarkAccepted("SUB-1", "UUID-1", null, null, null);
        
        sub.MarkCancelled("Customer requested cancellation");
        
        Assert.Equal("Cancelled", sub.Status);
        Assert.Equal("Customer requested cancellation", sub.Reason);
        Assert.NotNull(sub.CancelledAt);
    }

    [Fact]
    public void Submission_SupportsPurchaseInvoice()
    {
        var sub = new EInvoiceSubmission(Guid.NewGuid(), CompanyId, "PurchaseInvoice", Guid.NewGuid());
        Assert.Equal("PurchaseInvoice", sub.SourceDocumentType);
    }

    [Fact]
    public void Submission_DefaultDocumentTypeCode_Is01()
    {
        var sub = new EInvoiceSubmission(Guid.NewGuid(), CompanyId, "SalesInvoice", Guid.NewGuid());
        Assert.Equal("01", sub.DocumentTypeCode);
    }

    [Fact]
    public void Submission_CompanyAndTenant_SetCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var sub = new EInvoiceSubmission(Guid.NewGuid(), CompanyId, "SalesInvoice", docId, tenantId);
        
        Assert.Equal(CompanyId, sub.CompanyId);
        Assert.Equal(tenantId, sub.TenantId);
        Assert.Equal(docId, sub.SourceDocumentId);
    }

    #endregion

    #region Settings DTOs Validation

    [Fact]
    public void ConnectionStatus_NotConfigured_Defaults()
    {
        var dto = new EInvoiceConnectionStatusDto();
        Assert.False(dto.IsConfigured);
        Assert.False(dto.IsConnected);
        Assert.False(dto.IsTokenExpired);
        Assert.Equal("Sandbox", dto.Environment);
        Assert.Null(dto.ClientId);
        Assert.Null(dto.TokenExpiresAt);
        Assert.False(dto.IsCertificateConfigured);
    }

    [Fact]
    public void ConnectionStatus_FullyConfigured()
    {
        var dto = new EInvoiceConnectionStatusDto
        {
            IsConfigured = true,
            IsConnected = true,
            IsTokenExpired = false,
            Environment = "Production",
            ClientId = "MY-CLIENT-001",
            TokenExpiresAt = DateTime.UtcNow.AddMinutes(30),
            IsCertificateConfigured = true,
        };
        
        Assert.True(dto.IsConfigured);
        Assert.True(dto.IsConnected);
        Assert.False(dto.IsTokenExpired);
        Assert.Equal("Production", dto.Environment);
    }

    [Fact]
    public void ConnectResult_Success()
    {
        var dto = new EInvoiceConnectResultDto
        {
            IsSuccess = true,
            TokenExpiresAt = DateTime.UtcNow.AddMinutes(55),
        };
        
        Assert.True(dto.IsSuccess);
        Assert.Null(dto.ErrorMessage);
        Assert.NotNull(dto.TokenExpiresAt);
    }

    [Fact]
    public void ConnectResult_Failure()
    {
        var dto = new EInvoiceConnectResultDto
        {
            IsSuccess = false,
            ErrorMessage = "Invalid client credentials",
        };
        
        Assert.False(dto.IsSuccess);
        Assert.Equal("Invalid client credentials", dto.ErrorMessage);
        Assert.Null(dto.TokenExpiresAt);
    }

    [Fact]
    public void TaxpayerSearchResult_Found()
    {
        var dto = new TaxpayerSearchResultDto
        {
            IsSuccess = true,
            Tin = "C12345678901",
            Name = "Acme Sdn Bhd",
            IdType = "BRN",
            IdValue = "202201234567",
        };
        
        Assert.True(dto.IsSuccess);
        Assert.Equal("C12345678901", dto.Tin);
        Assert.Equal("Acme Sdn Bhd", dto.Name);
    }

    [Fact]
    public void TaxpayerSearchResult_NotFound()
    {
        var dto = new TaxpayerSearchResultDto
        {
            IsSuccess = false,
            ErrorMessage = "Taxpayer not found",
        };
        
        Assert.False(dto.IsSuccess);
        Assert.Null(dto.Tin);
    }

    #endregion

    #region LHDN Response Models

    [Fact]
    public void LhdnSubmissionResponse_Success()
    {
        var response = new LhdnSubmissionResponse
        {
            IsSuccess = true,
            SubmissionUid = "SUB-20260720-001",
            DocumentUuid = "UUID-ABCDEF",
            LongId = "LONG-123456",
            QrCodeUrl = "https://myinvois.hasil.gov.my/qr/UUID-ABCDEF",
        };
        
        Assert.True(response.IsSuccess);
        Assert.NotNull(response.SubmissionUid);
        Assert.NotNull(response.DocumentUuid);
    }

    [Fact]
    public void LhdnSubmissionResponse_Failure()
    {
        var response = new LhdnSubmissionResponse
        {
            IsSuccess = false,
            ErrorMessage = "Document validation failed: TIN mismatch",
        };
        
        Assert.False(response.IsSuccess);
        Assert.Null(response.DocumentUuid);
        Assert.NotNull(response.ErrorMessage);
    }

    [Fact]
    public void LhdnStatusResponse_ValidStatus()
    {
        var response = new LhdnStatusResponse
        {
            Status = "Valid",
            DocumentUuid = "UUID-001",
            LongId = "LONG-001",
        };
        
        Assert.Equal("Valid", response.Status);
    }

    [Fact]
    public void LhdnCancelResponse_Success()
    {
        var response = new LhdnCancelResponse { IsSuccess = true };
        Assert.True(response.IsSuccess);
        Assert.Null(response.ErrorMessage);
    }

    [Fact]
    public void LhdnTaxpayerSearchResponse_Found()
    {
        var response = new LhdnTaxpayerSearchResponse
        {
            IsFound = true,
            Tin = "C99887766554",
            TaxpayerName = "Widget Corp Sdn Bhd",
        };
        
        Assert.True(response.IsFound);
        Assert.Equal("C99887766554", response.Tin);
    }

    [Fact]
    public void LhdnTaxpayerSearchResponse_NotFound()
    {
        var response = new LhdnTaxpayerSearchResponse { IsFound = false };
        Assert.False(response.IsFound);
        Assert.Null(response.Tin);
    }

    #endregion

    #region Document Type Code Mapping

    [Theory]
    [InlineData("01", "Standard invoice")]
    [InlineData("02", "Credit note")]
    [InlineData("03", "Debit note")]
    [InlineData("11", "Self-billed invoice")]
    [InlineData("12", "Self-billed credit note")]
    [InlineData("13", "Self-billed debit note")]
    public void DocumentTypeCode_ValidCodes(string code, string _)
    {
        var sub = new EInvoiceSubmission(Guid.NewGuid(), CompanyId, "SalesInvoice", Guid.NewGuid());
        sub.DocumentTypeCode = code;
        Assert.Equal(code, sub.DocumentTypeCode);
    }

    #endregion

    #region LhdnEnvironment Enum

    [Fact]
    public void LhdnEnvironment_HasSandboxAndProduction()
    {
        Assert.True(Enum.IsDefined(typeof(LhdnEnvironment), "Sandbox"));
        Assert.True(Enum.IsDefined(typeof(LhdnEnvironment), "Production"));
    }

    [Fact]
    public void LhdnEnvironment_ParsesFromString()
    {
        var sandbox = Enum.Parse<LhdnEnvironment>("Sandbox");
        var production = Enum.Parse<LhdnEnvironment>("Production");
        Assert.NotEqual(sandbox, production);
    }

    #endregion
}
