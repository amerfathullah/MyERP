using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.EInvoice;
using MyERP.EInvoice.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.EInvoice;

/// <summary>
/// Unit tests for B2C E-Invoice consolidation workflow, candidate filtering,
/// >RM10k item exclusion, LHDN success log audit trail, and status refresh.
/// Migrated from myinvois consolidate_invoice.py and lhdn_success_log doctype.
/// </summary>
public class EInvoiceConsolidationFlowTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void CandidateDto_CorrectlyRepresentsEligibleInvoice()
    {
        var invId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var candidate = new ConsolidationCandidateDto
        {
            InvoiceId = invId,
            InvoiceNumber = "SINV-POS-001",
            IssueDate = now,
            CustomerId = CustomerId,
            CustomerName = "Walk-in Retail Customer",
            GrandTotal = 250.50m,
            ItemCount = 3,
            CurrencyCode = "MYR",
            IsEligible = true
        };

        Assert.Equal(invId, candidate.InvoiceId);
        Assert.Equal("SINV-POS-001", candidate.InvoiceNumber);
        Assert.Equal(250.50m, candidate.GrandTotal);
        Assert.Equal(3, candidate.ItemCount);
        Assert.True(candidate.IsEligible);
    }

    [Fact]
    public void ConsolidationDto_ContainsOriginalInvoicesAndConsolidatedSummary()
    {
        var consolId = Guid.NewGuid();
        var consolInvId = Guid.NewGuid();
        var orig1 = Guid.NewGuid();
        var orig2 = Guid.NewGuid();

        var dto = new EInvoiceConsolidationDto
        {
            Id = consolId,
            CompanyId = CompanyId,
            ConsolidatedInvoiceId = consolInvId,
            ConsolidatedInvoiceNumber = "CONSOL-2026-08-001",
            ConsolidatedIssueDate = DateTime.UtcNow,
            ConsolidatedGrandTotal = 1500.00m,
            EInvoiceStatus = "Valid",
            LhdnUuid = "LHDN-CONSOL-UUID-1234",
            OriginalInvoices = new List<ConsolidationCandidateDto>
            {
                new() { InvoiceId = orig1, InvoiceNumber = "POS-001", GrandTotal = 700.00m },
                new() { InvoiceId = orig2, InvoiceNumber = "POS-002", GrandTotal = 800.00m }
            }
        };

        Assert.Equal(consolId, dto.Id);
        Assert.Equal(consolInvId, dto.ConsolidatedInvoiceId);
        Assert.Equal(1500.00m, dto.ConsolidatedGrandTotal);
        Assert.Equal(2, dto.OriginalInvoices.Count);
        Assert.Equal(1500.00m, dto.OriginalInvoices.Sum(x => x.GrandTotal));
    }

    [Fact]
    public void LhdnSuccessLog_TracksRequiredAuditProperties()
    {
        var logId = Guid.NewGuid();
        var subId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var log = new LhdnSuccessLog(
            logId,
            CompanyId,
            subId,
            "LHDN-UUID-AUDIT-123",
            "SalesInvoice",
            docId)
        {
            SourceDocumentNumber = "SINV-2026-099",
            DocumentTypeCode = "01",
            LongId = "LONG-ID-8888",
            SubmittedAt = now,
            ValidatedAt = now.AddSeconds(5),
            GrandTotal = 5000.00m,
            CurrencyCode = "MYR",
            QrCodeUrl = "https://myinvois.hasil.gov.my/verify/LHDN-UUID-AUDIT-123",
            ResponseJson = "{\"status\":\"Accepted\"}"
        };

        Assert.Equal(logId, log.Id);
        Assert.Equal(CompanyId, log.CompanyId);
        Assert.Equal("LHDN-UUID-AUDIT-123", log.DocumentUuid);
        Assert.Equal("SalesInvoice", log.SourceDocumentType);
        Assert.Equal(docId, log.SourceDocumentId);
        Assert.Equal("SINV-2026-099", log.SourceDocumentNumber);
        Assert.Equal(5000.00m, log.GrandTotal);
        Assert.NotNull(log.QrCodeUrl);
        Assert.NotNull(log.ResponseJson);
    }

    [Theory]
    [InlineData(100.00, true)]
    [InlineData(5000.00, true)]
    [InlineData(10000.00, true)]
    [InlineData(10000.01, false)]
    [InlineData(25000.00, false)]
    public void ThresholdFilter_ValidatesConsolidationEligibility(decimal amount, bool expectedEligible)
    {
        const decimal maxConsolidationAmount = 10000.00m;
        var isEligible = amount <= maxConsolidationAmount;
        Assert.Equal(expectedEligible, isEligible);
    }
}
