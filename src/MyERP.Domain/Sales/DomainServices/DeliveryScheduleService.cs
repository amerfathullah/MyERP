using System;
using System.Collections.Generic;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Domain service for Sales Order Delivery Schedule management.
/// Per ERPNext sales_order/services/delivery_schedule.py:
/// - Generates schedule entries by splitting item qty across dates by frequency
/// - Supports Weekly, Monthly, Quarterly, Yearly frequencies
/// - Handles whole-number UOM enforcement (no fractional deliveries)
/// - Last entry absorbs rounding remainder (total scheduled = total ordered)
/// Per gotcha #108: SO has a dialog for frequency-based split deliveries.
/// </summary>
public class DeliveryScheduleService : DomainService
{
    /// <summary>
    /// Generate delivery schedule entries for a Sales Order item by splitting qty evenly
    /// across the date range at the specified frequency.
    /// </summary>
    /// <param name="salesOrderId">Parent SO ID</param>
    /// <param name="salesOrderItemId">SO Item to schedule</param>
    /// <param name="totalQty">Total quantity to deliver</param>
    /// <param name="startDate">First delivery date</param>
    /// <param name="endDate">Last possible delivery date</param>
    /// <param name="frequency">Delivery frequency (Weekly, Monthly, Quarterly, Yearly)</param>
    /// <param name="mustBeWholeNumber">If true, rounds down per delivery (last absorbs remainder)</param>
    /// <param name="tenantId">Tenant for multi-tenancy</param>
    /// <returns>List of delivery schedule entries</returns>
    public List<DeliveryScheduleEntry> GenerateSchedule(
        Guid salesOrderId,
        Guid salesOrderItemId,
        decimal totalQty,
        DateTime startDate,
        DateTime endDate,
        DeliveryFrequency frequency,
        bool mustBeWholeNumber = false,
        Guid? tenantId = null)
    {
        var dates = GenerateDates(startDate, endDate, frequency);
        if (dates.Count == 0) return new List<DeliveryScheduleEntry>();

        var qtyPerDelivery = totalQty / dates.Count;
        if (mustBeWholeNumber)
        {
            qtyPerDelivery = Math.Floor(qtyPerDelivery);
        }

        var entries = new List<DeliveryScheduleEntry>();
        var allocated = 0m;

        for (int i = 0; i < dates.Count; i++)
        {
            decimal qty;
            if (i == dates.Count - 1)
            {
                // Last entry absorbs remainder (total - already_allocated)
                qty = totalQty - allocated;
            }
            else
            {
                qty = qtyPerDelivery;
            }

            entries.Add(new DeliveryScheduleEntry(
                Guid.NewGuid(), salesOrderId, salesOrderItemId,
                dates[i], qty, tenantId));

            allocated += qty;
        }

        return entries;
    }

    /// <summary>
    /// Generate delivery dates between start and end at specified frequency.
    /// </summary>
    private static List<DateTime> GenerateDates(DateTime start, DateTime end, DeliveryFrequency frequency)
    {
        var dates = new List<DateTime>();
        var current = start;

        while (current <= end)
        {
            dates.Add(current);
            current = frequency switch
            {
                DeliveryFrequency.Weekly => current.AddDays(7),
                DeliveryFrequency.Monthly => current.AddMonths(1),
                DeliveryFrequency.Quarterly => current.AddMonths(3),
                DeliveryFrequency.Yearly => current.AddYears(1),
                _ => current.AddMonths(1)
            };
        }

        return dates;
    }
}

/// <summary>
/// Delivery schedule frequency options.
/// Per ERPNext: SO delivery schedule dialog offers these split options.
/// </summary>
public enum DeliveryFrequency
{
    Weekly = 0,
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3
}
