using System;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Batch and Stock Entry invariants:
/// - Batch auto-derives ManufacturingDate from reference doc PostingDate if unset (Gotcha #242)
/// - ExpiryDate is derived from ManufacturingDate + ShelfLifeInDays
/// - StockEntry has IsAdditionalTransferEntry flag for tracking excess WO material transfers (Gotcha #179)
/// </summary>
public class BatchAndStockEntryInvariantTests
{
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void Batch_DerivesManufacturingDate_FromReferenceDocPostingDate_WhenUnset()
    {
        var postingDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var batch = new Batch(Guid.NewGuid(), _itemId, "BATCH-2026-001")
        {
            ShelfLifeInDays = 30
        };

        batch.DeriveManufacturingDateAndExpiry(postingDate);

        Assert.Equal(postingDate, batch.ManufacturingDate);
        Assert.Equal(postingDate.AddDays(30), batch.ExpiryDate);
    }

    [Fact]
    public void Batch_PreservesExplicitManufacturingDate_OverReferenceDocPostingDate()
    {
        var explicitMfgDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var postingDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var batch = new Batch(Guid.NewGuid(), _itemId, "BATCH-2026-002")
        {
            ManufacturingDate = explicitMfgDate,
            ShelfLifeInDays = 60
        };

        batch.DeriveManufacturingDateAndExpiry(postingDate);

        Assert.Equal(explicitMfgDate, batch.ManufacturingDate);
        Assert.Equal(explicitMfgDate.AddDays(60), batch.ExpiryDate);
    }

    [Fact]
    public void StockEntry_IsAdditionalTransferEntry_DefaultsFalse_AndCanBeSet()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);

        Assert.False(se.IsAdditionalTransferEntry);

        se.IsAdditionalTransferEntry = true;
        Assert.True(se.IsAdditionalTransferEntry);
    }
}
