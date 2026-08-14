using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class SupplierDeliveryPerformanceDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public int OnTimeDeliveries { get; set; }
    public int LateDeliveries { get; set; }
    public int PendingDeliveries { get; set; }
    public decimal OnTimeRate => TotalOrders > 0 ? Math.Round((decimal)OnTimeDeliveries / TotalOrders * 100, 1) : 0;
    public decimal AvgDelayDays { get; set; }
    public decimal TotalOrderValue { get; set; }
}

public class DeliveryPerformanceReportDto
{
    public List<SupplierDeliveryPerformanceDto> Suppliers { get; set; } = new();
    public int TotalOrders { get; set; }
    public int TotalOnTime { get; set; }
    public int TotalLate { get; set; }
    public int TotalPending { get; set; }
    public decimal OverallOnTimeRate => TotalOrders > 0 ? Math.Round((decimal)TotalOnTime / TotalOrders * 100, 1) : 0;
    public decimal OverallAvgDelayDays { get; set; }
}
