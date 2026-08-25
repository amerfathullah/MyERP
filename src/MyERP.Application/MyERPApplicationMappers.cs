using System.Linq;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace MyERP;

/// <summary>
/// Mapperly mappers for Entity → DTO.
/// Entity → DTO: auto-mapped by Mapperly source generator.
/// DTO → Entity: override MapToEntity in CrudAppService subclasses (domain constructors + setter methods).
/// </summary>

// ─── CrudAppService mappers (Item, Customer, Supplier) ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ItemToItemDtoMapper : MapperBase<Inventory.Entities.Item, Inventory.ItemDto>
{
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.TotalStockQty))]
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.IsLowStock))]
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.Barcodes))]
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.Suppliers))]
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.CustomerDetails))]
    public override partial Inventory.ItemDto Map(Inventory.Entities.Item source);
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.TotalStockQty))]
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.IsLowStock))]
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.Barcodes))]
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.Suppliers))]
    [MapperIgnoreTarget(nameof(Inventory.ItemDto.CustomerDetails))]
    public override partial void Map(Inventory.Entities.Item source, Inventory.ItemDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class CustomerToCustomerDtoMapper : MapperBase<Sales.Entities.Customer, Sales.CustomerDto>
{
    public override partial Sales.CustomerDto Map(Sales.Entities.Customer source);
    public override partial void Map(Sales.Entities.Customer source, Sales.CustomerDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ItemGroupToItemGroupDtoMapper : MapperBase<Inventory.Entities.ItemGroup, Inventory.ItemGroupDto>
{
    public override partial Inventory.ItemGroupDto Map(Inventory.Entities.ItemGroup source);
    public override partial void Map(Inventory.Entities.ItemGroup source, Inventory.ItemGroupDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BrandToBrandDtoMapper : MapperBase<Inventory.Entities.Brand, Inventory.BrandDto>
{
    public override partial Inventory.BrandDto Map(Inventory.Entities.Brand source);
    public override partial void Map(Inventory.Entities.Brand source, Inventory.BrandDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DesignationToDesignationDtoMapper : MapperBase<HumanResources.Entities.Designation, HumanResources.DesignationDto>
{
    public override partial HumanResources.DesignationDto Map(HumanResources.Entities.Designation source);
    public override partial void Map(HumanResources.Entities.Designation source, HumanResources.DesignationDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DepartmentToDepartmentDtoMapper : MapperBase<HumanResources.Entities.Department, HumanResources.DepartmentDto>
{
    public override partial HumanResources.DepartmentDto Map(HumanResources.Entities.Department source);
    public override partial void Map(HumanResources.Entities.Department source, HumanResources.DepartmentDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SupplierToSupplierDtoMapper : MapperBase<Purchasing.Entities.Supplier, Purchasing.SupplierDto>
{
    public override partial Purchasing.SupplierDto Map(Purchasing.Entities.Supplier source);
    public override partial void Map(Purchasing.Entities.Supplier source, Purchasing.SupplierDto destination);
}

// ─── Accounting ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AccountingDimensionMapper : MapperBase<Accounting.Entities.AccountingDimension, Accounting.AccountingDimensionDto>
{
    public override partial Accounting.AccountingDimensionDto Map(Accounting.Entities.AccountingDimension source);
    public override partial void Map(Accounting.Entities.AccountingDimension source, Accounting.AccountingDimensionDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BankAccountMapper : MapperBase<Accounting.Entities.BankAccount, Accounting.BankAccountDto>
{
    public override partial Accounting.BankAccountDto Map(Accounting.Entities.BankAccount source);
    public override partial void Map(Accounting.Entities.BankAccount source, Accounting.BankAccountDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BankTransactionMapper : MapperBase<Accounting.Entities.BankTransaction, Accounting.BankTransactionDto>
{
    public override partial Accounting.BankTransactionDto Map(Accounting.Entities.BankTransaction source);
    public override partial void Map(Accounting.Entities.BankTransaction source, Accounting.BankTransactionDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AccountingPeriodMapper : MapperBase<Accounting.Entities.AccountingPeriod, Accounting.AccountingPeriodDto>
{
    public override partial Accounting.AccountingPeriodDto Map(Accounting.Entities.AccountingPeriod source);
    public override partial void Map(Accounting.Entities.AccountingPeriod source, Accounting.AccountingPeriodDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class CostCenterMapper : MapperBase<Accounting.Entities.CostCenter, Accounting.CostCenterDto>
{
    public override partial Accounting.CostCenterDto Map(Accounting.Entities.CostCenter source);
    public override partial void Map(Accounting.Entities.CostCenter source, Accounting.CostCenterDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class CurrencyExchangeMapper : MapperBase<Accounting.Entities.CurrencyExchange, Accounting.CurrencyExchangeDto>
{
    public override partial Accounting.CurrencyExchangeDto Map(Accounting.Entities.CurrencyExchange source);
    public override partial void Map(Accounting.Entities.CurrencyExchange source, Accounting.CurrencyExchangeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FiscalYearMapper : MapperBase<Accounting.Entities.FiscalYear, Accounting.FiscalYearDto>
{
    public override partial Accounting.FiscalYearDto Map(Accounting.Entities.FiscalYear source);
    public override partial void Map(Accounting.Entities.FiscalYear source, Accounting.FiscalYearDto destination);
}

// ─── Core ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AddressMapper : MapperBase<Core.Entities.Address, Core.AddressDto>
{
    public override partial Core.AddressDto Map(Core.Entities.Address source);
    public override partial void Map(Core.Entities.Address source, Core.AddressDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ContactMapper : MapperBase<Core.Entities.Contact, Core.ContactDto>
{
    public override partial Core.ContactDto Map(Core.Entities.Contact source);
    public override partial void Map(Core.Entities.Contact source, Core.ContactDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DocumentActivityLogMapper : MapperBase<Core.Entities.DocumentActivityLog, Core.DocumentActivityLogDto>
{
    public override partial Core.DocumentActivityLogDto Map(Core.Entities.DocumentActivityLog source);
    public override partial void Map(Core.Entities.DocumentActivityLog source, Core.DocumentActivityLogDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EmailTemplateMapper : MapperBase<Core.Entities.EmailTemplate, Core.EmailTemplateDto>
{
    public override partial Core.EmailTemplateDto Map(Core.Entities.EmailTemplate source);
    public override partial void Map(Core.Entities.EmailTemplate source, Core.EmailTemplateDto destination);
}

// ─── Automation ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AutomationRuleMapper : MapperBase<Automation.Entities.AutomationRule, Automation.DTOs.AutomationRuleDto>
{
    public override partial Automation.DTOs.AutomationRuleDto Map(Automation.Entities.AutomationRule source);
    public override partial void Map(Automation.Entities.AutomationRule source, Automation.DTOs.AutomationRuleDto destination);
}

// ─── EInvoice ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EInvoiceSubmissionMapper : MapperBase<EInvoice.Entities.EInvoiceSubmission, EInvoice.EInvoiceSubmissionDto>
{
    public override partial EInvoice.EInvoiceSubmissionDto Map(EInvoice.Entities.EInvoiceSubmission source);
    public override partial void Map(EInvoice.Entities.EInvoiceSubmission source, EInvoice.EInvoiceSubmissionDto destination);
}

// ─── Human Resources ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LeaveAllocationMapper : MapperBase<HumanResources.Entities.LeaveAllocation, HumanResources.LeaveAllocationDto>
{
    public override partial HumanResources.LeaveAllocationDto Map(HumanResources.Entities.LeaveAllocation source);
    public override partial void Map(HumanResources.Entities.LeaveAllocation source, HumanResources.LeaveAllocationDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LeaveApplicationMapper : MapperBase<HumanResources.Entities.LeaveApplication, HumanResources.LeaveApplicationDto>
{
    public override partial HumanResources.LeaveApplicationDto Map(HumanResources.Entities.LeaveApplication source);
    public override partial void Map(HumanResources.Entities.LeaveApplication source, HumanResources.LeaveApplicationDto destination);
}

// ─── Inventory ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ItemGroupMapper : MapperBase<Inventory.Entities.ItemGroup, Inventory.ItemGroupDto>
{
    public override partial Inventory.ItemGroupDto Map(Inventory.Entities.ItemGroup source);
    public override partial void Map(Inventory.Entities.ItemGroup source, Inventory.ItemGroupDto destination);
}

// ─── Manufacturing ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ManufacturingSettingsMapper : MapperBase<Manufacturing.Entities.ManufacturingSettings, Manufacturing.ManufacturingSettingsDto>
{
    public override partial Manufacturing.ManufacturingSettingsDto Map(Manufacturing.Entities.ManufacturingSettings source);
    public override partial void Map(Manufacturing.Entities.ManufacturingSettings source, Manufacturing.ManufacturingSettingsDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class OperationMapper : MapperBase<Manufacturing.Entities.Operation, Manufacturing.OperationDto>
{
    public override partial Manufacturing.OperationDto Map(Manufacturing.Entities.Operation source);
    public override partial void Map(Manufacturing.Entities.Operation source, Manufacturing.OperationDto destination);
    private partial Manufacturing.SubOperationDto MapChild(Manufacturing.Entities.SubOperation source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class WorkstationTypeMapper : MapperBase<Manufacturing.Entities.WorkstationType, Manufacturing.WorkstationTypeDto>
{
    public override partial Manufacturing.WorkstationTypeDto Map(Manufacturing.Entities.WorkstationType source);
    public override partial void Map(Manufacturing.Entities.WorkstationType source, Manufacturing.WorkstationTypeDto destination);
    [MapProperty(nameof(Manufacturing.Entities.WorkstationTypeCost.OperatingComponent), nameof(Manufacturing.WorkstationTypeCostDto.Component))]
    private partial Manufacturing.WorkstationTypeCostDto MapChild(Manufacturing.Entities.WorkstationTypeCost source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DowntimeEntryMapper : MapperBase<Manufacturing.Entities.DowntimeEntry, Manufacturing.DowntimeEntryDto>
{
    public override partial Manufacturing.DowntimeEntryDto Map(Manufacturing.Entities.DowntimeEntry source);
    public override partial void Map(Manufacturing.Entities.DowntimeEntry source, Manufacturing.DowntimeEntryDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BomCreatorMapper : MapperBase<Manufacturing.Entities.BomCreator, Manufacturing.BomCreatorDto>
{
    public override partial Manufacturing.BomCreatorDto Map(Manufacturing.Entities.BomCreator source);
    public override partial void Map(Manufacturing.Entities.BomCreator source, Manufacturing.BomCreatorDto destination);
    private partial Manufacturing.BomCreatorItemDto MapItem(Manufacturing.Entities.BomCreatorItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ShareTypeMapper : MapperBase<Accounting.Entities.ShareType, Accounting.ShareTypeDto>
{
    public override partial Accounting.ShareTypeDto Map(Accounting.Entities.ShareType source);
    public override partial void Map(Accounting.Entities.ShareType source, Accounting.ShareTypeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ShareholderMapper : MapperBase<Accounting.Entities.Shareholder, Accounting.ShareholderDto>
{
    public override partial Accounting.ShareholderDto Map(Accounting.Entities.Shareholder source);
    public override partial void Map(Accounting.Entities.Shareholder source, Accounting.ShareholderDto destination);
    private partial Accounting.ShareBalanceEntryDto MapShareBalance(Accounting.Entities.ShareBalanceEntry source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ShareTransferMapper : MapperBase<Accounting.Entities.ShareTransfer, Accounting.ShareTransferDto>
{
    public override partial Accounting.ShareTransferDto Map(Accounting.Entities.ShareTransfer source);
    public override partial void Map(Accounting.Entities.ShareTransfer source, Accounting.ShareTransferDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class MonthlyDistributionMapper : MapperBase<Accounting.Entities.MonthlyDistribution, Accounting.MonthlyDistributionDto>
{
    public override partial Accounting.MonthlyDistributionDto Map(Accounting.Entities.MonthlyDistribution source);
    public override partial void Map(Accounting.Entities.MonthlyDistribution source, Accounting.MonthlyDistributionDto destination);
    private partial Accounting.MonthlyDistributionPercentageDto MapPercentage(Accounting.Entities.MonthlyDistributionPercentage source);
}

// ─── Sales ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PricingRuleMapper : MapperBase<Sales.Entities.PricingRule, Sales.PricingRuleDto>
{
    public override partial Sales.PricingRuleDto Map(Sales.Entities.PricingRule source);
    public override partial void Map(Sales.Entities.PricingRule source, Sales.PricingRuleDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PromotionalSchemeMapper : MapperBase<Sales.Entities.PromotionalScheme, Sales.PromotionalSchemeDto>
{
    [MapperIgnoreTarget(nameof(Sales.PromotionalSchemeDto.GeneratedRuleCount))]
    public override partial Sales.PromotionalSchemeDto Map(Sales.Entities.PromotionalScheme source);
    [MapperIgnoreTarget(nameof(Sales.PromotionalSchemeDto.GeneratedRuleCount))]
    public override partial void Map(Sales.Entities.PromotionalScheme source, Sales.PromotionalSchemeDto destination);
    private partial Sales.PromotionalSchemeTargetDto MapTarget(Sales.Entities.PromotionalSchemeTarget source);
    private partial Sales.PromotionalSchemePartyDto MapParty(Sales.Entities.PromotionalSchemeParty source);
    private partial Sales.PromotionalSchemePriceDiscountSlabDto MapPriceSlab(Sales.Entities.PromotionalSchemePriceDiscountSlab source);
    private partial Sales.PromotionalSchemeProductDiscountSlabDto MapProductSlab(Sales.Entities.PromotionalSchemeProductDiscountSlab source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class CouponCodeMapper : MapperBase<Sales.Entities.CouponCode, Sales.CouponCodeDto>
{
    public override partial Sales.CouponCodeDto Map(Sales.Entities.CouponCode source);
    public override partial void Map(Sales.Entities.CouponCode source, Sales.CouponCodeDto destination);
}

// ─── Support ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class IssueMapper : MapperBase<Support.Entities.Issue, Support.IssueDto>
{
    [MapperIgnoreTarget(nameof(Support.IssueDto.TotalHoldTimeSeconds))]
    public override partial Support.IssueDto Map(Support.Entities.Issue source);
    [MapperIgnoreTarget(nameof(Support.IssueDto.TotalHoldTimeSeconds))]
    public override partial void Map(Support.Entities.Issue source, Support.IssueDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class IssuePriorityMapper : MapperBase<Support.Entities.IssuePriority, Support.IssuePriorityDto>
{
    public override partial Support.IssuePriorityDto Map(Support.Entities.IssuePriority source);
    public override partial void Map(Support.Entities.IssuePriority source, Support.IssuePriorityDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class IssueTypeMapper : MapperBase<Support.Entities.IssueType, Support.IssueTypeDto>
{
    public override partial Support.IssueTypeDto Map(Support.Entities.IssueType source);
    public override partial void Map(Support.Entities.IssueType source, Support.IssueTypeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SupportSettingsMapper : MapperBase<Support.Entities.SupportSettings, Support.SupportSettingsDto>
{
    public override partial Support.SupportSettingsDto Map(Support.Entities.SupportSettings source);
    public override partial void Map(Support.Entities.SupportSettings source, Support.SupportSettingsDto destination);
}

// ─── Tax ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TaxRuleMapper : MapperBase<Tax.Entities.TaxRule, Tax.TaxRuleDto>
{
    public override partial Tax.TaxRuleDto Map(Tax.Entities.TaxRule source);
    public override partial void Map(Tax.Entities.TaxRule source, Tax.TaxRuleDto destination);
}

// ━━━ Enum-cast-only mappers (Mapperly auto-handles enum→int and enum→string) ━━━

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PaymentRequestMapper : MapperBase<Accounting.Entities.PaymentRequest, Accounting.PaymentRequestDto>
{
    public override partial Accounting.PaymentRequestDto Map(Accounting.Entities.PaymentRequest source);
    public override partial void Map(Accounting.Entities.PaymentRequest source, Accounting.PaymentRequestDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AssetMovementMapper : MapperBase<Assets.Entities.AssetMovement, Assets.AssetMovementDto>
{
    public override partial Assets.AssetMovementDto Map(Assets.Entities.AssetMovement source);
    public override partial void Map(Assets.Entities.AssetMovement source, Assets.AssetMovementDto destination);
    private partial Assets.AssetMovementItemDto MapMovementItem(Assets.Entities.AssetMovementItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AssetRepairMapper : MapperBase<Assets.Entities.AssetRepair, Assets.AssetRepairDto>
{
    public override partial Assets.AssetRepairDto Map(Assets.Entities.AssetRepair source);
    public override partial void Map(Assets.Entities.AssetRepair source, Assets.AssetRepairDto destination);
    private partial Assets.AssetRepairConsumedItemDto MapConsumedItem(Assets.Entities.AssetRepairConsumedItem source);
    private partial Assets.AssetRepairPurchaseInvoiceDto MapRepairInvoice(Assets.Entities.AssetRepairPurchaseInvoice source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AuthorizationRuleMapper : MapperBase<Core.Entities.AuthorizationRule, Core.AuthorizationRuleDto>
{
    public override partial Core.AuthorizationRuleDto Map(Core.Entities.AuthorizationRule source);
    public override partial void Map(Core.Entities.AuthorizationRule source, Core.AuthorizationRuleDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AutoRepeatMapper : MapperBase<Core.Entities.AutoRepeat, Core.AutoRepeatDto>
{
    public override partial Core.AutoRepeatDto Map(Core.Entities.AutoRepeat source);
    public override partial void Map(Core.Entities.AutoRepeat source, Core.AutoRepeatDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EmployeeMapper : MapperBase<HumanResources.Entities.Employee, HumanResources.EmployeeDto>
{
    public override partial HumanResources.EmployeeDto Map(HumanResources.Entities.Employee source);
    public override partial void Map(HumanResources.Entities.Employee source, HumanResources.EmployeeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SalarySlipMapper : MapperBase<HumanResources.Entities.SalarySlip, HumanResources.SalarySlipDto>
{
    public override partial HumanResources.SalarySlipDto Map(HumanResources.Entities.SalarySlip source);
    public override partial void Map(HumanResources.Entities.SalarySlip source, HumanResources.SalarySlipDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SerialNoMapper : MapperBase<Inventory.Entities.SerialNo, Inventory.SerialNoDto>
{
    public override partial Inventory.SerialNoDto Map(Inventory.Entities.SerialNo source);
    public override partial void Map(Inventory.Entities.SerialNo source, Inventory.SerialNoDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class StockClosingEntryMapper : MapperBase<Inventory.Entities.StockClosingEntry, Inventory.StockClosingEntryDto>
{
    public override partial Inventory.StockClosingEntryDto Map(Inventory.Entities.StockClosingEntry source);
    public override partial void Map(Inventory.Entities.StockClosingEntry source, Inventory.StockClosingEntryDto destination);

    [MapperIgnoreTarget(nameof(Inventory.StockClosingBalanceDto.ItemName))]
    [MapperIgnoreTarget(nameof(Inventory.StockClosingBalanceDto.WarehouseName))]
    private partial Inventory.StockClosingBalanceDto MapBalance(Inventory.Entities.StockClosingBalance source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class StockReservationEntryMapper : MapperBase<Inventory.Entities.StockReservationEntry, Inventory.StockReservationEntryDto>
{
    public override partial Inventory.StockReservationEntryDto Map(Inventory.Entities.StockReservationEntry source);
    public override partial void Map(Inventory.Entities.StockReservationEntry source, Inventory.StockReservationEntryDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TaxCategoryMapper : MapperBase<Tax.Entities.TaxCategory, Tax.TaxCategoryDto>
{
    public override partial Tax.TaxCategoryDto Map(Tax.Entities.TaxCategory source);
    public override partial void Map(Tax.Entities.TaxCategory source, Tax.TaxCategoryDto destination);
}

// ━━━ CHILDREN mappers (parent + child type methods for collection auto-mapping) ━━━

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BudgetMapper : MapperBase<Accounting.Entities.Budget, Dtos.BudgetDto>
{
    public override partial Dtos.BudgetDto Map(Accounting.Entities.Budget source);
    public override partial void Map(Accounting.Entities.Budget source, Dtos.BudgetDto destination);
    private partial Dtos.BudgetAccountDto MapChild(Accounting.Entities.BudgetAccount source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class JournalEntryMapper : MapperBase<Accounting.Entities.JournalEntry, Accounting.JournalEntryDto>
{
    public override partial Accounting.JournalEntryDto Map(Accounting.Entities.JournalEntry source);
    public override partial void Map(Accounting.Entities.JournalEntry source, Accounting.JournalEntryDto destination);
    [MapperIgnoreTarget(nameof(Accounting.JournalEntryLineDto.AccountName))]
    [MapperIgnoreTarget(nameof(Accounting.JournalEntryLineDto.AccountCode))]
    private partial Accounting.JournalEntryLineDto MapChild(Accounting.Entities.JournalEntryLine source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AssetMapper : MapperBase<Assets.Entities.Asset, Assets.AssetDto>
{
    [MapperIgnoreTarget(nameof(Assets.AssetDto.AssetCategoryName))]
    [MapProperty(nameof(Assets.Entities.Asset.DepreciationSchedule), nameof(Assets.AssetDto.Schedule))]
    public override partial Assets.AssetDto Map(Assets.Entities.Asset source);
    [MapperIgnoreTarget(nameof(Assets.AssetDto.AssetCategoryName))]
    [MapProperty(nameof(Assets.Entities.Asset.DepreciationSchedule), nameof(Assets.AssetDto.Schedule))]
    public override partial void Map(Assets.Entities.Asset source, Assets.AssetDto destination);
    private partial Assets.DepreciationScheduleDto MapSchedule(Assets.Entities.DepreciationScheduleEntry source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class MaintenanceScheduleMapper : MapperBase<Maintenance.Entities.MaintenanceSchedule, Assets.MaintenanceScheduleDto>
{
    public override partial Assets.MaintenanceScheduleDto Map(Maintenance.Entities.MaintenanceSchedule source);
    public override partial void Map(Maintenance.Entities.MaintenanceSchedule source, Assets.MaintenanceScheduleDto destination);
    private partial Assets.MaintenanceScheduleDetailDto MapChild(Maintenance.Entities.MaintenanceScheduleDetail source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class MaintenanceVisitMapper : MapperBase<Maintenance.Entities.MaintenanceVisit, Assets.MaintenanceVisitDto>
{
    public override partial Assets.MaintenanceVisitDto Map(Maintenance.Entities.MaintenanceVisit source);
    public override partial void Map(Maintenance.Entities.MaintenanceVisit source, Assets.MaintenanceVisitDto destination);
    private partial Assets.MaintenanceVisitPurposeDto MapChild(Maintenance.Entities.MaintenanceVisitPurpose source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class OpportunityMapper : MapperBase<CRM.Entities.Opportunity, CRM.OpportunityDto>
{
    [MapperIgnoreTarget(nameof(CRM.OpportunityDto.Competitors))]
    public override partial CRM.OpportunityDto Map(CRM.Entities.Opportunity source);
    [MapperIgnoreTarget(nameof(CRM.OpportunityDto.Competitors))]
    public override partial void Map(CRM.Entities.Opportunity source, CRM.OpportunityDto destination);
    private partial CRM.OpportunityItemDto MapChild(CRM.Entities.OpportunityItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ExpenseClaimMapper : MapperBase<HumanResources.Entities.ExpenseClaim, HumanResources.ExpenseClaimDto>
{
    public override partial HumanResources.ExpenseClaimDto Map(HumanResources.Entities.ExpenseClaim source);
    public override partial void Map(HumanResources.Entities.ExpenseClaim source, HumanResources.ExpenseClaimDto destination);
    private partial HumanResources.ExpenseClaimDetailDto MapChild(HumanResources.Entities.ExpenseClaimDetail source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class HolidayListMapper : MapperBase<HumanResources.Entities.HolidayList, HumanResources.HolidayListDto>
{
    public override partial HumanResources.HolidayListDto Map(HumanResources.Entities.HolidayList source);
    public override partial void Map(HumanResources.Entities.HolidayList source, HumanResources.HolidayListDto destination);
    private partial HumanResources.HolidayDto MapChild(HumanResources.Entities.Holiday source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AttendanceMapper : MapperBase<HumanResources.Entities.Attendance, HumanResources.AttendanceDto>
{
    [MapperIgnoreTarget(nameof(HumanResources.AttendanceDto.EmployeeName))]
    public override partial HumanResources.AttendanceDto Map(HumanResources.Entities.Attendance source);
    [MapperIgnoreTarget(nameof(HumanResources.AttendanceDto.EmployeeName))]
    public override partial void Map(HumanResources.Entities.Attendance source, HumanResources.AttendanceDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ShiftTypeMapper : MapperBase<HumanResources.Entities.ShiftType, HumanResources.ShiftTypeDto>
{
    public override partial HumanResources.ShiftTypeDto Map(HumanResources.Entities.ShiftType source);
    public override partial void Map(HumanResources.Entities.ShiftType source, HumanResources.ShiftTypeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ShiftAssignmentMapper : MapperBase<HumanResources.Entities.ShiftAssignment, HumanResources.ShiftAssignmentDto>
{
    [MapperIgnoreTarget(nameof(HumanResources.ShiftAssignmentDto.EmployeeName))]
    [MapperIgnoreTarget(nameof(HumanResources.ShiftAssignmentDto.ShiftTypeName))]
    public override partial HumanResources.ShiftAssignmentDto Map(HumanResources.Entities.ShiftAssignment source);
    [MapperIgnoreTarget(nameof(HumanResources.ShiftAssignmentDto.EmployeeName))]
    [MapperIgnoreTarget(nameof(HumanResources.ShiftAssignmentDto.ShiftTypeName))]
    public override partial void Map(HumanResources.Entities.ShiftAssignment source, HumanResources.ShiftAssignmentDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PayrollEntryMapper : MapperBase<HumanResources.Entities.PayrollEntry, HumanResources.PayrollEntryDto>
{
    public override partial HumanResources.PayrollEntryDto Map(HumanResources.Entities.PayrollEntry source);
    public override partial void Map(HumanResources.Entities.PayrollEntry source, HumanResources.PayrollEntryDto destination);
    private partial HumanResources.PayrollEntryLineDto MapChild(HumanResources.Entities.PayrollEntryLine source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SalaryStructureMapper : MapperBase<HumanResources.Entities.SalaryStructure, HumanResources.SalaryStructureDto>
{
    public override partial HumanResources.SalaryStructureDto Map(HumanResources.Entities.SalaryStructure source);
    public override partial void Map(HumanResources.Entities.SalaryStructure source, HumanResources.SalaryStructureDto destination);
    private partial HumanResources.SalaryStructureDetailDto MapChild(HumanResources.Entities.SalaryStructureDetail source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ItemAttributeMapper : MapperBase<Inventory.Entities.ItemAttribute, Inventory.ItemAttributeDto>
{
    [MapProperty(nameof(Inventory.Entities.ItemAttribute.AttributeName), nameof(Inventory.ItemAttributeDto.Name))]
    public override partial Inventory.ItemAttributeDto Map(Inventory.Entities.ItemAttribute source);
    [MapProperty(nameof(Inventory.Entities.ItemAttribute.AttributeName), nameof(Inventory.ItemAttributeDto.Name))]
    public override partial void Map(Inventory.Entities.ItemAttribute source, Inventory.ItemAttributeDto destination);
    [MapProperty(nameof(Inventory.Entities.ItemAttributeValue.AttributeValue), nameof(Inventory.ItemAttributeValueDto.Value))]
    private partial Inventory.ItemAttributeValueDto MapChild(Inventory.Entities.ItemAttributeValue source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LandedCostVoucherMapper : MapperBase<Inventory.Entities.LandedCostVoucher, Dtos.LandedCostVoucherDto>
{
    public override partial Dtos.LandedCostVoucherDto Map(Inventory.Entities.LandedCostVoucher source);
    public override partial void Map(Inventory.Entities.LandedCostVoucher source, Dtos.LandedCostVoucherDto destination);
    private partial Dtos.LandedCostItemDto MapItem(Inventory.Entities.LandedCostItem source);
    private partial Dtos.LandedCostChargeDto MapCharge(Inventory.Entities.LandedCostCharge source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PickListMapper : MapperBase<Inventory.Entities.PickList, Inventory.PickListDto>
{
    public override partial Inventory.PickListDto Map(Inventory.Entities.PickList source);
    public override partial void Map(Inventory.Entities.PickList source, Inventory.PickListDto destination);
    private partial Inventory.PickListItemDto MapChild(Inventory.Entities.PickListItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class QualityInspectionMapper : MapperBase<Inventory.Entities.QualityInspection, Dtos.QualityInspectionDto>
{
    public override partial Dtos.QualityInspectionDto Map(Inventory.Entities.QualityInspection source);
    public override partial void Map(Inventory.Entities.QualityInspection source, Dtos.QualityInspectionDto destination);
    private partial Dtos.QualityInspectionReadingDto MapChild(Inventory.Entities.QualityInspectionReading source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class StockReconciliationMapper : MapperBase<Inventory.Entities.StockReconciliation, Dtos.StockReconciliationDto>
{
    public override partial Dtos.StockReconciliationDto Map(Inventory.Entities.StockReconciliation source);
    public override partial void Map(Inventory.Entities.StockReconciliation source, Dtos.StockReconciliationDto destination);
    private partial Dtos.StockReconciliationItemDto MapChild(Inventory.Entities.StockReconciliationItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class JobCardMapper : MapperBase<Manufacturing.Entities.JobCard, Manufacturing.JobCardDto>
{
    public override partial Manufacturing.JobCardDto Map(Manufacturing.Entities.JobCard source);
    public override partial void Map(Manufacturing.Entities.JobCard source, Manufacturing.JobCardDto destination);
    private partial Manufacturing.JobCardTimeLogDto MapChild(Manufacturing.Entities.JobCardTimeLog source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class RoutingMapper : MapperBase<Manufacturing.Entities.Routing, Manufacturing.RoutingDto>
{
    public override partial Manufacturing.RoutingDto Map(Manufacturing.Entities.Routing source);
    public override partial void Map(Manufacturing.Entities.Routing source, Manufacturing.RoutingDto destination);
    private partial Manufacturing.RoutingOperationDto MapChild(Manufacturing.Entities.RoutingOperation source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ProductionPlanMapper : MapperBase<Manufacturing.Entities.ProductionPlan, Manufacturing.ProductionPlanDto>
{
    public override partial Manufacturing.ProductionPlanDto Map(Manufacturing.Entities.ProductionPlan source);
    public override partial void Map(Manufacturing.Entities.ProductionPlan source, Manufacturing.ProductionPlanDto destination);
    private partial Manufacturing.ProductionPlanItemDto MapItem(Manufacturing.Entities.ProductionPlanItem source);
    private partial Manufacturing.ProductionPlanMrItemDto MapMrItem(Manufacturing.Entities.ProductionPlanMrItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class MasterProductionScheduleMapper : MapperBase<Manufacturing.Entities.MasterProductionSchedule, Manufacturing.MasterProductionScheduleDto>
{
    public override partial Manufacturing.MasterProductionScheduleDto Map(Manufacturing.Entities.MasterProductionSchedule source);
    public override partial void Map(Manufacturing.Entities.MasterProductionSchedule source, Manufacturing.MasterProductionScheduleDto destination);
    private partial Manufacturing.MpsSalesOrderRefDto MapSalesOrderRef(Manufacturing.Entities.MpsSalesOrderRef source);
    private partial Manufacturing.MpsMaterialRequestRefDto MapMaterialRequestRef(Manufacturing.Entities.MpsMaterialRequestRef source);
    private partial Manufacturing.MasterProductionScheduleItemDto MapItem(Manufacturing.Entities.MasterProductionScheduleItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SalesForecastMapper : MapperBase<Manufacturing.Entities.SalesForecast, Manufacturing.SalesForecastDto>
{
    [MapperIgnoreTarget(nameof(Manufacturing.SalesForecastDto.SelectedItemIds))]
    public override partial Manufacturing.SalesForecastDto Map(Manufacturing.Entities.SalesForecast source);
    [MapperIgnoreTarget(nameof(Manufacturing.SalesForecastDto.SelectedItemIds))]
    public override partial void Map(Manufacturing.Entities.SalesForecast source, Manufacturing.SalesForecastDto destination);
    private partial Manufacturing.SalesForecastItemDto MapForecastItem(Manufacturing.Entities.SalesForecastItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TimesheetMapper : MapperBase<Projects.Entities.Timesheet, Projects.TimesheetDto>
{
    public override partial Projects.TimesheetDto Map(Projects.Entities.Timesheet source);
    public override partial void Map(Projects.Entities.Timesheet source, Projects.TimesheetDto destination);
    private partial Projects.TimesheetDetailDto MapChild(Projects.Entities.TimesheetDetail source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class MaterialRequestMapper : MapperBase<Purchasing.Entities.MaterialRequest, Purchasing.DTOs.MaterialRequestDto>
{
    public override partial Purchasing.DTOs.MaterialRequestDto Map(Purchasing.Entities.MaterialRequest source);
    public override partial void Map(Purchasing.Entities.MaterialRequest source, Purchasing.DTOs.MaterialRequestDto destination);
    private partial Purchasing.DTOs.MaterialRequestItemDto MapChild(Purchasing.Entities.MaterialRequestItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PurchaseInvoiceMapper : MapperBase<Purchasing.Entities.PurchaseInvoice, Purchasing.PurchaseInvoiceDto>
{
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.SupplierName))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.DaysOverdue))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.IsOverdue))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.MatchingStatus))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.IsReadyForPayment))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.InterCompanyInvoiceNumber))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.InterCompanyCompanyName))]
    public override partial Purchasing.PurchaseInvoiceDto Map(Purchasing.Entities.PurchaseInvoice source);
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.SupplierName))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.DaysOverdue))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.IsOverdue))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.MatchingStatus))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.IsReadyForPayment))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.InterCompanyInvoiceNumber))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseInvoiceDto.InterCompanyCompanyName))]
    public override partial void Map(Purchasing.Entities.PurchaseInvoice source, Purchasing.PurchaseInvoiceDto destination);
    private partial Purchasing.PurchaseInvoiceItemDto MapChild(Purchasing.Entities.PurchaseInvoiceItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PurchaseOrderMapper : MapperBase<Purchasing.Entities.PurchaseOrder, Purchasing.PurchaseOrderDto>
{
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseOrderDto.SupplierName))]
    public override partial Purchasing.PurchaseOrderDto Map(Purchasing.Entities.PurchaseOrder source);
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseOrderDto.SupplierName))]
    public override partial void Map(Purchasing.Entities.PurchaseOrder source, Purchasing.PurchaseOrderDto destination);
    private partial Purchasing.PurchaseOrderItemDto MapChild(Purchasing.Entities.PurchaseOrderItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PurchaseReceiptMapper : MapperBase<Purchasing.Entities.PurchaseReceipt, Purchasing.PurchaseReceiptDto>
{
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseReceiptDto.SupplierName))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseReceiptDto.WarehouseName))]
    public override partial Purchasing.PurchaseReceiptDto Map(Purchasing.Entities.PurchaseReceipt source);
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseReceiptDto.SupplierName))]
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseReceiptDto.WarehouseName))]
    public override partial void Map(Purchasing.Entities.PurchaseReceipt source, Purchasing.PurchaseReceiptDto destination);
    [MapperIgnoreTarget(nameof(Purchasing.PurchaseReceiptItemDto.ItemName))]
    private partial Purchasing.PurchaseReceiptItemDto MapChild(Purchasing.Entities.PurchaseReceiptItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class RequestForQuotationMapper : MapperBase<Purchasing.Entities.RequestForQuotation, Purchasing.RfqDto>
{
    public override partial Purchasing.RfqDto Map(Purchasing.Entities.RequestForQuotation source);
    public override partial void Map(Purchasing.Entities.RequestForQuotation source, Purchasing.RfqDto destination);
    private partial Purchasing.RfqItemDto MapItem(Purchasing.Entities.RfqItem source);
    private partial Purchasing.RfqSupplierDto MapSupplier(Purchasing.Entities.RfqSupplier source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SupplierQuotationMapper : MapperBase<Purchasing.Entities.SupplierQuotation, Purchasing.SupplierQuotationDto>
{
    public override partial Purchasing.SupplierQuotationDto Map(Purchasing.Entities.SupplierQuotation source);
    public override partial void Map(Purchasing.Entities.SupplierQuotation source, Purchasing.SupplierQuotationDto destination);
    private partial Purchasing.SupplierQuotationItemDto MapChild(Purchasing.Entities.SupplierQuotationItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BlanketOrderMapper : MapperBase<Sales.Entities.BlanketOrder, Sales.BlanketOrderDto>
{
    public override partial Sales.BlanketOrderDto Map(Sales.Entities.BlanketOrder source);
    public override partial void Map(Sales.Entities.BlanketOrder source, Sales.BlanketOrderDto destination);
    private partial Sales.BlanketOrderItemDto MapChild(Sales.Entities.BlanketOrderItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DeliveryNoteMapper : MapperBase<Sales.Entities.DeliveryNote, Sales.DeliveryNoteDto>
{
    [MapperIgnoreTarget(nameof(Sales.DeliveryNoteDto.CustomerName))]
    public override partial Sales.DeliveryNoteDto Map(Sales.Entities.DeliveryNote source);
    [MapperIgnoreTarget(nameof(Sales.DeliveryNoteDto.CustomerName))]
    public override partial void Map(Sales.Entities.DeliveryNote source, Sales.DeliveryNoteDto destination);
    private partial Sales.DeliveryNoteItemDto MapChild(Sales.Entities.DeliveryNoteItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class InstallationNoteMapper : MapperBase<Sales.Entities.InstallationNote, Sales.InstallationNoteDto>
{
    public override partial Sales.InstallationNoteDto Map(Sales.Entities.InstallationNote source);
    public override partial void Map(Sales.Entities.InstallationNote source, Sales.InstallationNoteDto destination);
    private partial Sales.InstallationNoteItemDto MapChild(Sales.Entities.InstallationNoteItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LoyaltyProgramMapper : MapperBase<Sales.Entities.LoyaltyProgram, Sales.LoyaltyProgramDto>
{
    public override partial Sales.LoyaltyProgramDto Map(Sales.Entities.LoyaltyProgram source);
    public override partial void Map(Sales.Entities.LoyaltyProgram source, Sales.LoyaltyProgramDto destination);
    private partial Sales.LoyaltyProgramTierDto MapChild(Sales.Entities.LoyaltyProgramTier source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PosClosingEntryMapper : MapperBase<Sales.Entities.PosClosingEntry, Sales.PosClosingDto>
{
    public override partial Sales.PosClosingDto Map(Sales.Entities.PosClosingEntry source);
    public override partial void Map(Sales.Entities.PosClosingEntry source, Sales.PosClosingDto destination);
    private partial Sales.PosClosingPaymentDto MapPayment(Sales.Entities.PosClosingPayment source);
    private partial Sales.PosClosingInvoiceDto MapInvoice(Sales.Entities.PosClosingInvoice source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ProductBundleMapper : MapperBase<Sales.Entities.ProductBundle, Sales.ProductBundleDto>
{
    public override partial Sales.ProductBundleDto Map(Sales.Entities.ProductBundle source);
    public override partial void Map(Sales.Entities.ProductBundle source, Sales.ProductBundleDto destination);
    private partial Sales.ProductBundleItemDto MapChild(Sales.Entities.ProductBundleItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SalesPersonMapper : MapperBase<Sales.Entities.SalesPerson, Sales.SalesPersonDto>
{
    public override partial Sales.SalesPersonDto Map(Sales.Entities.SalesPerson source);
    public override partial void Map(Sales.Entities.SalesPerson source, Sales.SalesPersonDto destination);
    private partial Sales.SalesTargetDto MapChild(Sales.Entities.SalesPersonTarget source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SubscriptionMapper : MapperBase<Sales.Entities.Subscription, Sales.SubscriptionDto>
{
    [MapperIgnoreTarget(nameof(Sales.SubscriptionDto.FinalProratedInvoice))]
    public override partial Sales.SubscriptionDto Map(Sales.Entities.Subscription source);
    [MapperIgnoreTarget(nameof(Sales.SubscriptionDto.FinalProratedInvoice))]
    public override partial void Map(Sales.Entities.Subscription source, Sales.SubscriptionDto destination);
    private partial Sales.SubscriptionPlanDto MapChild(Sales.Entities.SubscriptionPlan source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ItemTaxTemplateMapper : MapperBase<Tax.Entities.ItemTaxTemplate, Tax.ItemTaxTemplateDto>
{
    public override partial Tax.ItemTaxTemplateDto Map(Tax.Entities.ItemTaxTemplate source);
    public override partial void Map(Tax.Entities.ItemTaxTemplate source, Tax.ItemTaxTemplateDto destination);
    private partial Tax.ItemTaxTemplateDetailDto MapChild(Tax.Entities.ItemTaxTemplateDetail source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TaxWithholdingCategoryMapper : MapperBase<Tax.Entities.TaxWithholdingCategory, Tax.TaxWithholdingCategoryDto>
{
    public override partial Tax.TaxWithholdingCategoryDto Map(Tax.Entities.TaxWithholdingCategory source);
    public override partial void Map(Tax.Entities.TaxWithholdingCategory source, Tax.TaxWithholdingCategoryDto destination);
    private partial Tax.TaxWithholdingRateDto MapChild(Tax.Entities.TaxWithholdingRate source);
    private partial Tax.TaxWithholdingAccountDto MapChild(Tax.Entities.TaxWithholdingAccount source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LowerDeductionCertificateMapper : MapperBase<Tax.Entities.LowerDeductionCertificate, Tax.LowerDeductionCertificateDto>
{
    [MapperIgnoreTarget(nameof(Tax.LowerDeductionCertificateDto.SupplierName))]
    [MapperIgnoreTarget(nameof(Tax.LowerDeductionCertificateDto.TaxWithholdingCategoryName))]
    public override partial Tax.LowerDeductionCertificateDto Map(Tax.Entities.LowerDeductionCertificate source);
    [MapperIgnoreTarget(nameof(Tax.LowerDeductionCertificateDto.SupplierName))]
    [MapperIgnoreTarget(nameof(Tax.LowerDeductionCertificateDto.TaxWithholdingCategoryName))]
    public override partial void Map(Tax.Entities.LowerDeductionCertificate source, Tax.LowerDeductionCertificateDto destination);
}

// ━━━ COMPLEX mappers (AfterMap for computed props, [MapProperty] for renames) ━━━

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PaymentEntryMapper : MapperBase<Accounting.Entities.PaymentEntry, Accounting.PaymentEntryDto>
{
    [MapperIgnoreTarget(nameof(Accounting.PaymentEntryDto.PartyName))]
    public override partial Accounting.PaymentEntryDto Map(Accounting.Entities.PaymentEntry source);
    [MapperIgnoreTarget(nameof(Accounting.PaymentEntryDto.PartyName))]
    public override partial void Map(Accounting.Entities.PaymentEntry source, Accounting.PaymentEntryDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PeriodClosingVoucherMapper : MapperBase<Accounting.Entities.PeriodClosingVoucher, Accounting.PeriodClosingVoucherDto>
{
    [MapperIgnoreTarget(nameof(Accounting.PeriodClosingVoucherDto.EntryCount))]
    [MapperIgnoreTarget(nameof(Accounting.PeriodClosingVoucherDto.ClosingAccountName))]
    public override partial Accounting.PeriodClosingVoucherDto Map(Accounting.Entities.PeriodClosingVoucher source);
    [MapperIgnoreTarget(nameof(Accounting.PeriodClosingVoucherDto.EntryCount))]
    [MapperIgnoreTarget(nameof(Accounting.PeriodClosingVoucherDto.ClosingAccountName))]
    public override partial void Map(Accounting.Entities.PeriodClosingVoucher source, Accounting.PeriodClosingVoucherDto destination);

    public override void AfterMap(Accounting.Entities.PeriodClosingVoucher source, Accounting.PeriodClosingVoucherDto destination)
        => destination.EntryCount = source.Entries.Count;
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AssetCapitalizationMapper : MapperBase<Assets.Entities.AssetCapitalization, Assets.AssetCapitalizationDto>
{
    [MapperIgnoreTarget(nameof(Assets.AssetCapitalizationDto.StockItems))]
    [MapperIgnoreTarget(nameof(Assets.AssetCapitalizationDto.ServiceItems))]
    public override partial Assets.AssetCapitalizationDto Map(Assets.Entities.AssetCapitalization source);
    [MapperIgnoreTarget(nameof(Assets.AssetCapitalizationDto.StockItems))]
    [MapperIgnoreTarget(nameof(Assets.AssetCapitalizationDto.ServiceItems))]
    public override partial void Map(Assets.Entities.AssetCapitalization source, Assets.AssetCapitalizationDto destination);
    private partial Assets.AssetCapitalizationAssetItemDto MapCapAsset(Assets.Entities.AssetCapitalizationAsset source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class NotificationLogMapper : MapperBase<Core.Entities.NotificationLog, Core.NotificationLogDto>
{
    [MapProperty(nameof(Core.Entities.NotificationLog.CreationTime), nameof(Core.NotificationLogDto.CreatedAt))]
    public override partial Core.NotificationLogDto Map(Core.Entities.NotificationLog source);
    [MapProperty(nameof(Core.Entities.NotificationLog.CreationTime), nameof(Core.NotificationLogDto.CreatedAt))]
    public override partial void Map(Core.Entities.NotificationLog source, Core.NotificationLogDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LoanMapper : MapperBase<HumanResources.Entities.Loan, HumanResources.LoanDto>
{
    [MapProperty(nameof(HumanResources.Entities.Loan.RepaymentSchedule), nameof(HumanResources.LoanDto.Schedule))]
    public override partial HumanResources.LoanDto Map(HumanResources.Entities.Loan source);
    [MapProperty(nameof(HumanResources.Entities.Loan.RepaymentSchedule), nameof(HumanResources.LoanDto.Schedule))]
    public override partial void Map(HumanResources.Entities.Loan source, HumanResources.LoanDto destination);

    [MapProperty(nameof(HumanResources.Entities.LoanRepaymentSchedule.OutstandingAfterPayment), nameof(HumanResources.LoanRepaymentScheduleDto.OutstandingBalance))]
    private partial HumanResources.LoanRepaymentScheduleDto MapChild(HumanResources.Entities.LoanRepaymentSchedule source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BatchMapper : MapperBase<Inventory.Entities.Batch, Inventory.BatchDto>
{
    [MapperIgnoreTarget(nameof(Inventory.BatchDto.IsExpired))]
    public override partial Inventory.BatchDto Map(Inventory.Entities.Batch source);
    [MapperIgnoreTarget(nameof(Inventory.BatchDto.IsExpired))]
    public override partial void Map(Inventory.Entities.Batch source, Inventory.BatchDto destination);

    public override void AfterMap(Inventory.Entities.Batch source, Inventory.BatchDto destination)
        => destination.IsExpired = source.IsExpired();
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ProjectMapper : MapperBase<Projects.Entities.Project, Projects.ProjectDto>
{
    [MapperIgnoreTarget(nameof(Projects.ProjectDto.TaskCount))]
    [MapperIgnoreTarget(nameof(Projects.ProjectDto.Users))]
    public override partial Projects.ProjectDto Map(Projects.Entities.Project source);
    [MapperIgnoreTarget(nameof(Projects.ProjectDto.TaskCount))]
    [MapperIgnoreTarget(nameof(Projects.ProjectDto.Users))]
    public override partial void Map(Projects.Entities.Project source, Projects.ProjectDto destination);

    public override void AfterMap(Projects.Entities.Project source, Projects.ProjectDto destination)
    {
        destination.TaskCount = source.Tasks.Count;
        destination.Users = source.Users.Select(u => new Projects.ProjectUserDto
        {
            Id = u.Id,
            ProjectId = u.ProjectId,
            UserId = u.UserId,
            ViewAttachments = u.ViewAttachments,
            HideTimesheets = u.HideTimesheets,
        }).ToList();
    }
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SupplierScorecardMapper : MapperBase<Purchasing.Entities.SupplierScorecard, Purchasing.ScorecardDto>
{
    public override partial Purchasing.ScorecardDto Map(Purchasing.Entities.SupplierScorecard source);
    public override partial void Map(Purchasing.Entities.SupplierScorecard source, Purchasing.ScorecardDto destination);

    [MapProperty(nameof(Purchasing.Entities.ScorecardStanding.MinGrade), nameof(Purchasing.ScorecardStandingDto.MinScore))]
    [MapProperty(nameof(Purchasing.Entities.ScorecardStanding.MaxGrade), nameof(Purchasing.ScorecardStandingDto.MaxScore))]
    private partial Purchasing.ScorecardStandingDto MapStanding(Purchasing.Entities.ScorecardStanding source);
    private partial Purchasing.ScorecardCriterionDto MapCriterion(Purchasing.Entities.ScorecardCriterion source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DunningMapper : MapperBase<Sales.Entities.Dunning, Sales.DunningDto>
{
    [MapperIgnoreTarget(nameof(Sales.DunningDto.OverduePaymentCount))]
    [MapperIgnoreTarget(nameof(Sales.DunningDto.OverduePayments))]
    public override partial Sales.DunningDto Map(Sales.Entities.Dunning source);
    [MapperIgnoreTarget(nameof(Sales.DunningDto.OverduePaymentCount))]
    [MapperIgnoreTarget(nameof(Sales.DunningDto.OverduePayments))]
    public override partial void Map(Sales.Entities.Dunning source, Sales.DunningDto destination);

    public override void AfterMap(Sales.Entities.Dunning source, Sales.DunningDto destination)
        => destination.OverduePaymentCount = source.OverduePayments.Count;
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DunningTypeMapper : MapperBase<Sales.Entities.DunningType, Sales.DunningTypeDto>
{
    public override partial Sales.DunningTypeDto Map(Sales.Entities.DunningType source);
    public override partial void Map(Sales.Entities.DunningType source, Sales.DunningTypeDto destination);
    private partial Sales.DunningLetterTextDto MapLetterText(Sales.Entities.DunningLetterText source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class WorkstationMapper : MapperBase<Manufacturing.Entities.Workstation, Manufacturing.WorkstationDto>
{
    [MapperIgnoreTarget(nameof(Manufacturing.WorkstationDto.Costs))]
    [MapperIgnoreTarget(nameof(Manufacturing.WorkstationDto.WorkingHours))]
    public override partial Manufacturing.WorkstationDto Map(Manufacturing.Entities.Workstation source);
    [MapperIgnoreTarget(nameof(Manufacturing.WorkstationDto.Costs))]
    [MapperIgnoreTarget(nameof(Manufacturing.WorkstationDto.WorkingHours))]
    public override partial void Map(Manufacturing.Entities.Workstation source, Manufacturing.WorkstationDto destination);

    public override void AfterMap(Manufacturing.Entities.Workstation source, Manufacturing.WorkstationDto destination)
    {
        destination.Costs = source.Costs.Select(c => new Manufacturing.WorkstationCostDto
        {
            Name = c.OperatingComponent, Amount = c.OperatingCost
        }).ToArray();
        destination.WorkingHours = source.WorkingHours.Select(w => new Manufacturing.WorkstationWorkingHourDto
        {
            DayOfWeek = w.Day, StartTime = w.StartTime.ToString(@"hh\:mm"), EndTime = w.EndTime.ToString(@"hh\:mm")
        }).ToArray();
    }
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ShippingRuleMapper : MapperBase<Sales.Entities.ShippingRule, Sales.ShippingRuleDto>
{
    [MapProperty(nameof(Sales.Entities.ShippingRule.FixedAmount), nameof(Sales.ShippingRuleDto.ShippingAmount))]
    [MapperIgnoreTarget(nameof(Sales.ShippingRuleDto.CompanyId))]
    [MapperIgnoreTarget(nameof(Sales.ShippingRuleDto.Countries))]
    public override partial Sales.ShippingRuleDto Map(Sales.Entities.ShippingRule source);
    [MapProperty(nameof(Sales.Entities.ShippingRule.FixedAmount), nameof(Sales.ShippingRuleDto.ShippingAmount))]
    [MapperIgnoreTarget(nameof(Sales.ShippingRuleDto.CompanyId))]
    [MapperIgnoreTarget(nameof(Sales.ShippingRuleDto.Countries))]
    public override partial void Map(Sales.Entities.ShippingRule source, Sales.ShippingRuleDto destination);

    private partial Sales.ShippingConditionDto MapCondition(Sales.Entities.ShippingRuleCondition source);

    public override void AfterMap(Sales.Entities.ShippingRule source, Sales.ShippingRuleDto destination)
    {
        destination.CompanyId = source.CompanyId ?? default;
        destination.Countries = source.Countries.Select(c => c.CountryCode).ToList();
    }
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AgingReportMapper : MapperBase<Accounting.DomainServices.AgingReport, Accounting.AgingReportDto>
{
    [MapperIgnoreTarget(nameof(Accounting.AgingReportDto.BucketLabels))]
    public override partial Accounting.AgingReportDto Map(Accounting.DomainServices.AgingReport source);
    [MapperIgnoreTarget(nameof(Accounting.AgingReportDto.BucketLabels))]
    public override partial void Map(Accounting.DomainServices.AgingReport source, Accounting.AgingReportDto destination);

    public override void AfterMap(Accounting.DomainServices.AgingReport source, Accounting.AgingReportDto destination)
    {
        var ranges = source.BucketRanges;
        var labels = new string[ranges.Length + 1];
        labels[0] = $"0-{ranges[0]}";
        for (int i = 1; i < ranges.Length; i++)
            labels[i] = $"{ranges[i - 1] + 1}-{ranges[i]}";
        labels[ranges.Length] = $"{ranges[^1]}+";
        destination.BucketLabels = labels;
    }
}

// ━━━ MISSING MAPPERS — CrudAppService + manual MapToDto replacements ━━━

// CrudAppService mappers (required for Get/GetList/Create/Update to work at runtime)

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AccountMapper : MapperBase<Accounting.Entities.Account, Accounting.AccountDto>
{
    public override partial Accounting.AccountDto Map(Accounting.Entities.Account source);
    public override partial void Map(Accounting.Entities.Account source, Accounting.AccountDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BranchMapper : MapperBase<Core.Entities.Branch, Core.BranchDto>
{
    public override partial Core.BranchDto Map(Core.Entities.Branch source);
    public override partial void Map(Core.Entities.Branch source, Core.BranchDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class CompanyMapper : MapperBase<Core.Entities.Company, Core.CompanyDto>
{
    public override partial Core.CompanyDto Map(Core.Entities.Company source);
    public override partial void Map(Core.Entities.Company source, Core.CompanyDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class WarehouseMapper : MapperBase<Inventory.Entities.Warehouse, Inventory.WarehouseDto>
{
    public override partial Inventory.WarehouseDto Map(Inventory.Entities.Warehouse source);
    public override partial void Map(Inventory.Entities.Warehouse source, Inventory.WarehouseDto destination);
}

// Manual MapToDto replacement mappers

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AppNotificationMapper : MapperBase<Notification.Entities.AppNotification, Notification.DTOs.AppNotificationDto>
{
    public override partial Notification.DTOs.AppNotificationDto Map(Notification.Entities.AppNotification source);
    public override partial void Map(Notification.Entities.AppNotification source, Notification.DTOs.AppNotificationDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LeadMapper : MapperBase<CRM.Entities.Lead, CRM.LeadDto>
{
    [MapperIgnoreTarget(nameof(CRM.LeadDto.FullName))]
    public override partial CRM.LeadDto Map(CRM.Entities.Lead source);
    [MapperIgnoreTarget(nameof(CRM.LeadDto.FullName))]
    public override partial void Map(CRM.Entities.Lead source, CRM.LeadDto destination);

    public override void AfterMap(CRM.Entities.Lead source, CRM.LeadDto destination)
        => destination.FullName = source.GetFullName();
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SalesInvoiceMapper : MapperBase<Sales.Entities.SalesInvoice, Sales.SalesInvoiceDto>
{
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.CustomerName))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.DaysOverdue))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.IsOverdue))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.SalesTeam))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.TotalCommission))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.InterCompanyPurchaseInvoiceNumber))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.InterCompanyCompanyName))]
    public override partial Sales.SalesInvoiceDto Map(Sales.Entities.SalesInvoice source);
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.CustomerName))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.DaysOverdue))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.IsOverdue))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.SalesTeam))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.TotalCommission))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.InterCompanyPurchaseInvoiceNumber))]
    [MapperIgnoreTarget(nameof(Sales.SalesInvoiceDto.InterCompanyCompanyName))]
    public override partial void Map(Sales.Entities.SalesInvoice source, Sales.SalesInvoiceDto destination);
    private partial Sales.SalesInvoiceItemDto MapChild(Sales.Entities.SalesInvoiceItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SalesOrderMapper : MapperBase<Sales.Entities.SalesOrder, Sales.SalesOrderDto>
{
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.CustomerName))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.OverdueWarning))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.FirstDeliveryDate))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.LastDeliveryDate))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.FirstBilledDate))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.FirstPaymentDate))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.SalesTeam))]
    public override partial Sales.SalesOrderDto Map(Sales.Entities.SalesOrder source);
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.CustomerName))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.OverdueWarning))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.FirstDeliveryDate))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.LastDeliveryDate))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.FirstBilledDate))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.FirstPaymentDate))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderDto.SalesTeam))]
    public override partial void Map(Sales.Entities.SalesOrder source, Sales.SalesOrderDto destination);
    [MapperIgnoreTarget(nameof(Sales.SalesOrderItemDto.AvailableQty))]
    [MapperIgnoreTarget(nameof(Sales.SalesOrderItemDto.IsInsufficientStock))]
    private partial Sales.SalesOrderItemDto MapChild(Sales.Entities.SalesOrderItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class QuotationMapper : MapperBase<Sales.Entities.Quotation, Sales.QuotationDto>
{
    [MapperIgnoreTarget(nameof(Sales.QuotationDto.CustomerName))]
    [MapperIgnoreTarget(nameof(Sales.QuotationDto.Competitors))]
    public override partial Sales.QuotationDto Map(Sales.Entities.Quotation source);
    [MapperIgnoreTarget(nameof(Sales.QuotationDto.CustomerName))]
    [MapperIgnoreTarget(nameof(Sales.QuotationDto.Competitors))]
    public override partial void Map(Sales.Entities.Quotation source, Sales.QuotationDto destination);
    private partial Sales.QuotationItemDto MapChild(Sales.Entities.QuotationItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class StockEntryMapper : MapperBase<Inventory.Entities.StockEntry, Inventory.StockEntryDto>
{
    public override partial Inventory.StockEntryDto Map(Inventory.Entities.StockEntry source);
    public override partial void Map(Inventory.Entities.StockEntry source, Inventory.StockEntryDto destination);
    [MapperIgnoreTarget(nameof(Inventory.StockEntryItemDto.ItemName))]
    [MapperIgnoreTarget(nameof(Inventory.StockEntryItemDto.SourceWarehouseName))]
    [MapperIgnoreTarget(nameof(Inventory.StockEntryItemDto.TargetWarehouseName))]
    private partial Inventory.StockEntryItemDto MapChild(Inventory.Entities.StockEntryItem source);
}

// ─── Manufacturing ─── (BOM + WorkOrder with child collections)

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BomMapper : MapperBase<Manufacturing.Entities.BillOfMaterials, Manufacturing.BomDto>
{
    [MapperIgnoreTarget(nameof(Manufacturing.BomDto.ItemName))]
    public override partial Manufacturing.BomDto Map(Manufacturing.Entities.BillOfMaterials source);
    [MapperIgnoreTarget(nameof(Manufacturing.BomDto.ItemName))]
    public override partial void Map(Manufacturing.Entities.BillOfMaterials source, Manufacturing.BomDto destination);
    private partial Manufacturing.BomItemDto MapChild(Manufacturing.Entities.BomItem source);
    private partial Manufacturing.BomOperationDto MapChild(Manufacturing.Entities.BomOperation source);
    private partial Manufacturing.BomSecondaryItemDto MapChild(Manufacturing.Entities.BomSecondaryItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class WorkOrderMapper : MapperBase<Manufacturing.Entities.WorkOrder, Manufacturing.WorkOrderDto>
{
    [MapperIgnoreTarget(nameof(Manufacturing.WorkOrderDto.ItemName))]
    public override partial Manufacturing.WorkOrderDto Map(Manufacturing.Entities.WorkOrder source);
    [MapperIgnoreTarget(nameof(Manufacturing.WorkOrderDto.ItemName))]
    public override partial void Map(Manufacturing.Entities.WorkOrder source, Manufacturing.WorkOrderDto destination);
    private partial Manufacturing.WorkOrderItemDto MapChild(Manufacturing.Entities.WorkOrderItem source);
}

// ─── Assets ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AssetCategoryMapper : MapperBase<Assets.Entities.AssetCategory, Assets.AssetCategoryDto>
{
    public override partial Assets.AssetCategoryDto Map(Assets.Entities.AssetCategory source);
    public override partial void Map(Assets.Entities.AssetCategory source, Assets.AssetCategoryDto destination);
    private partial Assets.AssetCategoryAccountDto MapCategoryAccount(Assets.Entities.AssetCategoryAccount source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class LocationMapper : MapperBase<Assets.Entities.Location, Assets.LocationDto>
{
    public override partial Assets.LocationDto Map(Assets.Entities.Location source);
    public override partial void Map(Assets.Entities.Location source, Assets.LocationDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class DrivingLicenseCategoryMapper : MapperBase<Assets.Entities.DrivingLicenseCategory, Assets.DrivingLicenseCategoryDto>
{
    public override partial Assets.DrivingLicenseCategoryDto Map(Assets.Entities.DrivingLicenseCategory source);
    public override partial void Map(Assets.Entities.DrivingLicenseCategory source, Assets.DrivingLicenseCategoryDto destination);
}

// ─── Automation ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AutomationExecutionLogMapper : MapperBase<Automation.Entities.AutomationExecutionLog, Automation.DTOs.AutomationExecutionLogDto>
{
    [MapperIgnoreTarget(nameof(Automation.DTOs.AutomationExecutionLogDto.RuleName))]
    public override partial Automation.DTOs.AutomationExecutionLogDto Map(Automation.Entities.AutomationExecutionLog source);
    [MapperIgnoreTarget(nameof(Automation.DTOs.AutomationExecutionLogDto.RuleName))]
    public override partial void Map(Automation.Entities.AutomationExecutionLog source, Automation.DTOs.AutomationExecutionLogDto destination);
}

// ━━━ Remaining entity→DTO mappers (replacing manual new Dto { } patterns) ━━━

// ─── Accounting (config entities) ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ModeOfPaymentMapper : MapperBase<Accounting.Entities.ModeOfPayment, Accounting.ModeOfPaymentDto>
{
    public override partial Accounting.ModeOfPaymentDto Map(Accounting.Entities.ModeOfPayment source);
    public override partial void Map(Accounting.Entities.ModeOfPayment source, Accounting.ModeOfPaymentDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UomConversionMapper : MapperBase<Inventory.Entities.UomConversion, Inventory.UomConversionDto>
{
    public override partial Inventory.UomConversionDto Map(Inventory.Entities.UomConversion source);
    public override partial void Map(Inventory.Entities.UomConversion source, Inventory.UomConversionDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AccountingDimensionFilterMapper : MapperBase<Accounting.Entities.AccountingDimensionFilter, Accounting.AccountingDimensionFilterDto>
{
    public override partial Accounting.AccountingDimensionFilterDto Map(Accounting.Entities.AccountingDimensionFilter source);
    public override partial void Map(Accounting.Entities.AccountingDimensionFilter source, Accounting.AccountingDimensionFilterDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PaymentScheduleEntryMapper : MapperBase<Accounting.Entities.PaymentScheduleEntry, Sales.PaymentScheduleDto>
{
    public override partial Sales.PaymentScheduleDto Map(Accounting.Entities.PaymentScheduleEntry source);
    public override partial void Map(Accounting.Entities.PaymentScheduleEntry source, Sales.PaymentScheduleDto destination);
}

// ─── Core ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DocumentSeriesMapper : MapperBase<Core.Entities.DocumentSeries, Core.DocumentSeriesDto>
{
    public override partial Core.DocumentSeriesDto Map(Core.Entities.DocumentSeries source);
    public override partial void Map(Core.Entities.DocumentSeries source, Core.DocumentSeriesDto destination);
}

// ─── HR ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LeaveTypeMapper : MapperBase<HumanResources.Entities.LeaveType, HumanResources.LeaveTypeDto>
{
    public override partial HumanResources.LeaveTypeDto Map(HumanResources.Entities.LeaveType source);
    public override partial void Map(HumanResources.Entities.LeaveType source, HumanResources.LeaveTypeDto destination);
}

// ─── Inventory ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PriceListMapper : MapperBase<Inventory.Entities.PriceList, Inventory.PriceListDto>
{
    [MapProperty(nameof(Inventory.Entities.PriceList.Name), nameof(Inventory.PriceListDto.PriceListName))]
    public override partial Inventory.PriceListDto Map(Inventory.Entities.PriceList source);
    [MapProperty(nameof(Inventory.Entities.PriceList.Name), nameof(Inventory.PriceListDto.PriceListName))]
    public override partial void Map(Inventory.Entities.PriceList source, Inventory.PriceListDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ItemPriceMapper : MapperBase<Inventory.Entities.ItemPrice, Inventory.ItemPriceDto>
{
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.ItemCode))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.ItemName))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.PriceListName))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.CustomerName))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.SupplierName))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.IsAutoInserted))]
    public override partial Inventory.ItemPriceDto Map(Inventory.Entities.ItemPrice source);
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.ItemCode))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.ItemName))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.PriceListName))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.CustomerName))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.SupplierName))]
    [MapperIgnoreTarget(nameof(Inventory.ItemPriceDto.IsAutoInserted))]
    public override partial void Map(Inventory.Entities.ItemPrice source, Inventory.ItemPriceDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BinToStockBalanceMapper : MapperBase<Inventory.Entities.Bin, Inventory.StockBalanceDto>
{
    [MapperIgnoreTarget(nameof(Inventory.StockBalanceDto.ItemName))]
    [MapperIgnoreTarget(nameof(Inventory.StockBalanceDto.WarehouseName))]
    public override partial Inventory.StockBalanceDto Map(Inventory.Entities.Bin source);
    [MapperIgnoreTarget(nameof(Inventory.StockBalanceDto.ItemName))]
    [MapperIgnoreTarget(nameof(Inventory.StockBalanceDto.WarehouseName))]
    public override partial void Map(Inventory.Entities.Bin source, Inventory.StockBalanceDto destination);
}

// ─── Projects ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ProjectTaskMapper : MapperBase<Projects.Entities.ProjectTask, Projects.ProjectTaskDto>
{
    public override partial Projects.ProjectTaskDto Map(Projects.Entities.ProjectTask source);
    public override partial void Map(Projects.Entities.ProjectTask source, Projects.ProjectTaskDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ActivityTypeMapper : MapperBase<Projects.Entities.ActivityType, Projects.ActivityTypeDto>
{
    public override partial Projects.ActivityTypeDto Map(Projects.Entities.ActivityType source);
    public override partial void Map(Projects.Entities.ActivityType source, Projects.ActivityTypeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ActivityCostMapper : MapperBase<Projects.Entities.ActivityCost, Projects.ActivityCostDto>
{
    public override partial Projects.ActivityCostDto Map(Projects.Entities.ActivityCost source);
    public override partial void Map(Projects.Entities.ActivityCost source, Projects.ActivityCostDto destination);
}

// ─── Purchasing ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SubcontractingOrderMapper : MapperBase<Purchasing.Entities.SubcontractingOrder, Purchasing.SubcontractingOrderDto>
{
    [MapperIgnoreTarget(nameof(Purchasing.SubcontractingOrderDto.SupplierName))]
    public override partial Purchasing.SubcontractingOrderDto Map(Purchasing.Entities.SubcontractingOrder source);
    [MapperIgnoreTarget(nameof(Purchasing.SubcontractingOrderDto.SupplierName))]
    public override partial void Map(Purchasing.Entities.SubcontractingOrder source, Purchasing.SubcontractingOrderDto destination);
    private partial Purchasing.ScoItemDto MapChild(Purchasing.Entities.SubcontractingOrderItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SubcontractingReceiptMapper : MapperBase<Purchasing.Entities.SubcontractingReceipt, Purchasing.SubcontractingReceiptDto>
{
    public override partial Purchasing.SubcontractingReceiptDto Map(Purchasing.Entities.SubcontractingReceipt source);
    public override partial void Map(Purchasing.Entities.SubcontractingReceipt source, Purchasing.SubcontractingReceiptDto destination);
    private partial Purchasing.SubcontractingReceiptItemDto MapChild(Purchasing.Entities.SubcontractingReceiptItem source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SubcontractingInwardOrderMapper : MapperBase<Purchasing.Entities.SubcontractingInwardOrder, Purchasing.SubcontractingInwardOrderDto>
{
    public override partial Purchasing.SubcontractingInwardOrderDto Map(Purchasing.Entities.SubcontractingInwardOrder source);
    public override partial void Map(Purchasing.Entities.SubcontractingInwardOrder source, Purchasing.SubcontractingInwardOrderDto destination);
    private partial Purchasing.SubcontractingInwardOrderItemDto MapChild(Purchasing.Entities.SubcontractingInwardOrderItem source);
}

// ─── ImportExport ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ImportJobMapper : MapperBase<ImportExport.Entities.ImportJob, ImportExport.DTOs.ImportJobDto>
{
    public override partial ImportExport.DTOs.ImportJobDto Map(ImportExport.Entities.ImportJob source);
    public override partial void Map(ImportExport.Entities.ImportJob source, ImportExport.DTOs.ImportJobDto destination);
}

// ─── Workflow ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ApprovalRuleMapper : MapperBase<Workflow.Entities.ApprovalRule, Workflow.DTOs.ApprovalRuleDto>
{
    public override partial Workflow.DTOs.ApprovalRuleDto Map(Workflow.Entities.ApprovalRule source);
    public override partial void Map(Workflow.Entities.ApprovalRule source, Workflow.DTOs.ApprovalRuleDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ApprovalRequestMapper : MapperBase<Workflow.Entities.ApprovalRequest, Workflow.DTOs.ApprovalRequestDto>
{
    [MapperIgnoreTarget(nameof(Workflow.DTOs.ApprovalRequestDto.RuleName))]
    public override partial Workflow.DTOs.ApprovalRequestDto Map(Workflow.Entities.ApprovalRequest source);
    [MapperIgnoreTarget(nameof(Workflow.DTOs.ApprovalRequestDto.RuleName))]
    public override partial void Map(Workflow.Entities.ApprovalRequest source, Workflow.DTOs.ApprovalRequestDto destination);
}

// ━━━ Replacement mappers for manual MapToDto methods ━━━

// ─── Accounting: PaymentTermsTemplate ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PaymentTermsTemplateMapper : MapperBase<Accounting.Entities.PaymentTermsTemplate, Accounting.PaymentTermsTemplateDto>
{
    public override partial Accounting.PaymentTermsTemplateDto Map(Accounting.Entities.PaymentTermsTemplate source);
    public override partial void Map(Accounting.Entities.PaymentTermsTemplate source, Accounting.PaymentTermsTemplateDto destination);
    private partial Accounting.PaymentTermDto MapChild(Accounting.Entities.PaymentTerm source);
}

// ─── HR: LeaveType → LeaveTypeDetailDto ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LeaveTypeDetailMapper : MapperBase<HumanResources.Entities.LeaveType, HumanResources.LeaveTypeDetailDto>
{
    public override partial HumanResources.LeaveTypeDetailDto Map(HumanResources.Entities.LeaveType source);
    public override partial void Map(HumanResources.Entities.LeaveType source, HumanResources.LeaveTypeDetailDto destination);

    private int MapDecimalToInt(decimal d) => (int)d;
}

// ─── HR: SalaryComponent ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SalaryComponentMapper : MapperBase<HumanResources.Entities.SalaryComponent, HumanResources.SalaryComponentDto>
{
    public override partial HumanResources.SalaryComponentDto Map(HumanResources.Entities.SalaryComponent source);
    public override partial void Map(HumanResources.Entities.SalaryComponent source, HumanResources.SalaryComponentDto destination);
}

// ─── Inventory: ItemStandardCost ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ItemStandardCostMapper : MapperBase<Inventory.Entities.ItemStandardCost, Inventory.ItemStandardCostDto>
{
    public override partial Inventory.ItemStandardCostDto Map(Inventory.Entities.ItemStandardCost source);
    public override partial void Map(Inventory.Entities.ItemStandardCost source, Inventory.ItemStandardCostDto destination);
}

// ─── Inventory: PutawayRule ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PutawayRuleMapper : MapperBase<Inventory.Entities.PutawayRule, Inventory.PutawayRuleDto>
{
    public override partial Inventory.PutawayRuleDto Map(Inventory.Entities.PutawayRule source);
    public override partial void Map(Inventory.Entities.PutawayRule source, Inventory.PutawayRuleDto destination);
}

// ─── Inventory: RepostItemValuation ───

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class RepostItemValuationMapper : MapperBase<Inventory.Entities.RepostItemValuation, Inventory.RepostItemValuationDto>
{
    public override partial Inventory.RepostItemValuationDto Map(Inventory.Entities.RepostItemValuation source);
    public override partial void Map(Inventory.Entities.RepostItemValuation source, Inventory.RepostItemValuationDto destination);
}

// ---------------------------------------------------
// Phase 7 Quality Management & Settings Additions
// ---------------------------------------------------
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class QualityGoalMapper : MapperBase<Inventory.Entities.QualityGoal, Inventory.QualityGoalDto>
{
    public override partial Inventory.QualityGoalDto Map(Inventory.Entities.QualityGoal source);
    public override partial void Map(Inventory.Entities.QualityGoal source, Inventory.QualityGoalDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class QualityActionMapper : MapperBase<Inventory.Entities.QualityAction, Inventory.QualityActionDto>
{
    public override partial Inventory.QualityActionDto Map(Inventory.Entities.QualityAction source);
    public override partial void Map(Inventory.Entities.QualityAction source, Inventory.QualityActionDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class QualityReviewMapper : MapperBase<Inventory.Entities.QualityReview, Inventory.QualityReviewDto>
{
    public override partial Inventory.QualityReviewDto Map(Inventory.Entities.QualityReview source);
    public override partial void Map(Inventory.Entities.QualityReview source, Inventory.QualityReviewDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PrintFormatMapper : MapperBase<Settings.Entities.PrintFormat, Settings.PrintFormatDto>
{
    public override partial Settings.PrintFormatDto Map(Settings.Entities.PrintFormat source);
    public override partial void Map(Settings.Entities.PrintFormat source, Settings.PrintFormatDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class LetterHeadMapper : MapperBase<Core.Entities.LetterHead, Core.LetterHeadDto>
{
    public override partial Core.LetterHeadDto Map(Core.Entities.LetterHead source);
    public override partial void Map(Core.Entities.LetterHead source, Core.LetterHeadDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TermsAndConditionsMapper : MapperBase<Core.Entities.TermsAndConditions, Core.TermsAndConditionsDto>
{
    public override partial Core.TermsAndConditionsDto Map(Core.Entities.TermsAndConditions source);
    public override partial void Map(Core.Entities.TermsAndConditions source, Core.TermsAndConditionsDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AssetValueAdjustmentMapper : MapperBase<Assets.Entities.AssetValueAdjustment, Assets.AssetValueAdjustmentDto>
{
    public override partial Assets.AssetValueAdjustmentDto Map(Assets.Entities.AssetValueAdjustment source);
    public override partial void Map(Assets.Entities.AssetValueAdjustment source, Assets.AssetValueAdjustmentDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AssetActivityMapper : MapperBase<Assets.Entities.AssetActivity, Assets.AssetActivityDto>
{
    public override partial Assets.AssetActivityDto Map(Assets.Entities.AssetActivity source);
    public override partial void Map(Assets.Entities.AssetActivity source, Assets.AssetActivityDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class QualityProcedureMapper : MapperBase<Inventory.Entities.QualityProcedure, Inventory.QualityProcedureDto>
{
    public override partial Inventory.QualityProcedureDto Map(Inventory.Entities.QualityProcedure source);
    public override partial void Map(Inventory.Entities.QualityProcedure source, Inventory.QualityProcedureDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class NonConformanceMapper : MapperBase<Inventory.Entities.NonConformance, Inventory.NonConformanceDto>
{
    public override partial Inventory.NonConformanceDto Map(Inventory.Entities.NonConformance source);
    public override partial void Map(Inventory.Entities.NonConformance source, Inventory.NonConformanceDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class QualityMeetingMapper : MapperBase<Inventory.Entities.QualityMeeting, Inventory.QualityMeetingDto>
{
    public override partial Inventory.QualityMeetingDto Map(Inventory.Entities.QualityMeeting source);
    public override partial void Map(Inventory.Entities.QualityMeeting source, Inventory.QualityMeetingDto destination);
    private partial Inventory.QualityMeetingAgendaDto MapMeetingAgenda(Inventory.Entities.QualityMeetingAgenda source);
    private partial Inventory.QualityMeetingMinutesDto MapMeetingMinutes(Inventory.Entities.QualityMeetingMinutes source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class QualityFeedbackTemplateMapper : MapperBase<Inventory.Entities.QualityFeedbackTemplate, Inventory.QualityFeedbackTemplateDto>
{
    public override partial Inventory.QualityFeedbackTemplateDto Map(Inventory.Entities.QualityFeedbackTemplate source);
    public override partial void Map(Inventory.Entities.QualityFeedbackTemplate source, Inventory.QualityFeedbackTemplateDto destination);
    private partial Inventory.QualityFeedbackTemplateParameterDto MapFeedbackTemplateParam(Inventory.Entities.QualityFeedbackTemplateParameter source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class QualityFeedbackMapper : MapperBase<Inventory.Entities.QualityFeedback, Inventory.QualityFeedbackDto>
{
    public override partial Inventory.QualityFeedbackDto Map(Inventory.Entities.QualityFeedback source);
    public override partial void Map(Inventory.Entities.QualityFeedback source, Inventory.QualityFeedbackDto destination);
    private partial Inventory.QualityFeedbackParameterDto MapFeedbackParam(Inventory.Entities.QualityFeedbackParameter source);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BankGuaranteeMapper : MapperBase<Accounting.Entities.BankGuarantee, Accounting.BankGuaranteeDto>
{
    public override partial Accounting.BankGuaranteeDto Map(Accounting.Entities.BankGuarantee source);
    public override partial void Map(Accounting.Entities.BankGuarantee source, Accounting.BankGuaranteeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class CustomsTariffNumberMapper : MapperBase<Inventory.Entities.CustomsTariffNumber, Inventory.CustomsTariffNumberDto>
{
    public override partial Inventory.CustomsTariffNumberDto Map(Inventory.Entities.CustomsTariffNumber source);
    public override partial void Map(Inventory.Entities.CustomsTariffNumber source, Inventory.CustomsTariffNumberDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class ManufacturerMapper : MapperBase<Inventory.Entities.Manufacturer, Inventory.ManufacturerDto>
{
    public override partial Inventory.ManufacturerDto Map(Inventory.Entities.Manufacturer source);
    public override partial void Map(Inventory.Entities.Manufacturer source, Inventory.ManufacturerDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class ItemManufacturerMapper : MapperBase<Inventory.Entities.ItemManufacturer, Inventory.ItemManufacturerDto>
{
    public override partial Inventory.ItemManufacturerDto Map(Inventory.Entities.ItemManufacturer source);
    public override partial void Map(Inventory.Entities.ItemManufacturer source, Inventory.ItemManufacturerDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class ItemAlternativeMapper : MapperBase<Inventory.Entities.ItemAlternative, Inventory.ItemAlternativeDto>
{
    public override partial Inventory.ItemAlternativeDto Map(Inventory.Entities.ItemAlternative source);
    public override partial void Map(Inventory.Entities.ItemAlternative source, Inventory.ItemAlternativeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class PartySpecificItemMapper : MapperBase<Sales.Entities.PartySpecificItem, Sales.PartySpecificItemDto>
{
    public override partial Sales.PartySpecificItemDto Map(Sales.Entities.PartySpecificItem source);
    public override partial void Map(Sales.Entities.PartySpecificItem source, Sales.PartySpecificItemDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DeliveryStopMapper : MapperBase<Inventory.Entities.DeliveryStop, Inventory.DeliveryStopDto>
{
    public override partial Inventory.DeliveryStopDto Map(Inventory.Entities.DeliveryStop source);
    public override partial void Map(Inventory.Entities.DeliveryStop source, Inventory.DeliveryStopDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class DeliveryTripMapper : MapperBase<Inventory.Entities.DeliveryTrip, Inventory.DeliveryTripDto>
{
    public override partial Inventory.DeliveryTripDto Map(Inventory.Entities.DeliveryTrip source);
    public override partial void Map(Inventory.Entities.DeliveryTrip source, Inventory.DeliveryTripDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AssetMaintenanceTaskMapper : MapperBase<Maintenance.Entities.AssetMaintenanceTask, Assets.AssetMaintenanceTaskDto>
{
    public override partial Assets.AssetMaintenanceTaskDto Map(Maintenance.Entities.AssetMaintenanceTask source);
    public override partial void Map(Maintenance.Entities.AssetMaintenanceTask source, Assets.AssetMaintenanceTaskDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AssetMaintenanceMapper : MapperBase<Maintenance.Entities.AssetMaintenance, Assets.AssetMaintenanceDto>
{
    public override partial Assets.AssetMaintenanceDto Map(Maintenance.Entities.AssetMaintenance source);
    public override partial void Map(Maintenance.Entities.AssetMaintenance source, Assets.AssetMaintenanceDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AssetMaintenanceLogMapper : MapperBase<Maintenance.Entities.AssetMaintenanceLog, Assets.AssetMaintenanceLogDto>
{
    public override partial Assets.AssetMaintenanceLogDto Map(Maintenance.Entities.AssetMaintenanceLog source);
    public override partial void Map(Maintenance.Entities.AssetMaintenanceLog source, Assets.AssetMaintenanceLogDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PosProfilePaymentMethodMapper : MapperBase<Sales.Entities.PosProfilePaymentMethod, Sales.PosProfilePaymentMethodDto>
{
    public override partial Sales.PosProfilePaymentMethodDto Map(Sales.Entities.PosProfilePaymentMethod source);
    public override partial void Map(Sales.Entities.PosProfilePaymentMethod source, Sales.PosProfilePaymentMethodDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class PosProfileMapper : MapperBase<Sales.Entities.PosProfile, Sales.PosProfileDto>
{
    public override partial Sales.PosProfileDto Map(Sales.Entities.PosProfile source);
    public override partial void Map(Sales.Entities.PosProfile source, Sales.PosProfileDto destination);
}

