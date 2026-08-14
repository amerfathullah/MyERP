using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Resolves party (Customer/Supplier) defaults for transaction forms.
/// Called when user selects a customer/supplier on SO, SI, PO, PI, DN, PR, PE, etc.
/// Per ERPNext: party.py get_party_details() — the most-called API in transaction forms.
/// Returns: address, TIN, payment terms, currency, receivable/payable account, credit info.
/// </summary>
[Authorize]
public class PartyDetailsAppService : ApplicationService, IPartyDetailsAppService
{
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Address, Guid> _addressRepo;
    private readonly IRepository<PaymentTermsTemplate, Guid> _paymentTermsRepo;
    private readonly IRepository<Company, Guid> _companyRepo;
    private readonly PartyDefaultsService _partyDefaults;

    public PartyDetailsAppService(
        IRepository<Customer, Guid> customerRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<Address, Guid> addressRepo,
        IRepository<PaymentTermsTemplate, Guid> paymentTermsRepo,
        IRepository<Company, Guid> companyRepo,
        PartyDefaultsService partyDefaults)
    {
        _customerRepo = customerRepo;
        _supplierRepo = supplierRepo;
        _addressRepo = addressRepo;
        _paymentTermsRepo = paymentTermsRepo;
        _companyRepo = companyRepo;
        _partyDefaults = partyDefaults;
    }

    /// <summary>
    /// Resolves all defaults for a customer in the context of a selling transaction.
    /// Per ERPNext party.py: fetches address, TIN, payment terms, receivable account, credit limit.
    /// Called on every customer change in SO/SI/DN/Quotation forms.
    /// </summary>
    public async Task<PartyDetailsDto> GetCustomerDetailsAsync(GetPartyDetailsInput input)
    {
        var customer = await _customerRepo.GetAsync(input.PartyId);
        var company = input.CompanyId.HasValue
            ? await _companyRepo.GetAsync(input.CompanyId.Value)
            : null;

        var result = new PartyDetailsDto
        {
            PartyId = customer.Id,
            PartyName = customer.Name,
            PartyType = "Customer",
            Tin = customer.Tin,
            RegistrationNumber = customer.RegistrationNumber,
            SstRegistrationNumber = customer.SstRegistrationNumber,
            IdType = customer.IdType,
            IdValue = customer.IdValue,
            ContactPerson = customer.ContactPerson,
            Phone = customer.Phone,
            Email = customer.Email,
            CreditLimit = customer.CreditLimit,
            DefaultPaymentTermsTemplateId = customer.DefaultPaymentTermsTemplateId,
            DefaultReceivableAccountId = customer.DefaultReceivableAccountId,
            CustomerGroupId = customer.CustomerGroupId,
            TerritoryId = customer.TerritoryId,
        };

        // Resolve primary billing address
        var billingAddress = await _partyDefaults.GetPrimaryAddressAsync("Customer", customer.Id);
        if (billingAddress != null)
        {
            result.BillingAddressId = billingAddress.Id;
            result.BillingAddress = FormatAddress(billingAddress);
            result.BillingCity = billingAddress.City;
            result.BillingState = billingAddress.State;
            result.BillingPostalCode = billingAddress.PostalCode;
            result.BillingCountry = billingAddress.Country;
        }
        else if (!string.IsNullOrEmpty(customer.Address))
        {
            // Fallback to inline address on customer master
            result.BillingAddress = customer.Address;
            result.BillingCity = customer.City;
            result.BillingState = customer.State;
            result.BillingPostalCode = customer.PostalCode;
            result.BillingCountry = customer.Country;
        }

        // Resolve shipping address
        var shippingAddress = await _partyDefaults.GetShippingAddressAsync("Customer", customer.Id);
        if (shippingAddress != null)
        {
            result.ShippingAddressId = shippingAddress.Id;
            result.ShippingAddress = FormatAddress(shippingAddress);
        }

        // Resolve payment terms template name and credit days for due date calculation
        if (customer.DefaultPaymentTermsTemplateId.HasValue)
        {
            var terms = await _paymentTermsRepo.FindAsync(
                customer.DefaultPaymentTermsTemplateId.Value, includeDetails: true);
            if (terms != null)
            {
                result.PaymentTermsTemplateName = terms.Name;
                // Per ERPNext: due date uses the FIRST term's credit days
                var firstTerm = terms.Terms.OrderBy(t => t.CreditDays).FirstOrDefault();
                result.DefaultCreditDays = firstTerm?.CreditDays ?? 0;
            }
        }

        // Resolve company currency for currency defaulting
        if (company != null)
        {
            result.CompanyCurrency = company.CurrencyCode ?? "MYR";
        }

        // Calculate current outstanding for credit display
        result.Outstanding = await GetCustomerOutstandingAsync(customer.Id, input.CompanyId);

        return result;
    }

    /// <summary>
    /// Resolves all defaults for a supplier in the context of a buying transaction.
    /// Per ERPNext party.py: fetches address, TIN, payment terms, payable account.
    /// Called on every supplier change in PO/PI/PR forms.
    /// </summary>
    public async Task<PartyDetailsDto> GetSupplierDetailsAsync(GetPartyDetailsInput input)
    {
        var supplier = await _supplierRepo.GetAsync(input.PartyId);
        var company = input.CompanyId.HasValue
            ? await _companyRepo.GetAsync(input.CompanyId.Value)
            : null;

        var result = new PartyDetailsDto
        {
            PartyId = supplier.Id,
            PartyName = supplier.Name,
            PartyType = "Supplier",
            Tin = supplier.Tin,
            RegistrationNumber = supplier.RegistrationNumber,
            SstRegistrationNumber = supplier.SstRegistrationNumber,
            IdType = supplier.IdType,
            IdValue = supplier.IdValue,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Email = supplier.Email,
            DefaultPaymentTermsTemplateId = supplier.DefaultPaymentTermsTemplateId,
            DefaultPayableAccountId = supplier.DefaultPayableAccountId,
        };

        // Resolve primary billing address
        var billingAddress = await _partyDefaults.GetPrimaryAddressAsync("Supplier", supplier.Id);
        if (billingAddress != null)
        {
            result.BillingAddressId = billingAddress.Id;
            result.BillingAddress = FormatAddress(billingAddress);
            result.BillingCity = billingAddress.City;
            result.BillingState = billingAddress.State;
            result.BillingPostalCode = billingAddress.PostalCode;
            result.BillingCountry = billingAddress.Country;
        }
        else if (!string.IsNullOrEmpty(supplier.Address))
        {
            result.BillingAddress = supplier.Address;
            result.BillingCity = supplier.City;
            result.BillingState = supplier.State;
            result.BillingPostalCode = supplier.PostalCode;
            result.BillingCountry = supplier.Country;
        }

        // Resolve payment terms template name
        if (supplier.DefaultPaymentTermsTemplateId.HasValue)
        {
            var terms = await _paymentTermsRepo.FindAsync(supplier.DefaultPaymentTermsTemplateId.Value);
            result.PaymentTermsTemplateName = terms?.Name;
        }

        // Resolve company currency
        if (company != null)
        {
            result.CompanyCurrency = company.CurrencyCode ?? "MYR";
        }

        return result;
    }

    private async Task<decimal> GetCustomerOutstandingAsync(Guid customerId, Guid? companyId)
    {
        var invoiceQuery = await LazyServiceProvider
            .LazyGetRequiredService<IRepository<SalesInvoice, Guid>>()
            .GetQueryableAsync();

        var query = invoiceQuery.Where(i =>
            i.CustomerId == customerId &&
            i.Status == DocumentStatus.Posted &&
            i.GrandTotal > i.AmountPaid);

        if (companyId.HasValue)
            query = query.Where(i => i.CompanyId == companyId.Value);

        return query.Sum(i => i.GrandTotal - i.AmountPaid);
    }

    private static string FormatAddress(Address addr)
    {
        var parts = new[]
        {
            addr.AddressLine1,
            addr.AddressLine2,
            addr.City,
            addr.State,
            addr.PostalCode,
            addr.Country
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(", ", parts);
    }
}

