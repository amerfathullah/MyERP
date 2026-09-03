using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Handles inter-company transactions — creating paired documents between companies.
/// E.g., SI in Company A → PI in Company B (linked via party validation).
/// Per DO-NOT: requires bidirectional party validation before creating.
/// Per DO-NOT: must NOT copy party-specific fields (12 CROSS_PARTY_FIELD_NO_MAP fields).
/// </summary>
public class InterCompanyTransactionService : DomainService
{
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public InterCompanyTransactionService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _companyRepository = companyRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
    }

    /// <summary>
    /// Creates a Purchase Invoice in the target company from a Sales Invoice in the source company.
    /// Validates bidirectional party linkage (Customer in source ↔ Supplier in target).
    /// </summary>
    public async Task<Guid> CreatePurchaseInvoiceFromSalesInvoiceAsync(
        Guid salesInvoiceId, Guid targetCompanyId, Guid? tenantId = null)
    {
        var si = await _salesInvoiceRepository.GetAsync(salesInvoiceId);
        var sourceCompany = await _companyRepository.GetAsync(si.CompanyId);
        var targetCompany = await _companyRepository.GetAsync(targetCompanyId);

        // Guard: block duplicate creation if already invoiced (ERPNext PR #55639)
        if (si.InterCompanyPurchaseInvoiceId.HasValue)
        {
            var existingPi = await _purchaseInvoiceRepository.FindAsync(si.InterCompanyPurchaseInvoiceId.Value);
            if (existingPi != null && existingPi.Status != DocumentStatus.Cancelled)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InterCompanyInvoiceAlreadyCreated)
                    .WithData("salesInvoiceNumber", si.InvoiceNumber)
                    .WithData("purchaseInvoiceNumber", existingPi.InvoiceNumber);
            }
        }

        // Validate: the Customer in source company must represent target company
        var customer = await _customerRepository.GetAsync(si.CustomerId);
        if (customer.RepresentsCompanyId != targetCompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyPartyMismatch)
                .WithData("customer", customer.Name)
                .WithData("targetCompany", targetCompany.Name);
        }

        // Find the matching Supplier in target company that represents source company
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var matchingSupplier = supplierQuery
            .FirstOrDefault(s => s.CompanyId == targetCompanyId
                              && s.RepresentsCompanyId == si.CompanyId);

        if (matchingSupplier == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyPartyMismatch)
                .WithData("sourceCompany", sourceCompany.Name)
                .WithData("targetCompany", targetCompany.Name);
        }

        // Create the PI in target company — do NOT copy party-specific fields
        var pi = new PurchaseInvoice(
            GuidGenerator.Create(),
            targetCompanyId,
            matchingSupplier.Id,
            $"IC-{si.InvoiceNumber}",
            si.IssueDate,
            tenantId);

        pi.DueDate = si.DueDate;
        pi.CurrencyCode = sourceCompany.CurrencyCode; // Must be source company currency
        pi.InterCompanyInvoiceId = si.Id;

        // Copy items (amounts only — no party-specific fields)
        foreach (var item in si.Items)
        {
            pi.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _purchaseInvoiceRepository.InsertAsync(pi, autoSave: true);

        // Reciprocal link — without this, cancelling the SI has no way to find the PI it created.
        si.InterCompanyPurchaseInvoiceId = pi.Id;
        await _salesInvoiceRepository.UpdateAsync(si, autoSave: true);

        return pi.Id;
    }

    /// <summary>
    /// Creates a Sales Invoice in the target company from a Purchase Invoice in the source company.
    /// Validates bidirectional party linkage (Supplier in source ↔ Customer in target).
    /// </summary>
    public async Task<Guid> CreateSalesInvoiceFromPurchaseInvoiceAsync(
        Guid purchaseInvoiceId, Guid targetCompanyId, Guid? tenantId = null)
    {
        var pi = await _purchaseInvoiceRepository.GetAsync(purchaseInvoiceId);
        var sourceCompany = await _companyRepository.GetAsync(pi.CompanyId);
        var targetCompany = await _companyRepository.GetAsync(targetCompanyId);

        // Guard: block duplicate creation if already invoiced (ERPNext PR #55639)
        if (pi.InterCompanyInvoiceId.HasValue)
        {
            var existingSi = await _salesInvoiceRepository.FindAsync(pi.InterCompanyInvoiceId.Value);
            if (existingSi != null && existingSi.Status != DocumentStatus.Cancelled)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InterCompanyInvoiceAlreadyCreated)
                    .WithData("purchaseInvoiceNumber", pi.InvoiceNumber)
                    .WithData("salesInvoiceNumber", existingSi.InvoiceNumber);
            }
        }

        // Validate: the Supplier in source company must represent target company
        var supplier = await _supplierRepository.GetAsync(pi.SupplierId);
        if (supplier.RepresentsCompanyId != targetCompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyPartyMismatch)
                .WithData("supplier", supplier.Name)
                .WithData("targetCompany", targetCompany.Name);
        }

        // Find matching Customer in target company that represents source company
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var matchingCustomer = customerQuery
            .FirstOrDefault(c => c.CompanyId == targetCompanyId
                              && c.RepresentsCompanyId == pi.CompanyId);

        if (matchingCustomer == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InterCompanyPartyMismatch)
                .WithData("sourceCompany", sourceCompany.Name)
                .WithData("targetCompany", targetCompany.Name);
        }

        // Create the SI in target company
        var si = new SalesInvoice(
            GuidGenerator.Create(),
            targetCompanyId,
            matchingCustomer.Id,
            $"IC-{pi.InvoiceNumber}",
            pi.IssueDate);

        si.DueDate = pi.DueDate;
        si.CurrencyCode = sourceCompany.CurrencyCode;
        si.InterCompanyPurchaseInvoiceId = pi.Id;

        foreach (var item in pi.Items)
        {
            si.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _salesInvoiceRepository.InsertAsync(si, autoSave: true);

        // Reciprocal link — without this, cancelling the PI has no way to find the SI it created.
        pi.InterCompanyInvoiceId = si.Id;
        await _purchaseInvoiceRepository.UpdateAsync(pi, autoSave: true);

        return si.Id;
    }

    /// <summary>
    /// Creates a Purchase Order in the target company from a Sales Order in the source company.
    /// Per ERPNext make_inter_company_purchase_order: triggered on SO submit when customer represents another company.
    /// </summary>
    public async Task<Guid?> CreatePurchaseOrderFromSalesOrderAsync(
        SalesOrder salesOrder, Func<string, Guid, Task<string>> numberGenerator, Guid? tenantId = null)
    {
        var customer = await _customerRepository.GetAsync(salesOrder.CustomerId);
        if (!customer.RepresentsCompanyId.HasValue)
            return null; // Not an inter-company customer

        var targetCompanyId = customer.RepresentsCompanyId.Value;
        var targetCompany = await _companyRepository.GetAsync(targetCompanyId);

        // Find the supplier in target company that represents the source company
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var matchingSupplier = supplierQuery
            .FirstOrDefault(s => s.CompanyId == targetCompanyId
                              && s.RepresentsCompanyId == salesOrder.CompanyId);

        if (matchingSupplier == null)
            return null; // No bidirectional link configured — skip silently

        var poNumber = await numberGenerator("PurchaseOrder", targetCompanyId);

        var po = new PurchaseOrder(
            GuidGenerator.Create(),
            targetCompanyId,
            matchingSupplier.Id,
            poNumber,
            salesOrder.OrderDate,
            tenantId);

        po.ExpectedDeliveryDate = salesOrder.DeliveryDate;
        po.CurrencyCode = salesOrder.CurrencyCode ?? "MYR";
        po.InterCompanySalesOrderId = salesOrder.Id;
        po.Notes = $"Auto-created from inter-company Sales Order {salesOrder.OrderNumber}";

        foreach (var item in salesOrder.Items)
        {
            po.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, 0m, item.Uom);
        }

        await _purchaseOrderRepository.InsertAsync(po, autoSave: true);
        return po.Id;
    }

    /// <summary>
    /// Validates that line items in an inter-company transaction have rates matching the linked transaction.
    /// Per ERPNext PR #47780 / commit 3a2b863e7f.
    /// </summary>
    public async Task ValidateInterCompanyRatesAsync(
        Guid companyId,
        Guid? linkedSalesInvoiceId,
        Guid? linkedPurchaseInvoiceId,
        IEnumerable<(Guid ItemId, decimal UnitPrice, string? Description)> items,
        IEnumerable<string>? userRoles = null)
    {
        var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.AccountsSettings, Guid>>();
        var settings = (await settingsRepo.GetQueryableAsync()).FirstOrDefault();
        if (settings == null || !settings.MaintainSameInternalTransactionRate)
            return;

        var action = settings.MaintainSameRateAction ?? "Stop";
        var overrideRole = settings.RoleToOverrideStopAction;
        var canOverride = !string.IsNullOrEmpty(overrideRole)
            && userRoles != null && userRoles.Contains(overrideRole);

        var rateLines = new List<(string ItemDescription, decimal Rate, decimal ReferenceRate, string ReferenceDocType)>();

        if (linkedSalesInvoiceId.HasValue)
        {
            var si = await _salesInvoiceRepository.FindAsync(linkedSalesInvoiceId.Value);
            if (si != null)
            {
                var siRates = si.Items.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.First().UnitPrice);
                foreach (var item in items)
                {
                    if (siRates.TryGetValue(item.ItemId, out var refRate))
                    {
                        rateLines.Add((item.Description ?? item.ItemId.ToString(), item.UnitPrice, refRate, $"Sales Invoice {si.InvoiceNumber}"));
                    }
                }
            }
        }
        else if (linkedPurchaseInvoiceId.HasValue)
        {
            var pi = await _purchaseInvoiceRepository.FindAsync(linkedPurchaseInvoiceId.Value);
            if (pi != null)
            {
                var piRates = pi.Items.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.First().UnitPrice);
                foreach (var item in items)
                {
                    if (piRates.TryGetValue(item.ItemId, out var refRate))
                    {
                        rateLines.Add((item.Description ?? item.ItemId.ToString(), item.UnitPrice, refRate, $"Purchase Invoice {pi.InvoiceNumber}"));
                    }
                }
            }
        }

        if (rateLines.Count > 0)
        {
            var validationService = LazyServiceProvider.LazyGetRequiredService<TransactionValidationService>();
            validationService.ValidateMaintainSameRate(rateLines, action, canOverride);
        }
    }
}
