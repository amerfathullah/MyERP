using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class CompanyDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? ShortName { get; set; }
    public string? TaxId { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? SstRegistrationNumber { get; set; }
    public string? MsicCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public int FiscalYearStartMonth { get; set; }
    public bool IsActive { get; set; }

    // Warehouse Defaults (moved from Stock Settings to Company per PR #57571)
    public Guid? DefaultWarehouseId { get; set; }
    public Guid? SampleRetentionWarehouseId { get; set; }
    public Guid? DefaultWipWarehouseId { get; set; }
    public Guid? DefaultFgWarehouseId { get; set; }
    public Guid? DefaultScrapWarehouseId { get; set; }

    // Account Defaults
    public Guid? DefaultReceivableAccountId { get; set; }
    public Guid? DefaultPayableAccountId { get; set; }
    public Guid? DefaultIncomeAccountId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public Guid? DefaultBankAccountId { get; set; }
    public Guid? DefaultInventoryAccountId { get; set; }
    public Guid? StockReceivedButNotBilledAccountId { get; set; }
    public Guid? StockDeliveredButNotBilledAccountId { get; set; }
}
