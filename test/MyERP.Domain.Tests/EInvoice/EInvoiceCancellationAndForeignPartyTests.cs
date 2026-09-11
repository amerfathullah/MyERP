using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.EInvoice;
using MyERP.EInvoice.Entities;
using MyERP.EInvoice.Services;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.EInvoice;

/// <summary>
/// Unit tests for LHDN MyInvois 72-hour cancellation window enforcement,
/// pre-cancellation state validation, and foreign customer/supplier handling.
/// Migrated from myinvois cancel_doc.py and PR d9adf36 / 7552df2.
/// </summary>
public class EInvoiceCancellationAndForeignPartyTests
{
    private readonly ILhdnApiClient _lhdnApiClient = Substitute.For<ILhdnApiClient>();
    private readonly IRepository<EInvoiceSubmission, Guid> _submissionRepository = Substitute.For<IRepository<EInvoiceSubmission, Guid>>();
    private readonly IRepository<LhdnSuccessLog, Guid> _successLogRepository = Substitute.For<IRepository<LhdnSuccessLog, Guid>>();
    private readonly EInvoiceService _eInvoiceService;
    private readonly InvoiceDocumentBuilder _xmlBuilder = new();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _submissionId = Guid.NewGuid();
    private readonly Guid _invoiceId = Guid.NewGuid();

    public EInvoiceCancellationAndForeignPartyTests()
    {
        _eInvoiceService = new EInvoiceService(_lhdnApiClient, _submissionRepository, _successLogRepository);
    }

    [Fact]
    public async Task CancelAsync_Within72Hours_Succeeds()
    {
        var submission = new EInvoiceSubmission(_submissionId, _companyId, "SalesInvoice", _invoiceId);
        submission.MarkAccepted("SUB-100", "UUID-100", "LONG-100", "https://qr.example.com", "{}");
        submission.SubmittedAt = DateTime.UtcNow.AddHours(-10); // 10 hours ago

        _submissionRepository.GetAsync(_submissionId).Returns(submission);
        _lhdnApiClient.CancelDocumentAsync("test-token", "UUID-100", "Customer returned goods", LhdnEnvironment.Sandbox)
            .Returns(new LhdnCancelResponse { IsSuccess = true });

        var result = await _eInvoiceService.CancelAsync(_submissionId, "Customer returned goods", "test-token", LhdnEnvironment.Sandbox);

        Assert.Equal("Cancelled", result.Status);
        Assert.Equal("Customer returned goods", result.Reason);
        Assert.NotNull(result.CancelledAt);
        await _submissionRepository.Received(1).UpdateAsync(submission);
    }

    [Fact]
    public async Task CancelAsync_Beyond72Hours_ThrowsLhdnCancellationWindowExpired()
    {
        var submission = new EInvoiceSubmission(_submissionId, _companyId, "SalesInvoice", _invoiceId);
        submission.MarkAccepted("SUB-100", "UUID-100", "LONG-100", "https://qr.example.com", "{}");
        submission.SubmittedAt = DateTime.UtcNow.AddHours(-73); // 73 hours ago (expired!)

        _submissionRepository.GetAsync(_submissionId).Returns(submission);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _eInvoiceService.CancelAsync(_submissionId, "Too late", "test-token", LhdnEnvironment.Sandbox));

        Assert.Equal(MyERPDomainErrorCodes.LhdnCancellationWindowExpired, ex.Code);
        Assert.Contains("72 hours", ex.Data["reason"]?.ToString());
        await _lhdnApiClient.DidNotReceiveWithAnyArgs().CancelDocumentAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task CancelAsync_WithoutSubmittedAt_ThrowsMissingSubmissionTime()
    {
        var submission = new EInvoiceSubmission(_submissionId, _companyId, "SalesInvoice", _invoiceId);
        submission.SubmissionUid = "SUB-100";
        submission.DocumentUuid = "UUID-100";
        submission.Status = "Valid";
        submission.SubmittedAt = null; // No timestamp

        _submissionRepository.GetAsync(_submissionId).Returns(submission);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _eInvoiceService.CancelAsync(_submissionId, "Missing time", "test-token", LhdnEnvironment.Sandbox));

        Assert.Equal(MyERPDomainErrorCodes.EInvoiceCancellationFailed, ex.Code);
        Assert.Contains("Submission time not found", ex.Data["reason"]?.ToString());
    }

    [Fact]
    public async Task CancelAsync_MissingUuidOrSubmissionUid_Throws()
    {
        var submission = new EInvoiceSubmission(_submissionId, _companyId, "SalesInvoice", _invoiceId);
        submission.SubmissionUid = null;
        submission.DocumentUuid = null;

        _submissionRepository.GetAsync(_submissionId).Returns(submission);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _eInvoiceService.CancelAsync(_submissionId, "Missing UUID", "test-token", LhdnEnvironment.Sandbox));

        Assert.Equal(MyERPDomainErrorCodes.EInvoiceCancellationFailed, ex.Code);
        Assert.Contains("Missing submission UID or UUID", ex.Data["reason"]?.ToString());
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_Throws()
    {
        var submission = new EInvoiceSubmission(_submissionId, _companyId, "SalesInvoice", _invoiceId);
        submission.MarkAccepted("SUB-100", "UUID-100", "LONG-100", null, null);
        submission.MarkCancelled("Previous cancel");

        _submissionRepository.GetAsync(_submissionId).Returns(submission);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _eInvoiceService.CancelAsync(_submissionId, "Cancel again", "test-token", LhdnEnvironment.Sandbox));

        Assert.Equal(MyERPDomainErrorCodes.EInvoiceCancellationFailed, ex.Code);
        Assert.Contains("already cancelled", ex.Data["reason"]?.ToString());
    }

    [Fact]
    public async Task CancelAsync_InvalidRejectedStatus_Throws()
    {
        var submission = new EInvoiceSubmission(_submissionId, _companyId, "SalesInvoice", _invoiceId);
        submission.SubmissionUid = "SUB-100";
        submission.DocumentUuid = "UUID-100";
        submission.SubmittedAt = DateTime.UtcNow;
        submission.MarkRejected("Invalid schema", "{}");

        _submissionRepository.GetAsync(_submissionId).Returns(submission);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _eInvoiceService.CancelAsync(_submissionId, "Cancel rejected", "test-token", LhdnEnvironment.Sandbox));

        Assert.Equal(MyERPDomainErrorCodes.EInvoiceCancellationFailed, ex.Code);
        Assert.Contains("Cannot cancel e-Invoice with status 'Invalid'", ex.Data["reason"]?.ToString());
    }

    [Fact]
    public void BuildXml_ForeignBuyer_IncludesForeignTinPassportAndContact()
    {
        var data = new EInvoiceDocumentData
        {
            InvoiceNumber = "INV-EXPORT-001",
            IssueDate = new DateTime(2026, 9, 1, 12, 0, 0),
            DocumentTypeCode = "01",
            CurrencyCode = "USD",
            Supplier = new EInvoicePartyData
            {
                Name = "Malaysian Exporter Sdn Bhd",
                Tin = "C12345678900",
                IdType = "BRN",
                IdValue = "202001012345",
                CountryCode = "MYS"
            },
            Buyer = new EInvoicePartyData
            {
                Name = "Singapore Importer Pte Ltd",
                Tin = EInvoiceConsts.ForeignPartyTin,
                IdType = "PASSPORT",
                IdValue = "E9876543A",
                CountryCode = "SGP",
                Phone = "+6561234567",
                Email = "finance@sgimporter.com",
                Address = "10 Marina Boulevard",
                City = "Singapore",
                PostalCode = "018983"
            },
            NetTotal = 5000m,
            TaxAmount = 0m,
            GrandTotal = 5000m,
            Lines = new()
            {
                new EInvoiceLineData
                {
                    Description = "Export Product A",
                    Quantity = 10,
                    UnitPrice = 500m,
                    TaxAmount = 0m
                }
            }
        };

        var xml = _xmlBuilder.Build(data);

        // Foreign TIN scheme
        Assert.Contains($"<cbc:ID schemeID=\"TIN\">{EInvoiceConsts.ForeignPartyTin}</cbc:ID>", xml);
        // Passport ID type
        Assert.Contains("<cbc:ID schemeID=\"PASSPORT\">E9876543A</cbc:ID>", xml);
        // Country SGP
        Assert.Contains("<cbc:IdentificationCode>SGP</cbc:IdentificationCode>", xml);
        // Contact phone and email
        Assert.Contains("<cbc:Telephone>+6561234567</cbc:Telephone>", xml);
        Assert.Contains("<cbc:ElectronicMail>finance@sgimporter.com</cbc:ElectronicMail>", xml);
    }

    [Fact]
    public void BuildXml_ForeignSupplier_IncludesForeignTinAndCountry()
    {
        var data = new EInvoiceDocumentData
        {
            InvoiceNumber = "PI-IMPORT-001",
            IssueDate = new DateTime(2026, 9, 1, 14, 0, 0),
            DocumentTypeCode = "11", // Self-billed invoice
            CurrencyCode = "USD",
            Supplier = new EInvoicePartyData
            {
                Name = "Japan Component Corp",
                Tin = EInvoiceConsts.ForeignPartyTin,
                IdType = "BRN",
                IdValue = "JP-12345678",
                CountryCode = "JPN",
                Phone = "+81312345678",
                Email = "export@jpcomponent.jp"
            },
            Buyer = new EInvoicePartyData
            {
                Name = "Malaysian Importer Sdn Bhd",
                Tin = "C12345678900",
                IdType = "BRN",
                IdValue = "202001012345",
                CountryCode = "MYS"
            },
            NetTotal = 10000m,
            TaxAmount = 0m,
            GrandTotal = 10000m,
            Lines = new()
            {
                new EInvoiceLineData
                {
                    Description = "Electronic Component",
                    Quantity = 100,
                    UnitPrice = 100m,
                    TaxAmount = 0m
                }
            }
        };

        var xml = _xmlBuilder.Build(data);

        Assert.Contains($"<cbc:ID schemeID=\"TIN\">{EInvoiceConsts.ForeignPartyTin}</cbc:ID>", xml);
        Assert.Contains("<cbc:ID schemeID=\"BRN\">JP-12345678</cbc:ID>", xml);
        Assert.Contains("<cbc:IdentificationCode>JPN</cbc:IdentificationCode>", xml);
        Assert.Contains("<cbc:Telephone>+81312345678</cbc:Telephone>", xml);
        Assert.Contains("<cbc:ElectronicMail>export@jpcomponent.jp</cbc:ElectronicMail>", xml);
    }
}
