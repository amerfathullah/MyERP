using System.ComponentModel.DataAnnotations;
using MyERP.Shared;

namespace MyERP.Core;

public class CreateUpdateCompanyDto
{
    [Required]
    [StringLength(CompanyConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [StringLength(CompanyConsts.MaxShortNameLength)]
    public string? ShortName { get; set; }

    [StringLength(CompanyConsts.MaxTaxIdLength)]
    public string? TaxId { get; set; }

    [StringLength(CompanyConsts.MaxRegistrationNumberLength)]
    public string? RegistrationNumber { get; set; }

    [StringLength(CompanyConsts.MaxSstRegistrationLength)]
    public string? SstRegistrationNumber { get; set; }

    [StringLength(CompanyConsts.MaxMsicCodeLength)]
    public string? MsicCode { get; set; }

    [StringLength(CompanyConsts.MaxPhoneLength)]
    public string? Phone { get; set; }

    [StringLength(CompanyConsts.MaxEmailLength)]
    [OptionalEmailAddress]
    public string? Email { get; set; }

    [StringLength(CompanyConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    [StringLength(CompanyConsts.MaxAddressLength)]
    public string? Address { get; set; }

    [StringLength(CompanyConsts.MaxCityLength)]
    public string? City { get; set; }

    [StringLength(CompanyConsts.MaxStateLength)]
    public string? State { get; set; }

    [StringLength(CompanyConsts.MaxPostalCodeLength)]
    public string? PostalCode { get; set; }

    [StringLength(CompanyConsts.MaxCountryLength)]
    public string? Country { get; set; }

    [Required]
    [StringLength(CompanyConsts.MaxCurrencyCodeLength)]
    public string CurrencyCode { get; set; } = "MYR";

    [Range(1, 12)]
    public int FiscalYearStartMonth { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public bool AllowUomWithConversionRateDefinedInItem { get; set; }

    // Warehouse Defaults (moved from Stock Settings to Company per PR #57571)
    public System.Guid? DefaultWarehouseId { get; set; }
    public System.Guid? SampleRetentionWarehouseId { get; set; }
    public System.Guid? DefaultInTransitWarehouseId { get; set; }
    public System.Guid? DefaultWarehouseForSalesReturnId { get; set; }
    public System.Guid? DefaultWipWarehouseId { get; set; }
    public System.Guid? DefaultFgWarehouseId { get; set; }
    public System.Guid? DefaultScrapWarehouseId { get; set; }

    // Account Defaults
    public System.Guid? DefaultReceivableAccountId { get; set; }
    public System.Guid? DefaultPayableAccountId { get; set; }
    public System.Guid? DefaultIncomeAccountId { get; set; }
    public System.Guid? DefaultExpenseAccountId { get; set; }
    public System.Guid? DefaultTaxPayableAccountId { get; set; }
    public System.Guid? DefaultBankAccountId { get; set; }
    public System.Guid? DefaultInventoryAccountId { get; set; }
    public System.Guid? StockReceivedButNotBilledAccountId { get; set; }
    public System.Guid? StockDeliveredButNotBilledAccountId { get; set; }
    public System.Guid? DefaultCostCenterId { get; set; }
    public System.Guid? RoundOffAccountId { get; set; }
    public System.Guid? RoundOffForOpeningAccountId { get; set; }

    // Advance Payment Defaults (gotcha #510)
    public bool BookAdvancePaymentsInSeparatePartyAccount { get; set; }
    public System.Guid? DefaultAdvanceReceivedAccountId { get; set; }
    public System.Guid? DefaultAdvancePaidAccountId { get; set; }
}
