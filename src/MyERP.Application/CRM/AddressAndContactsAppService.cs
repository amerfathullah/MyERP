using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Customers.Default)]
public class AddressAndContactsAppService : ApplicationService, IAddressAndContactsAppService
{
    private readonly IRepository<Address, Guid> _addressRepo;
    private readonly IRepository<Contact, Guid> _contactRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<SalesPartner, Guid> _salesPartnerRepo;
    private readonly IRepository<Lead, Guid> _leadRepo;

    public AddressAndContactsAppService(
        IRepository<Address, Guid> addressRepo,
        IRepository<Contact, Guid> contactRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<SalesPartner, Guid> salesPartnerRepo,
        IRepository<Lead, Guid> leadRepo)
    {
        _addressRepo = addressRepo;
        _contactRepo = contactRepo;
        _customerRepo = customerRepo;
        _supplierRepo = supplierRepo;
        _salesPartnerRepo = salesPartnerRepo;
        _leadRepo = leadRepo;
    }

    private sealed record PartyRecord(
        Guid Id,
        string Name,
        string? Code,
        string? Group);

    public async Task<AddressAndContactsReportDto> GetReportAsync(GetAddressAndContactsInput input)
    {
        var partyType = input.PartyType?.Trim() ?? "Customer";
        var normPartyType = partyType.ToLowerInvariant() switch
        {
            "supplier" => "Supplier",
            "salespartner" or "sales partner" => "SalesPartner",
            "lead" => "Lead",
            _ => "Customer",
        };

        var parties = await LoadPartiesAsync(normPartyType, input);

        if (parties.Count == 0)
        {
            return new AddressAndContactsReportDto
            {
                PartyType = normPartyType,
                Rows = new List<AddressAndContactRowDto>(),
                TotalParties = 0,
                TotalAddresses = 0,
                TotalContacts = 0,
            };
        }

        var partyIds = parties.Select(p => p.Id).ToList();

        // Load addresses for these parties
        var addrQuery = await _addressRepo.GetQueryableAsync();
        var addresses = addrQuery
            .Where(a => (a.PartyType == normPartyType || (normPartyType == "SalesPartner" && a.PartyType == "Sales Partner"))
                        && partyIds.Contains(a.PartyId)
                        && !a.IsDisabled)
            .OrderByDescending(a => a.IsPrimaryAddress)
            .ThenBy(a => a.Title)
            .ToList();

        var addrDict = addresses
            .GroupBy(a => a.PartyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load contacts for these parties
        var contactQuery = await _contactRepo.GetQueryableAsync();
        var contacts = contactQuery
            .Where(c => (c.PartyType == normPartyType || (normPartyType == "SalesPartner" && c.PartyType == "Sales Partner"))
                        && partyIds.Contains(c.PartyId)
                        && c.IsActive)
            .OrderByDescending(c => c.IsPrimaryContact)
            .ThenBy(c => c.FirstName)
            .ToList();

        var contactDict = contacts
            .GroupBy(c => c.PartyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<AddressAndContactRowDto>();

        foreach (var p in parties)
        {
            addrDict.TryGetValue(p.Id, out var partyAddrs);
            partyAddrs ??= new List<Address>();

            contactDict.TryGetValue(p.Id, out var partyContacts);
            partyContacts ??= new List<Contact>();

            var maxLen = Math.Max(partyAddrs.Count, partyContacts.Count);

            if (maxLen == 0)
            {
                rows.Add(new AddressAndContactRowDto
                {
                    PartyId = p.Id,
                    PartyType = normPartyType,
                    PartyName = p.Name,
                    PartyCode = p.Code,
                    PartyGroup = p.Group,
                });
            }
            else
            {
                for (int i = 0; i < maxLen; i++)
                {
                    var a = i < partyAddrs.Count ? partyAddrs[i] : null;
                    var c = i < partyContacts.Count ? partyContacts[i] : null;

                    rows.Add(new AddressAndContactRowDto
                    {
                        PartyId = p.Id,
                        PartyType = normPartyType,
                        PartyName = p.Name,
                        PartyCode = p.Code,
                        PartyGroup = p.Group,

                        // Address
                        AddressId = a?.Id,
                        AddressTitle = a?.Title,
                        AddressType = a?.AddressType,
                        AddressLine1 = a?.AddressLine1,
                        AddressLine2 = a?.AddressLine2,
                        City = a?.City,
                        State = a?.State,
                        PostalCode = a?.PostalCode,
                        Country = a?.Country,
                        IsPrimaryAddress = a?.IsPrimaryAddress ?? false,

                        // Contact
                        ContactId = c?.Id,
                        ContactName = c?.FullName,
                        Designation = c?.Designation,
                        Department = c?.Department,
                        Email = c?.Email,
                        Phone = c?.Phone,
                        MobileNo = c?.MobileNo,
                        IsPrimaryContact = c?.IsPrimaryContact ?? false,
                    });
                }
            }
        }

        return new AddressAndContactsReportDto
        {
            PartyType = normPartyType,
            Rows = rows,
            TotalParties = parties.Count,
            TotalAddresses = addresses.Count,
            TotalContacts = contacts.Count,
        };
    }

    private async Task<List<PartyRecord>> LoadPartiesAsync(string normPartyType, GetAddressAndContactsInput input)
    {
        var result = new List<PartyRecord>();

        if (normPartyType == "Supplier")
        {
            var sQuery = await _supplierRepo.GetQueryableAsync();
            var suppliers = sQuery
                .Where(s => s.CompanyId == input.CompanyId && s.IsActive)
                .WhereIf(input.PartyId.HasValue, s => s.Id == input.PartyId!.Value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(input.Search))
            {
                var term = input.Search.Trim().ToLower();
                suppliers = suppliers.Where(s => s.Name.ToLower().Contains(term) || (s.SupplierCode != null && s.SupplierCode.ToLower().Contains(term))).ToList();
            }

            var groupIds = suppliers.Where(s => s.SupplierGroupId.HasValue).Select(s => s.SupplierGroupId!.Value).Distinct().ToList();
            var groupDict = new Dictionary<Guid, string>();
            if (groupIds.Count > 0)
            {
                var sgRepo = LazyServiceProvider.LazyGetService<IRepository<SupplierGroup, Guid>>();
                if (sgRepo != null)
                {
                    var gQuery = await sgRepo.GetQueryableAsync();
                    groupDict = gQuery.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
                }
            }

            result = suppliers.Select(s => new PartyRecord(
                s.Id,
                s.Name,
                s.SupplierCode,
                s.SupplierGroupId.HasValue ? groupDict.GetValueOrDefault(s.SupplierGroupId.Value) : null
            )).OrderBy(p => p.Name).ToList();
        }
        else if (normPartyType == "SalesPartner")
        {
            var spQuery = await _salesPartnerRepo.GetQueryableAsync();
            var partners = spQuery
                .Where(p => p.IsEnabled)
                .WhereIf(input.PartyId.HasValue, p => p.Id == input.PartyId!.Value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(input.Search))
            {
                var term = input.Search.Trim().ToLower();
                partners = partners.Where(p => p.Name.ToLower().Contains(term)).ToList();
            }

            result = partners.Select(p => new PartyRecord(
                p.Id,
                p.Name,
                p.ReferralCode,
                p.PartnerType.ToString()
            )).OrderBy(p => p.Name).ToList();
        }
        else if (normPartyType == "Lead")
        {
            var lQuery = await _leadRepo.GetQueryableAsync();
            var leads = lQuery
                .Where(l => l.CompanyId == input.CompanyId && !l.Disabled)
                .WhereIf(input.PartyId.HasValue, l => l.Id == input.PartyId!.Value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(input.Search))
            {
                var term = input.Search.Trim().ToLower();
                leads = leads.Where(l => l.FirstName.ToLower().Contains(term) || (l.LastName != null && l.LastName.ToLower().Contains(term)) || (l.CompanyName != null && l.CompanyName.ToLower().Contains(term))).ToList();
            }

            result = leads.Select(l => new PartyRecord(
                l.Id,
                !string.IsNullOrWhiteSpace(l.CompanyName) ? l.CompanyName : $"{l.FirstName} {l.LastName}".Trim(),
                l.LeadNumber,
                l.Status.ToString()
            )).OrderBy(p => p.Name).ToList();
        }
        else // Customer
        {
            var cQuery = await _customerRepo.GetQueryableAsync();
            var customers = cQuery
                .Where(c => c.CompanyId == input.CompanyId && c.IsActive)
                .WhereIf(input.PartyId.HasValue, c => c.Id == input.PartyId!.Value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(input.Search))
            {
                var term = input.Search.Trim().ToLower();
                customers = customers.Where(c => c.Name.ToLower().Contains(term) || (c.CustomerCode != null && c.CustomerCode.ToLower().Contains(term))).ToList();
            }

            var groupIds = customers.Where(c => c.CustomerGroupId.HasValue).Select(c => c.CustomerGroupId!.Value).Distinct().ToList();
            var groupDict = new Dictionary<Guid, string>();
            if (groupIds.Count > 0)
            {
                var cgRepo = LazyServiceProvider.LazyGetService<IRepository<CustomerGroup, Guid>>();
                if (cgRepo != null)
                {
                    var gQuery = await cgRepo.GetQueryableAsync();
                    groupDict = gQuery.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
                }
            }

            result = customers.Select(c => new PartyRecord(
                c.Id,
                c.Name,
                c.CustomerCode,
                c.CustomerGroupId.HasValue ? groupDict.GetValueOrDefault(c.CustomerGroupId.Value) : null
            )).OrderBy(p => p.Name).ToList();
        }

        return result;
    }
}
