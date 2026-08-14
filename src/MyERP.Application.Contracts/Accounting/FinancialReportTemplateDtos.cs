using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class FinancialReportTemplateDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public FinancialReportType ReportType { get; set; }
    public Guid? CompanyId { get; set; }
    public bool IsStandard { get; set; }
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public List<FinancialReportRowDto> Rows { get; set; } = new();
}

public class FinancialReportRowDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = null!;
    public FinancialReportDataSource DataSource { get; set; }
    public int SortOrder { get; set; }
    public string? ReferenceCode { get; set; }
    public string? CalculationFormula { get; set; }
    public string? AccountCategoryFilter { get; set; }
    public string? CustomApiPath { get; set; }
    public bool HideWhenEmpty { get; set; }
    public bool IsBold { get; set; }
    public int IndentLevel { get; set; }
    public int SignMultiplier { get; set; } = 1;
}

public class CreateFinancialReportTemplateDto
{
    public string Name { get; set; } = null!;
    public FinancialReportType ReportType { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Description { get; set; }
    public List<CreateFinancialReportRowDto> Rows { get; set; } = new();
}

public class CreateFinancialReportRowDto
{
    public string Label { get; set; } = null!;
    public FinancialReportDataSource DataSource { get; set; }
    public int SortOrder { get; set; }
    public string? ReferenceCode { get; set; }
    public string? CalculationFormula { get; set; }
    public string? AccountCategoryFilter { get; set; }
    public string? CustomApiPath { get; set; }
    public bool HideWhenEmpty { get; set; }
    public bool IsBold { get; set; }
    public int IndentLevel { get; set; }
    public int SignMultiplier { get; set; } = 1;
}

public class ExecuteReportDto
{
    public Guid TemplateId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? FinanceBook { get; set; }
}

public class FinancialReportResultDto
{
    public string TemplateName { get; set; } = null!;
    public string ReportType { get; set; } = null!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal GrandTotal { get; set; }
    public List<FinancialReportResultRowDto> Rows { get; set; } = new();
}

public class FinancialReportResultRowDto
{
    public string Label { get; set; } = null!;
    public decimal Value { get; set; }
    public int IndentLevel { get; set; }
    public bool IsBold { get; set; }
    public string? ReferenceCode { get; set; }
    public string DataSource { get; set; } = null!;
}
