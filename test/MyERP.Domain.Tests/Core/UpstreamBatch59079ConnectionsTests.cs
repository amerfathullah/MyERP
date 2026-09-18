using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Accounting.Entities;

namespace MyERP.Domain.Tests.Core;

/// <summary>
/// Unit tests for upstream PR #59079 (Scorecard connections) and DocumentConnections coverage.
/// </summary>
public class UpstreamBatch59079ConnectionsTests
{
    // === PR #59079: Scorecard Period linked records count ===

    [Fact]
    public void ScorecardPeriod_BelongsToSupplierScorecard()
    {
        var scorecardId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 3, 31);

        var period = new ScorecardPeriod(
            id: Guid.NewGuid(),
            scorecardId: scorecardId,
            supplierId: supplierId,
            startDate: start,
            endDate: end
        );

        period.SupplierScorecardId.ShouldBe(scorecardId);
        period.SupplierId.ShouldBe(supplierId);
        period.StartDate.ShouldBe(start);
        period.EndDate.ShouldBe(end);
        period.IsSubmitted.ShouldBeFalse();
    }

    [Fact]
    public void ScorecardConnections_AggregatesPeriodsCorrectly()
    {
        var scorecardId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var periods = new List<ScorecardPeriod>
        {
            new(Guid.NewGuid(), scorecardId, supplierId, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31)),
            new(Guid.NewGuid(), scorecardId, supplierId, new DateTime(2026, 4, 1), new DateTime(2026, 6, 30))
        };

        var matchedPeriods = periods.Where(p => p.SupplierScorecardId == scorecardId).ToList();

        var group = new ConnectionGroupDto
        {
            Label = "Scorecards",
            Items = new()
            {
                new ConnectionItemDto
                {
                    DocumentType = "Scorecard Period",
                    Count = matchedPeriods.Count,
                    Route = "/purchasing/scorecards/periods",
                    Documents = matchedPeriods.Select(p => new ConnectionDocumentDto
                    {
                        Id = p.Id,
                        DocumentNumber = p.StartDate.ToString("yyyy-MM-dd") + " to " + p.EndDate.ToString("yyyy-MM-dd"),
                        Status = p.IsSubmitted ? "Submitted" : "Draft",
                        Amount = p.TotalScore,
                        Date = p.StartDate,
                        Route = $"/purchasing/scorecards/periods/{p.Id}"
                    }).ToList()
                }
            }
        };

        group.Items.Count.ShouldBe(1);
        group.Items[0].Count.ShouldBe(2);
        group.Items[0].DocumentType.ShouldBe("Scorecard Period");
        group.Items[0].Documents.Count.ShouldBe(2);
        group.Items[0].Documents[0].DocumentNumber.ShouldBe("2026-01-01 to 2026-03-31");
        group.Items[0].Documents[1].DocumentNumber.ShouldBe("2026-04-01 to 2026-06-30");
    }

    // === Document Connections DTO invariants ===

    [Fact]
    public void DocumentConnectionsDto_InitializedEmpty()
    {
        var dto = new DocumentConnectionsDto();
        dto.Groups.ShouldNotBeNull();
        dto.Groups.ShouldBeEmpty();
    }

    [Fact]
    public void ConnectionGroupDto_AllowsMultipleConnectionItems()
    {
        var group = new ConnectionGroupDto
        {
            Label = "Manufacturing",
            Items = new()
            {
                new ConnectionItemDto { DocumentType = "Work Order", Count = 3, Route = "/manufacturing/work-orders" },
                new ConnectionItemDto { DocumentType = "Job Card", Count = 5, Route = "/manufacturing/job-cards" },
                new ConnectionItemDto { DocumentType = "Production Plan", Count = 1, Route = "/manufacturing/production-plans" }
            }
        };

        group.Label.ShouldBe("Manufacturing");
        group.Items.Count.ShouldBe(3);
        group.Items.Sum(i => i.Count).ShouldBe(9);
    }

    [Fact]
    public void ConnectionDocumentDto_PreservesEntityReferences()
    {
        var docId = Guid.NewGuid();
        var date = DateTime.UtcNow;
        var doc = new ConnectionDocumentDto
        {
            Id = docId,
            DocumentNumber = "WO-2026-0001",
            Status = "Completed",
            Amount = 1500.50m,
            Date = date,
            Route = $"/manufacturing/work-orders/{docId}"
        };

        doc.Id.ShouldBe(docId);
        doc.DocumentNumber.ShouldBe("WO-2026-0001");
        doc.Status.ShouldBe("Completed");
        doc.Amount.ShouldBe(1500.50m);
        doc.Date.ShouldBe(date);
        doc.Route.ShouldBe($"/manufacturing/work-orders/{docId}");
    }

    // === Job Card & BOM Connection Invariants ===

    [Fact]
    public void JobCard_ParentWorkOrderId_LinksCorrectly()
    {
        var companyId = Guid.NewGuid();
        var woId = Guid.NewGuid();
        var opId = Guid.NewGuid();
        var jc = new JobCard(Guid.NewGuid(), companyId, woId, opId, 10m, 1);

        jc.CompanyId.ShouldBe(companyId);
        jc.WorkOrderId.ShouldBe(woId);
        jc.OperationId.ShouldBe(opId);
        jc.ForQuantity.ShouldBe(10m);
        jc.SequenceId.ShouldBe(1);
    }

    [Fact]
    public void StockEntry_JobCardId_LinksToJobCard()
    {
        var companyId = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        var jcId = Guid.NewGuid();
        se.JobCardId = jcId;

        se.JobCardId.ShouldBe(jcId);
    }

    // === Journal Entry Reversal & Payment Entry References ===

    [Fact]
    public void JournalEntry_ReversalOf_TracksPredecessor()
    {
        var companyId = Guid.NewGuid();
        var fiscalYearId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var reversal = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, DateTime.Today);
        reversal.ReversalOfId = originalId;

        reversal.ReversalOfId.ShouldBe(originalId);
    }

    [Fact]
    public void PaymentEntryReference_LinksToJournalEntry()
    {
        var peId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var supplierAccountId = Guid.NewGuid();
        var jeId = Guid.NewGuid();

        var pe = new PaymentEntry(
            peId,
            companyId,
            PaymentType.Pay,
            DateTime.Today,
            1000m,
            bankAccountId,
            supplierAccountId
        );

        var reference = new PaymentEntryReference(
            Guid.NewGuid(),
            peId,
            "JournalEntry",
            jeId,
            500m,
            500m,
            500m,
            "JE-2026-0001"
        );
        pe.References.Add(reference);

        pe.References.Count.ShouldBe(1);
        pe.References.First().ReferenceType.ShouldBe("JournalEntry");
        pe.References.First().ReferenceId.ShouldBe(jeId);
        pe.References.First().AllocatedAmount.ShouldBe(500m);
    }
}
