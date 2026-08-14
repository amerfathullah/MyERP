using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class ProfitLossByCostCenterDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetProfit { get; set; }
    public decimal OverallMargin { get; set; }
    public List<CostCenterPLRowDto> CostCenters { get; set; } = new();
}

public class CostCenterPLRowDto
{
    public Guid CostCenterId { get; set; }
    public string CostCenterName { get; set; } = null!;
    public decimal Revenue { get; set; }
    public decimal Expense { get; set; }
    public decimal NetProfit { get; set; }
    public decimal ProfitMargin { get; set; }
}
