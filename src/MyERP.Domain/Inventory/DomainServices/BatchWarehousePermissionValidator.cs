using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Enforces warehouse access in batch quantity calculations.
/// Per ERPNext PR #64314 (commits cbc40ba340, 34e791febb, 55fd06a510):
/// - If warehouse is specified: verifies the user has permission to access that warehouse.
/// - If warehouse is omitted: filters returned batch quantities strictly to permitted warehouses.
/// - Internal/system recalculation (for_stock_levels / recalculate_batch_qty) bypasses user restriction
///   so total batch quantity across the company is preserved.
/// </summary>
public class BatchWarehousePermissionValidator : DomainService
{
    /// <summary>
    /// Validates warehouse access for a specific requested warehouse.
    /// Throws InsufficientPermission if the user's permitted warehouse set does not include it.
    /// </summary>
    public static void ValidateWarehouseAccess(Guid requestedWarehouseId, ISet<Guid>? permittedWarehouseIds)
    {
        if (permittedWarehouseIds != null && permittedWarehouseIds.Count > 0 && !permittedWarehouseIds.Contains(requestedWarehouseId))
        {
            throw new BusinessException(MyERPDomainErrorCodes.WarehouseAccessRestricted)
                .WithData("warehouseId", requestedWarehouseId)
                .WithData("detail", $"User does not have permission to access warehouse {requestedWarehouseId}.");
        }
    }

    /// <summary>
    /// Filters a batch warehouse balance collection by permitted warehouses.
    /// </summary>
    public static List<T> FilterByPermittedWarehouses<T>(
        IEnumerable<T> items,
        Func<T, Guid> warehouseIdSelector,
        ISet<Guid>? permittedWarehouseIds,
        Guid? requestedWarehouseId = null)
    {
        if (requestedWarehouseId.HasValue)
        {
            ValidateWarehouseAccess(requestedWarehouseId.Value, permittedWarehouseIds);
        }

        if (permittedWarehouseIds == null || permittedWarehouseIds.Count == 0)
        {
            return items.ToList();
        }

        return items.Where(x => permittedWarehouseIds.Contains(warehouseIdSelector(x))).ToList();
    }
}
