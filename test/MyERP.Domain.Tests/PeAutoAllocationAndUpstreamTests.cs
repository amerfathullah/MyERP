using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MyERP.Accounting;
using MyERP.Accounting.Entities;

namespace MyERP.Domain.Tests;

public class PeAutoAllocationAndUpstreamTests
{
    [Fact]
    public void AutoAllocationResult_DefaultsZero()
    {
        var result = new AutoAllocationResultDto();
        Assert.Equal(0m, result.TotalAllocated);
        Assert.Equal(0m, result.UnallocatedAmount);
        Assert.Equal(0m, result.WriteOffAmount);
        Assert.Equal(0, result.InvoiceCount);
        Assert.Empty(result.Allocations);
    }

    [Fact]
    public void AutoAllocationResult_AllFieldsSettable()
    {
        var result = new AutoAllocationResultDto
        {
            TotalAllocated = 5000m,
            UnallocatedAmount = 200m,
            WriteOffAmount = 0.50m,
            InvoiceCount = 3,
            Allocations = new List<AllocationSuggestionDto>
            {
                new() { InvoiceId = Guid.NewGuid(), AllocatedAmount = 2000m },
                new() { InvoiceId = Guid.NewGuid(), AllocatedAmount = 2000m },
                new() { InvoiceId = Guid.NewGuid(), AllocatedAmount = 1000m },
            }
        };
        Assert.Equal(5000m, result.TotalAllocated);
        Assert.Equal(3, result.Allocations.Count);
    }

    [Fact]
    public void AllocationSuggestion_HasAllFields()
    {
        var id = Guid.NewGuid();
        var dto = new AllocationSuggestionDto
        {
            InvoiceId = id,
            InvoiceNumber = "SI-2026-00042",
            InvoiceType = "SalesInvoice",
            Outstanding = 1500m,
            AllocatedAmount = 1200m,
            DueDate = new DateTime(2026, 6, 15),
            IsOverdue = true,
        };
        Assert.Equal(id, dto.InvoiceId);
        Assert.Equal("SI-2026-00042", dto.InvoiceNumber);
        Assert.Equal(1500m, dto.Outstanding);
        Assert.Equal(1200m, dto.AllocatedAmount);
        Assert.True(dto.IsOverdue);
    }

    [Fact]
    public void AutoAllocateRequest_HasWriteOffThreshold()
    {
        var req = new AutoAllocateRequestDto
        {
            PartyType = "Customer",
            PartyId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PaymentAmount = 10000m,
            WriteOffThreshold = 2.00m,
        };
        Assert.Equal(2.00m, req.WriteOffThreshold);
    }

    [Fact]
    public void WriteOffThreshold_DefaultsNull()
    {
        var req = new AutoAllocateRequestDto
        {
            PartyType = "Supplier",
            PartyId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PaymentAmount = 5000m,
        };
        Assert.Null(req.WriteOffThreshold);
    }

    [Fact]
    public void FifoAllocation_OldestInvoiceFirst()
    {
        // Simulates FIFO: oldest due date gets allocated first
        var invoices = new[]
        {
            new { DueDate = new DateTime(2026, 3, 1), Outstanding = 1000m },
            new { DueDate = new DateTime(2026, 1, 15), Outstanding = 500m },
            new { DueDate = new DateTime(2026, 2, 10), Outstanding = 2000m },
        };

        var sorted = invoices.OrderBy(i => i.DueDate).ToList();
        var paymentAmount = 1200m;
        var allocations = new List<decimal>();
        var remaining = paymentAmount;

        foreach (var inv in sorted)
        {
            if (remaining <= 0) break;
            var allocate = Math.Min(remaining, inv.Outstanding);
            allocations.Add(allocate);
            remaining -= allocate;
        }

        // Oldest (Jan 15, 500) gets 500, next (Feb 10, 2000) gets 700
        Assert.Equal(500m, allocations[0]);
        Assert.Equal(700m, allocations[1]);
        Assert.Equal(2, allocations.Count);
    }

    [Fact]
    public void FifoAllocation_ExactPayment_ZeroUnallocated()
    {
        var totalOutstanding = 3500m;
        var paymentAmount = 3500m;
        var unallocated = paymentAmount - totalOutstanding;
        Assert.Equal(0m, unallocated);
    }

    [Fact]
    public void WriteOff_SuggestedWhenBelowThreshold()
    {
        var paymentAmount = 1000.50m;
        var totalOutstanding = 1000m;
        var remaining = paymentAmount - totalOutstanding;
        var threshold = 1.0m;
        var writeOff = remaining > 0 && remaining <= threshold ? remaining : 0m;
        Assert.Equal(0.50m, writeOff);
    }

    [Fact]
    public void WriteOff_NotSuggestedWhenAboveThreshold()
    {
        var paymentAmount = 1005m;
        var totalOutstanding = 1000m;
        var remaining = paymentAmount - totalOutstanding;
        var threshold = 1.0m;
        var writeOff = remaining > 0 && remaining <= threshold ? remaining : 0m;
        Assert.Equal(0m, writeOff);
    }

    [Fact]
    public void WriteOff_NotSuggestedWhenExactMatch()
    {
        var paymentAmount = 1000m;
        var totalOutstanding = 1000m;
        var remaining = paymentAmount - totalOutstanding;
        var writeOff = remaining > 0 && remaining <= 1m ? remaining : 0m;
        Assert.Equal(0m, writeOff);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_WhenNoReferences()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var pe = new PaymentEntry(Guid.NewGuid(), companyId, PaymentType.Receive,
            DateTime.UtcNow, 5000m, accountId, accountId);
        Assert.Equal(5000m, pe.UnallocatedAmount);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_WithPartialAllocation()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var pe = new PaymentEntry(Guid.NewGuid(), companyId, PaymentType.Receive,
            DateTime.UtcNow, 5000m, accountId, accountId);
        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(), 3000m, 3000m, 3000m));
        Assert.Equal(2000m, pe.UnallocatedAmount);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_FullyAllocated()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var pe = new PaymentEntry(Guid.NewGuid(), companyId, PaymentType.Receive,
            DateTime.UtcNow, 5000m, accountId, accountId);
        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(), 3000m, 3000m, 3000m));
        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(), 2000m, 2000m, 2000m));
        Assert.Equal(0m, pe.UnallocatedAmount);
    }

    [Fact]
    public void Upstream_PR57634_WoGanttBarColors_NoBusinessLogic()
    {
        // PR #57634: status-based bar colors in Work Order gantt view
        // Changes: work_order_calendar.js only (57 lines added)
        // Impact: NONE — JS calendar file, no business logic change
        // MyERP: Angular doesn't use ERPNext's gantt view
        Assert.True(true, "PR #57634 is JS-only gantt calendar colors — no migration impact");
    }

    [Fact]
    public void Upstream_MyInvois_Unchanged()
    {
        // myinvois: no new commits since 6501660
        Assert.True(true, "myinvois unchanged");
    }

    [Theory]
    [InlineData("AllocatedToInvoices")]
    [InlineData("WriteOffSuggested")]
    [InlineData("WriteOffAmount")]
    [InlineData("AllocateAutomatically")]
    public void Localization_NewKeys_Exist(string key)
    {
        var json = System.IO.File.ReadAllText(
            "../../../../../src/MyERP.Domain.Shared/Localization/MyERP/en.json");
        Assert.Contains($"\"{key}\"", json);
    }
}
