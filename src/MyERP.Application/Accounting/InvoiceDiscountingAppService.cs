using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public class InvoiceDiscountingDto
{
    public Guid Id { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal DiscountCharge { get; set; }
    public decimal DisbursementAmount { get; set; }
    public int Status { get; set; }
}

public class CreateInvoiceDiscountingDto
{
    public Guid CompanyId { get; set; }
    public decimal AnnualDiscountRate { get; set; }
    public int DaysToMaturity { get; set; }
    public List<InvoiceForDiscountingDto> Invoices { get; set; } = new();
}

public class InvoiceForDiscountingDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal OutstandingAmount { get; set; }
    public bool IsAlreadyDiscounted { get; set; }
}

/// <summary>
/// AppService for Invoice Discounting — selling receivables to a bank at a discount.
/// Delegates to InvoiceDiscountingService for state machine logic and GL entry building.
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class InvoiceDiscountingAppService : ApplicationService
{
    private readonly InvoiceDiscountingService _service;

    public InvoiceDiscountingAppService(InvoiceDiscountingService service)
    {
        _service = service;
    }

    /// <summary>
    /// Calculate discount charge and disbursement amount for a set of invoices.
    /// </summary>
    public Task<InvoiceDiscountingDto> CalculateAsync(CreateInvoiceDiscountingDto input)
    {
        // Validate invoices
        var invoices = input.Invoices.ConvertAll(i => new InvoiceForDiscounting
        {
            InvoiceId = i.InvoiceId,
            InvoiceNumber = i.InvoiceNumber,
            OutstandingAmount = i.OutstandingAmount,
            IsAlreadyDiscounted = i.IsAlreadyDiscounted,
        });
        InvoiceDiscountingService.ValidateInvoicesForDiscounting(invoices);

        var totalOutstanding = 0m;
        foreach (var inv in invoices) totalOutstanding += inv.OutstandingAmount;

        var discountCharge = _service.CalculateDiscountCharge(
            totalOutstanding, input.AnnualDiscountRate, input.DaysToMaturity);
        var disbursement = _service.CalculateDisbursementAmount(totalOutstanding, discountCharge);

        return Task.FromResult(new InvoiceDiscountingDto
        {
            TotalOutstanding = totalOutstanding,
            DiscountCharge = discountCharge,
            DisbursementAmount = disbursement,
            Status = (int)InvoiceDiscountingStatus.Sanctioned,
        });
    }
}
