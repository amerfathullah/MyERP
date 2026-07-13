using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Validates batch expiry on outward stock transactions.
/// Per DO-NOT rules: must block expired batch consumption in transactions.
/// Cannot use expired batches for stock-out (DN, SI with update_stock, SE outward).
/// </summary>
public class BatchExpiryValidationService : DomainService
{
    private readonly IRepository<Batch, Guid> _batchRepository;

    public BatchExpiryValidationService(IRepository<Batch, Guid> batchRepository)
    {
        _batchRepository = batchRepository;
    }

    /// <summary>
    /// Validates that none of the specified batches are expired or disabled.
    /// Called before stock-out transactions (DN submit, SE outward, SI with stock).
    /// </summary>
    public async Task ValidateForStockOutAsync(IEnumerable<BatchValidationItem> items, DateTime transactionDate)
    {
        var batchIds = items
            .Where(i => i.BatchId.HasValue)
            .Select(i => i.BatchId!.Value)
            .Distinct()
            .ToList();

        if (!batchIds.Any()) return;

        var batches = await _batchRepository.GetListAsync(b => batchIds.Contains(b.Id));

        foreach (var item in items.Where(i => i.BatchId.HasValue))
        {
            var batch = batches.FirstOrDefault(b => b.Id == item.BatchId!.Value);
            if (batch == null) continue;

            if (batch.IsDisabled)
            {
                throw new BusinessException(MyERPDomainErrorCodes.BatchDisabled)
                    .WithData("batchNo", batch.BatchNo)
                    .WithData("item", item.ItemName ?? item.ItemId.ToString());
            }

            if (batch.IsExpired(transactionDate))
            {
                throw new BusinessException(MyERPDomainErrorCodes.BatchExpired)
                    .WithData("batchNo", batch.BatchNo)
                    .WithData("expiryDate", batch.ExpiryDate?.ToString("yyyy-MM-dd") ?? "N/A")
                    .WithData("item", item.ItemName ?? item.ItemId.ToString());
            }
        }
    }
}

/// <summary>Input for batch expiry validation.</summary>
public class BatchValidationItem
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? BatchId { get; set; }

    public BatchValidationItem(Guid itemId, Guid? batchId, string? itemName = null)
    {
        ItemId = itemId;
        BatchId = batchId;
        ItemName = itemName;
    }
}
