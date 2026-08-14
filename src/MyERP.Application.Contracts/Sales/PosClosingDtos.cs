using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class PosClosingDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid PosProfileId { get; set; }
    public DateTime PostingDate { get; set; }
    public string Status { get; set; } = null!;
    public decimal GrandTotal { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TotalDifference { get; set; }
    public Guid? ConsolidatedSalesInvoiceId { get; set; }
    public List<PosClosingPaymentDto> Payments { get; set; } = new();
    public List<PosClosingInvoiceDto> Invoices { get; set; } = new();
}

public class PosClosingPaymentDto
{
    public string ModeName { get; set; } = null!;
    public decimal ExpectedAmount { get; set; }
    public decimal ClosingAmount { get; set; }
    public decimal Difference { get; set; }
}

public class PosClosingInvoiceDto
{
    public Guid PosInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal GrandTotal { get; set; }
}

public class CreatePosClosingDto
{
    public Guid CompanyId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid PosOpeningEntryId { get; set; }
    public Guid UserId { get; set; }
    public List<CreatePosClosingInvoiceDto> Invoices { get; set; } = new();
    public List<CreatePosClosingPaymentDto> Payments { get; set; } = new();
}

public class CreatePosClosingInvoiceDto
{
    public Guid PosInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal GrandTotal { get; set; }
}

public class CreatePosClosingPaymentDto
{
    public Guid ModeOfPaymentId { get; set; }
    public string ModeName { get; set; } = null!;
    public decimal ExpectedAmount { get; set; }
    public decimal ClosingAmount { get; set; }
}

/// <summary>
/// Pre-calculated expected payment per mode — used to pre-fill the POS Closing form.
/// Per ERPNext: Opening Balance + Total POS Invoice payments during shift = Expected.
/// </summary>
public class PosExpectedPaymentDto
{
    public Guid ModeOfPaymentId { get; set; }
    public string ModeName { get; set; } = null!;
    public decimal OpeningAmount { get; set; }
    public decimal InvoiceTotal { get; set; }
    public decimal ExpectedAmount { get; set; }
}
