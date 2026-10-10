using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class CustomerAppService :
    CrudAppService<
        Customer,
        CustomerDto,
        Guid,
        GetCustomerListDto,
        CreateUpdateCustomerDto>,
    ICustomerAppService
{
    public CustomerAppService(IRepository<Customer, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Customers.Default;
        GetListPolicyName = MyERPPermissions.Customers.Default;
        CreatePolicyName = MyERPPermissions.Customers.Create;
        UpdatePolicyName = MyERPPermissions.Customers.Edit;
        DeletePolicyName = MyERPPermissions.Customers.Delete;
    }

    /// <summary>
    /// Prevent deletion of customers with active orders or posted invoices.
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var soQuery = await soRepo.GetQueryableAsync();
        var hasActiveOrders = soQuery.Any(so =>
            so.CustomerId == id
            && so.Status != DocumentStatus.Draft
            && so.Status != DocumentStatus.Cancelled);

        if (hasActiveOrders)
        {
            throw new BusinessException("MyERP:03003")
                .WithData("partyType", "Customer")
                .WithData("reason", "Customer has active Sales Orders.");
        }

        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
        var siQuery = await siRepo.GetQueryableAsync();
        var hasPostedInvoices = siQuery.Any(si =>
            si.CustomerId == id
            && (si.Status == DocumentStatus.Posted || si.Status == DocumentStatus.Submitted));

        if (hasPostedInvoices)
        {
            throw new BusinessException("MyERP:03003")
                .WithData("partyType", "Customer")
                .WithData("reason", "Customer has posted Sales Invoices.");
        }

        // Per ERPNext Customer on_trash gotcha #182: revert linked Lead status to "Interested"
        var customer = await Repository.FindAsync(id);
        if (customer != null)
        {
            var leadRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CRM.Entities.Lead, Guid>>();
            if (customer.LeadId.HasValue)
            {
                var lead = await leadRepo.FindAsync(customer.LeadId.Value);
                if (lead != null && (lead.ConvertedCustomerId == customer.Id || lead.Status == CRM.LeadStatus.Converted))
                {
                    lead.RevertCustomer();
                    await leadRepo.UpdateAsync(lead, autoSave: true);
                }
            }
            else
            {
                var leadQuery = await leadRepo.GetQueryableAsync();
                var lead = leadQuery.FirstOrDefault(l => l.ConvertedCustomerId == customer.Id);
                if (lead != null)
                {
                    lead.RevertCustomer();
                    await leadRepo.UpdateAsync(lead, autoSave: true);
                }
            }

            if (customer.LeadId.HasValue)
            {
                var oppRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CRM.Entities.Opportunity, Guid>>();
                var oppQuery = await oppRepo.GetQueryableAsync();
                var customerOpps = oppQuery.Where(o => o.CustomerId == customer.Id && o.LeadId == customer.LeadId.Value).ToList();
                foreach (var opp in customerOpps)
                {
                    opp.CustomerId = null;
                    await oppRepo.UpdateAsync(opp, autoSave: true);
                }
            }
        }

        await base.DeleteAsync(id);
    }

    public override async Task<PagedResultDto<CustomerDto>> GetListAsync(GetCustomerListDto input)
    {
        var filter = input.Filter;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return await base.GetListAsync(input);
        }

        var queryable = await Repository.GetQueryableAsync();

        queryable = queryable.Where(c =>
            c.Name.Contains(filter)
            || (c.CustomerCode != null && c.CustomerCode.Contains(filter))
            || (c.Tin != null && c.Tin.Contains(filter)));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(c => c.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<CustomerDto>(
            totalCount,
            items.Select(ObjectMapper.Map<Customer, CustomerDto>).ToList());
    }

    public override async Task<CustomerDto> CreateAsync(CreateUpdateCustomerDto input)
    {
        await ValidateCustomerAsync(input);
        var result = await base.CreateAsync(input);

        // Per ERPNext customer.py update_lead_status: mark linked lead converted
        // and link lead's open opportunities to the new customer (PR #60279 / commit 9f9cf26639)
        if (input.LeadId.HasValue)
        {
            var leadRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CRM.Entities.Lead, Guid>>();
            var lead = await leadRepo.FindAsync(input.LeadId.Value);
            if (lead != null && lead.Status != CRM.LeadStatus.Converted)
            {
                lead.ConvertToCustomer(result.Id);
                await leadRepo.UpdateAsync(lead, autoSave: true);
            }

            // Per ERPNext PR #59907 / commit eb445464ca: update lead's Prospect row status to Converted
            var prospectRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CRM.Entities.Prospect, Guid>>();
            var prospects = (await prospectRepo.WithDetailsAsync(p => p.Leads))
                .Where(p => p.Leads.Any(l => l.LeadId == input.LeadId.Value))
                .ToList();
            foreach (var p in prospects)
            {
                p.UpdateLeadStatus(input.LeadId.Value, CRM.LeadStatus.Converted);
                await prospectRepo.UpdateAsync(p, autoSave: true);
            }

            var oppRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CRM.Entities.Opportunity, Guid>>();
            var oppQuery = await oppRepo.GetQueryableAsync();
            var leadOpps = oppQuery.Where(o => o.LeadId == input.LeadId.Value && o.CustomerId == null).ToList();
            foreach (var opp in leadOpps)
            {
                opp.CustomerId = result.Id;
                await oppRepo.UpdateAsync(opp, autoSave: true);
            }

            // Per ERPNext PR #60279 / commit 9f9cf26639: link lead's quotations to the new customer
            var leadOppIds = leadOpps.Select(o => o.Id).ToList();
            if (leadOppIds.Count > 0)
            {
                var quotRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Quotation, Guid>>();
                var quotQuery = await quotRepo.GetQueryableAsync();
                var linkedQuotes = quotQuery.Where(q => q.OpportunityId.HasValue && leadOppIds.Contains(q.OpportunityId.Value)).ToList();
                foreach (var q in linkedQuotes)
                {
                    q.CustomerId = result.Id;
                    await quotRepo.UpdateAsync(q, autoSave: true);
                }
            }
        }

        // Link source opportunity and its quotations to customer (PR #60279 / commit 9f9cf26639)
        if (input.OpportunityId.HasValue)
        {
            var oppRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CRM.Entities.Opportunity, Guid>>();
            var opp = await oppRepo.FindAsync(input.OpportunityId.Value);
            if (opp != null && opp.CustomerId == null)
            {
                opp.CustomerId = result.Id;
                await oppRepo.UpdateAsync(opp, autoSave: true);
            }

            var quotRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Quotation, Guid>>();
            var quotQuery = await quotRepo.GetQueryableAsync();
            var oppQuotes = quotQuery.Where(q => q.OpportunityId == input.OpportunityId.Value).ToList();
            foreach (var q in oppQuotes)
            {
                q.CustomerId = result.Id;
                await quotRepo.UpdateAsync(q, autoSave: true);
            }
        }

        // Mark linked prospect converted
        if (input.ProspectId.HasValue)
        {
            var prospectRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CRM.Entities.Prospect, Guid>>();
            var prospect = await prospectRepo.FindAsync(input.ProspectId.Value);
            if (prospect != null && !prospect.ConvertedCustomerId.HasValue)
            {
                prospect.ConvertToCustomer(result.Id);
                await prospectRepo.UpdateAsync(prospect, autoSave: true);
            }
        }

        return result;
    }

    public override async Task<CustomerDto> UpdateAsync(Guid id, CreateUpdateCustomerDto input)
    {
        await ValidateCustomerAsync(input, id);

        // Per gotcha #1286: validate credit limit reduction against existing outstanding
        if (input.CreditLimit > 0)
        {
            var creditLimitService = LazyServiceProvider.LazyGetRequiredService<DomainServices.CreditLimitService>();
            var outstanding = await creditLimitService.GetCustomerOutstandingAsync(id, input.CompanyId);
            if (input.CreditLimit < outstanding)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CreditLimitExceeded)
                    .WithData("outstanding", outstanding)
                    .WithData("creditLimit", input.CreditLimit)
                    .WithData("detail", $"Cannot reduce credit limit to {input.CreditLimit} because customer currently has {outstanding} in outstanding invoices.");
            }
        }

        return await base.UpdateAsync(id, input);
    }

    private async Task ValidateCustomerAsync(CreateUpdateCustomerDto input, Guid? currentId = null)
    {
        // ERPNext validate_account_head: the party account must belong to the customer's company
        // and be a postable (non-group, enabled) ledger.
        if (input.DefaultReceivableAccountId.HasValue)
        {
            var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var account = await accountRepo.GetAsync(input.DefaultReceivableAccountId.Value);
            if (account.CompanyId != input.CompanyId)
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch);
            if (account.IsGroup)
                throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup);
            if (!account.IsActive)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Account {account.AccountName} is disabled.");
        }

        if (input.RepresentsCompanyId.HasValue)
        {
            if (input.RepresentsCompanyId.Value == input.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PartyCannotRepresentOwnCompany);
            }

            if (input.IsActive)
            {
                var custQuery = await Repository.GetQueryableAsync();
                var existing = custQuery.FirstOrDefault(c =>
                    c.RepresentsCompanyId == input.RepresentsCompanyId.Value
                    && c.IsActive
                    && (!currentId.HasValue || c.Id != currentId.Value));

                if (existing != null)
                {
                    var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>();
                    var company = await companyRepo.FindAsync(input.RepresentsCompanyId.Value);
                    var companyName = company?.Name ?? input.RepresentsCompanyId.Value.ToString();

                    throw new BusinessException(MyERPDomainErrorCodes.InternalPartyAlreadyExists)
                        .WithData("partyType", "Customer")
                        .WithData("partyName", existing.Name)
                        .WithData("companyName", companyName);
                }
            }
        }

        var groupRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.CustomerGroup, Guid>>();
        var query = await groupRepo.GetQueryableAsync();
        var trimmed = input.Name.Trim();
        var groupExists = query.Any(g => g.Name.ToLower() == trimmed.ToLower());
        if (groupExists)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CustomerNameCannotMatchCustomerGroup)
                .WithData("name", input.Name);
        }

        // Per ERPNext PR #53811: prevent selection of group type customer group in customer master
        if (input.CustomerGroupId.HasValue)
        {
            var customerGroup = query.FirstOrDefault(g => g.Id == input.CustomerGroupId.Value);
            if (customerGroup != null && customerGroup.IsGroup)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Cannot select a Group type Customer Group. Please select a non-group Customer Group.");
            }
        }

        // Per ERPNext PR #59891 / commit 718d79701b: refuse converting a lead into a second customer
        if (input.LeadId.HasValue)
        {
            var custQuery = await Repository.GetQueryableAsync();
            var existingLeadCust = custQuery.FirstOrDefault(c =>
                c.LeadId == input.LeadId.Value
                && (!currentId.HasValue || c.Id != currentId.Value));
            if (existingLeadCust != null)
            {
                throw new BusinessException(MyERPDomainErrorCodes.LeadAlreadyConverted)
                    .WithData("leadId", input.LeadId.Value)
                    .WithData("customerName", existingLeadCust.Name);
            }
        }

        // Per ERPNext PR #59890 / commit 167380e7f4: refuse making duplicate customer from opportunity
        if (input.OpportunityId.HasValue)
        {
            var custQuery = await Repository.GetQueryableAsync();
            var existingOppCust = custQuery.FirstOrDefault(c =>
                c.OpportunityId == input.OpportunityId.Value
                && (!currentId.HasValue || c.Id != currentId.Value));
            if (existingOppCust != null)
            {
                throw new BusinessException(MyERPDomainErrorCodes.OpportunityCustomerAlreadyExists)
                    .WithData("opportunityId", input.OpportunityId.Value)
                    .WithData("customerName", existingOppCust.Name);
            }

            var oppRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CRM.Entities.Opportunity, Guid>>();
            var opp = await oppRepo.FindAsync(input.OpportunityId.Value);
            if (opp != null)
            {
                if (opp.CustomerId.HasValue && (!currentId.HasValue || opp.CustomerId.Value != currentId.Value))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.OpportunityAlreadyHasCustomer)
                        .WithData("opportunityId", opp.Id)
                        .WithData("opportunityNumber", opp.OpportunityNumber);
                }

                if (opp.LeadId.HasValue)
                {
                    var existingLeadCust = custQuery.FirstOrDefault(c =>
                        c.LeadId == opp.LeadId.Value
                        && (!currentId.HasValue || c.Id != currentId.Value));
                    if (existingLeadCust != null)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.OpportunityCustomerAlreadyExists)
                            .WithData("opportunityId", opp.Id)
                            .WithData("leadId", opp.LeadId.Value)
                            .WithData("customerName", existingLeadCust.Name);
                    }
                }
            }
        }

        // Per ERPNext PR #59907 / commit b07b8053ad: refuse duplicate customer conversion for prospect; hide customer name if caller cannot read Customer
        if (input.ProspectId.HasValue)
        {
            var custQuery = await Repository.GetQueryableAsync();
            var existingProspectCust = custQuery.FirstOrDefault(c =>
                c.ProspectId == input.ProspectId.Value
                && (!currentId.HasValue || c.Id != currentId.Value));

            var prospectRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CRM.Entities.Prospect, Guid>>();
            var prospect = await prospectRepo.FindAsync(input.ProspectId.Value);

            if (existingProspectCust != null || (prospect != null && prospect.ConvertedCustomerId.HasValue && (!currentId.HasValue || prospect.ConvertedCustomerId.Value != currentId.Value)))
            {
                var ex = new BusinessException(MyERPDomainErrorCodes.ProspectAlreadyConverted)
                    .WithData("prospectId", input.ProspectId.Value)
                    .WithData("prospectName", prospect?.ProspectName ?? input.ProspectId.Value.ToString());

                var canReadCustomer = false;
                try
                {
                    var principal = LazyServiceProvider.LazyGetService<Volo.Abp.Security.Claims.ICurrentPrincipalAccessor>()?.Principal
                        ?? new System.Security.Claims.ClaimsPrincipal();
                    var authResult = await AuthorizationService.AuthorizeAsync(
                        principal,
                        existingProspectCust,
                        MyERPPermissions.Customers.Default);
                    canReadCustomer = authResult.Succeeded;
                }
                catch
                {
                }

                if (canReadCustomer)
                {
                    var existingName = existingProspectCust?.Name;
                    if (existingName == null && prospect?.ConvertedCustomerId.HasValue == true)
                    {
                        var convertedCust = await Repository.FindAsync(prospect.ConvertedCustomerId.Value);
                        existingName = convertedCust?.Name;
                    }
                    if (existingName != null)
                    {
                        ex.WithData("customerName", existingName);
                    }
                }

                throw ex;
            }
        }
    }

    protected override Customer MapToEntity(CreateUpdateCustomerDto input)
    {
        var customer = new Customer(
            GuidGenerator.Create(), input.CompanyId, input.Name, CurrentTenant.Id);
        MapToEntity(input, customer);
        return customer;
    }

    protected override void MapToEntity(CreateUpdateCustomerDto input, Customer entity)
    {
        entity.SetName(input.Name);
        entity.CustomerCode = input.CustomerCode;
        entity.Tin = input.Tin;
        entity.RegistrationNumber = input.RegistrationNumber;
        entity.SstRegistrationNumber = input.SstRegistrationNumber;
        entity.IdType = input.IdType;
        entity.IdValue = input.IdValue;
        entity.ContactPerson = input.ContactPerson;
        entity.Phone = input.Phone;
        entity.Email = input.Email;
        entity.Website = input.Website;
        entity.Address = input.Address;
        entity.City = input.City;
        entity.State = input.State;
        entity.PostalCode = input.PostalCode;
        entity.Country = input.Country;
        entity.DefaultReceivableAccountId = input.DefaultReceivableAccountId;
        entity.IsActive = input.IsActive;
        entity.CreditLimit = input.CreditLimit;
        entity.RepresentsCompanyId = input.RepresentsCompanyId;
        entity.CustomerGroupId = input.CustomerGroupId;
        entity.TerritoryId = input.TerritoryId;
        entity.LoyaltyProgramId = input.LoyaltyProgramId;
        entity.DefaultPaymentTermsTemplateId = input.DefaultPaymentTermsTemplateId;
        entity.DefaultPriceListId = input.DefaultPriceListId;
        entity.RestrictToCompanies = input.RestrictToCompanies;
        entity.SoRequired = input.SoRequired;
        entity.IsFrozen = input.IsFrozen;
        entity.DnRequired = input.DnRequired;
        entity.OnHold = input.OnHold;
        entity.ReleaseDate = input.OnHold ? input.ReleaseDate : null;
        entity.LeadId = input.LeadId;
        entity.OpportunityId = input.OpportunityId;
        entity.ProspectId = input.ProspectId;
    }

    /// <summary>
    /// Returns the KPIs, receivables ageing, monthly sales trend, and sales pipeline for a customer and company.
    /// Per ERPNext selling/doctype/customer/customer_overview.py (PR #59376 / commit 64ec181ba9).
    /// </summary>
    public async Task<CustomerOverviewDto> GetCustomerOverviewAsync(GetCustomerOverviewInputDto input)
    {
        var customer = await Repository.GetAsync(input.CustomerId);
        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>();
        var company = await companyRepo.GetAsync(input.CompanyId);

        var asOfDate = DateTime.UtcNow.Date;
        var (fromDate, toDate) = await ResolvePeriodAsync(input.Period, input.CompanyId, asOfDate);

        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
        var siQ = await siRepo.GetQueryableAsync();

        // All non-draft non-cancelled posted/submitted sales invoices for this customer & company
        var customerInvoices = siQ
            .Where(si => si.CustomerId == input.CustomerId
                && si.CompanyId == input.CompanyId
                && (si.Status == DocumentStatus.Posted || si.Status == DocumentStatus.Submitted))
            .ToList();

        // 1. Position: Net Sales, Outstanding, Overdue, Credit
        var currentPeriodInvoices = customerInvoices
            .Where(si => !si.IsReturn && si.IssueDate >= fromDate && si.IssueDate <= toDate)
            .ToList();
        var currentNetSales = currentPeriodInvoices.Sum(si => si.NetTotal > 0 ? si.NetTotal : si.GrandTotal);

        var prevFromDate = fromDate.AddYears(-1);
        var prevToDate = toDate.AddYears(-1);
        var prevPeriodInvoices = customerInvoices
            .Where(si => !si.IsReturn && si.IssueDate >= prevFromDate && si.IssueDate <= prevToDate)
            .ToList();
        var prevNetSales = prevPeriodInvoices.Sum(si => si.NetTotal > 0 ? si.NetTotal : si.GrandTotal);

        var outstandingInvoices = customerInvoices
            .Where(si => !si.IsReturn && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0)
            .ToList();
        var outstandingTotal = outstandingInvoices.Sum(si => si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance);

        // Days to pay (DSO) = outstanding / (trailing annual sales / 365)
        var trailing365Start = asOfDate.AddDays(-365);
        var trailingSales = customerInvoices
            .Where(si => !si.IsReturn && si.IssueDate >= trailing365Start && si.IssueDate <= asOfDate)
            .Sum(si => si.NetTotal > 0 ? si.NetTotal : si.GrandTotal);
        int? daysToPay = null;
        if (trailingSales > 0)
        {
            var dailySales = trailingSales / 365m;
            if (dailySales > 0)
            {
                var dso = (int)Math.Round(outstandingTotal / dailySales);
                if (dso <= 730) daysToPay = dso;
            }
        }

        var overdueInvoices = outstandingInvoices
            .Where(si => si.DueDate.HasValue && si.DueDate.Value < asOfDate)
            .ToList();
        var overdueTotal = overdueInvoices.Sum(si => si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance);

        var asOf30DaysAgo = asOfDate.AddDays(-30);
        var overdue30Total = customerInvoices
            .Where(si => !si.IsReturn
                && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0
                && si.DueDate.HasValue && si.DueDate.Value < asOf30DaysAgo)
            .Sum(si => si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance);

        // Credit limit resolution
        var creditLimitRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CustomerCreditLimit, Guid>>();
        var creditQ = await creditLimitRepo.GetQueryableAsync();
        var companyCreditLimit = creditQ.FirstOrDefault(cl => cl.CustomerId == input.CustomerId && cl.CompanyId == input.CompanyId);
        var creditLimit = companyCreditLimit != null && !companyCreditLimit.BypassCreditLimitCheck
            ? companyCreditLimit.CreditLimit
            : customer.CreditLimit;

        // 2. Trend: monthly net sales
        var points = new List<CustomerTrendPointDto>();
        var closedMonthTotals = new List<decimal>();
        var cursor = new DateTime(fromDate.Year, fromDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endMonth = new DateTime(toDate.Year, toDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthStart = new DateTime(asOfDate.Year, asOfDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        while (cursor <= endMonth)
        {
            var isMtd = cursor == currentMonthStart;
            var monthSales = customerInvoices
                .Where(si => !si.IsReturn && si.IssueDate.Year == cursor.Year && si.IssueDate.Month == cursor.Month)
                .Sum(si => si.NetTotal > 0 ? si.NetTotal : si.GrandTotal);

            points.Add(new CustomerTrendPointDto
            {
                Label = cursor.ToString("MMM"),
                Value = Math.Round(monthSales, 2),
                IsMtd = isMtd
            });

            if (!isMtd)
            {
                closedMonthTotals.Add(monthSales);
            }

            cursor = cursor.AddMonths(1);
        }

        // 3. Ageing: Not Due, 1-30, 31-60, 61-90, 90+
        decimal notDueAmount = 0m, b1Amount = 0m, b2Amount = 0m, b3Amount = 0m, b4Amount = 0m;
        foreach (var inv in outstandingInvoices)
        {
            var bal = inv.GrandTotal - inv.AmountPaid - inv.WriteOffAmount - inv.TotalAdvance;
            if (!inv.DueDate.HasValue || inv.DueDate.Value >= asOfDate)
            {
                notDueAmount += bal;
            }
            else
            {
                var overdueDays = (int)(asOfDate - inv.DueDate.Value).TotalDays;
                if (overdueDays <= 30) b1Amount += bal;
                else if (overdueDays <= 60) b2Amount += bal;
                else if (overdueDays <= 90) b3Amount += bal;
                else b4Amount += bal;
            }
        }

        var buckets = new List<CustomerAgeingBucketDto>
        {
            new() { Key = "not_due", Label = "Not due", Value = Math.Round(notDueAmount, 2), IsOverdue = false },
            new() { Key = "b1", Label = "1–30 days", Value = Math.Round(b1Amount, 2), IsOverdue = true },
            new() { Key = "b2", Label = "31–60 days", Value = Math.Round(b2Amount, 2), IsOverdue = true },
            new() { Key = "b3", Label = "61–90 days", Value = Math.Round(b3Amount, 2), IsOverdue = true },
            new() { Key = "b4", Label = "90+ days", Value = Math.Round(b4Amount, 2), IsOverdue = true },
        };
        var totalAgeing = Math.Round(notDueAmount + b1Amount + b2Amount + b3Amount + b4Amount, 2);
        var totalOverdue = Math.Round(b1Amount + b2Amount + b3Amount + b4Amount, 2);

        // 4. Pipeline: Quotations, Delivery, Billing, Invoices
        var quoteRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Quotation, Guid>>();
        var quoteQ = await quoteRepo.GetQueryableAsync();
        var openQuotes = quoteQ
            .Where(q => q.CustomerId == input.CustomerId
                && q.CompanyId == input.CompanyId
                && q.Status == DocumentStatus.Submitted
                && q.ConvertedToSalesOrderId == null)
            .ToList();

        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var soQ = await soRepo.GetQueryableAsync();
        var openSos = soQ
            .Where(so => so.CustomerId == input.CustomerId
                && so.CompanyId == input.CompanyId
                && so.Status != DocumentStatus.Draft
                && so.Status != DocumentStatus.Cancelled
                && so.Status != DocumentStatus.Closed)
            .ToList();

        var deliverySos = openSos.Where(so => so.PerDelivered < 100 && !so.SkipDeliveryNote).ToList();
        var deliveryValue = deliverySos.Sum(so => so.GrandTotal * (100m - so.PerDelivered) / 100m);
        var pastDueDelivery = deliverySos.Count(so => so.DeliveryDate.HasValue && so.DeliveryDate.Value < asOfDate);

        var billingSos = openSos.Where(so => so.PerBilled < 100).ToList();
        var billingValue = billingSos.Sum(so => so.GrandTotal * (100m - so.PerBilled) / 100m);

        // 5. Advances: unallocated customer payments
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQ = await peRepo.GetQueryableAsync();
        var advancesList = peQ
            .Where(pe => pe.CompanyId == input.CompanyId
                && pe.PartyType == "Customer"
                && pe.PartyId == input.CustomerId
                && (pe.Status == DocumentStatus.Submitted || pe.Status == DocumentStatus.Posted))
            .ToList();
        var advances = advancesList.Sum(pe => pe.UnallocatedAmount);

        return new CustomerOverviewDto
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CompanyId = input.CompanyId,
            Currency = company.CurrencyCode,
            Period = input.Period,
            FromDate = fromDate,
            ToDate = toDate,
            AsOfDate = asOfDate,
            Position = new CustomerPositionDto
            {
                NetSales = new CustomerMetricDto
                {
                    Value = Math.Round(currentNetSales, 2),
                    Count = currentPeriodInvoices.Count,
                    Delta = CalculatePctChange(currentNetSales, prevNetSales),
                    DeltaPositiveIsGood = true
                },
                Outstanding = new CustomerOutstandingMetricDto
                {
                    Value = Math.Round(outstandingTotal, 2),
                    UnpaidCount = outstandingInvoices.Count,
                    DaysToPay = daysToPay
                },
                Overdue = new CustomerMetricDto
                {
                    Value = Math.Round(overdueTotal, 2),
                    Count = overdueInvoices.Count,
                    Delta = CalculatePctChange(overdueTotal, overdue30Total),
                    DeltaPositiveIsGood = false
                },
                Credit = new CustomerCreditMetricDto
                {
                    Limit = creditLimit,
                    UsedPct = creditLimit > 0 ? Math.Round(outstandingTotal / creditLimit * 100m, 1) : null
                }
            },
            Trend = new CustomerTrendDto
            {
                Points = points,
                Average = closedMonthTotals.Count > 0 ? Math.Round(closedMonthTotals.Average(), 2) : 0m,
                HasMtd = points.Any(p => p.IsMtd)
            },
            Ageing = new CustomerAgeingDto
            {
                Buckets = buckets,
                Total = totalAgeing,
                Overdue = totalOverdue,
                OverduePct = totalAgeing > 0 ? Math.Round(totalOverdue / totalAgeing * 100m, 1) : 0m
            },
            Pipeline = new CustomerPipelineDto
            {
                Quotations = new CustomerPipelineTileDto
                {
                    Value = Math.Round(openQuotes.Sum(q => q.GrandTotal), 2),
                    Count = openQuotes.Count
                },
                Delivery = new CustomerDeliveryTileDto
                {
                    Value = Math.Round(deliveryValue, 2),
                    Count = deliverySos.Count,
                    PastDue = pastDueDelivery
                },
                Billing = new CustomerPipelineTileDto
                {
                    Value = Math.Round(billingValue, 2),
                    Count = billingSos.Count
                },
                Invoices = new CustomerInvoiceTileDto
                {
                    Value = Math.Round(outstandingTotal, 2),
                    Count = outstandingInvoices.Count,
                    Overdue = overdueInvoices.Count
                }
            },
            UnallocatedAdvances = Math.Round(advances, 2)
        };
    }

    /// <summary>
    /// Fetches recent transactions across Sales Invoices, Sales Orders, and Payment Entries for customer.
    /// Per ERPNext selling/doctype/customer/customer_overview.py get_customer_transactions().
    /// </summary>
    public async Task<List<CustomerTransactionDto>> GetCustomerTransactionsAsync(GetCustomerTransactionsInputDto input)
    {
        var limit = Math.Clamp(input.MaxResultCount, 1, 100);
        var rows = new List<CustomerTransactionDto>();

        var docType = input.DocType ?? "All";

        if (string.Equals(docType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(docType, "Sales Invoice", StringComparison.OrdinalIgnoreCase))
        {
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
            var siQ = await siRepo.GetQueryableAsync();
            var siList = siQ
                .Where(si => si.CustomerId == input.CustomerId && si.CompanyId == input.CompanyId && (si.Status == DocumentStatus.Posted || si.Status == DocumentStatus.Submitted))
                .OrderByDescending(si => si.IssueDate)
                .ThenByDescending(si => si.CreationTime)
                .Take(limit)
                .ToList();

            foreach (var si in siList)
            {
                var outstanding = Math.Max(0m, si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance);
                rows.Add(new CustomerTransactionDto
                {
                    Id = si.Id,
                    TransactionNumber = si.InvoiceNumber ?? si.Id.ToString(),
                    DocType = "Sales Invoice",
                    TypeLabel = "Sales Invoice",
                    Date = si.IssueDate,
                    Status = si.IsReturn ? "Return" : si.Status.ToString(),
                    Amount = si.GrandTotal,
                    OutstandingAmount = outstanding
                });
            }
        }

        if (string.Equals(docType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(docType, "Sales Order", StringComparison.OrdinalIgnoreCase))
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
            var soQ = await soRepo.GetQueryableAsync();
            var soList = soQ
                .Where(so => so.CustomerId == input.CustomerId && so.CompanyId == input.CompanyId && so.Status != DocumentStatus.Draft && so.Status != DocumentStatus.Cancelled)
                .OrderByDescending(so => so.OrderDate)
                .ThenByDescending(so => so.CreationTime)
                .Take(limit)
                .ToList();

            foreach (var so in soList)
            {
                rows.Add(new CustomerTransactionDto
                {
                    Id = so.Id,
                    TransactionNumber = so.OrderNumber ?? so.Id.ToString(),
                    DocType = "Sales Order",
                    TypeLabel = "Sales Order",
                    Date = so.OrderDate,
                    Status = so.Status.ToString(),
                    Amount = so.GrandTotal,
                    OutstandingAmount = null
                });
            }
        }

        if (string.Equals(docType, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(docType, "Payment Entry", StringComparison.OrdinalIgnoreCase))
        {
            var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
            var peQ = await peRepo.GetQueryableAsync();
            var peList = peQ
                .Where(pe => pe.CompanyId == input.CompanyId && pe.PartyType == "Customer" && pe.PartyId == input.CustomerId && (pe.Status == DocumentStatus.Submitted || pe.Status == DocumentStatus.Posted))
                .OrderByDescending(pe => pe.PostingDate)
                .ThenByDescending(pe => pe.CreationTime)
                .Take(limit)
                .ToList();

            foreach (var pe in peList)
            {
                rows.Add(new CustomerTransactionDto
                {
                    Id = pe.Id,
                    TransactionNumber = pe.PaymentNumber ?? pe.Id.ToString(),
                    DocType = "Payment Entry",
                    TypeLabel = "Payment Entry",
                    Date = pe.PostingDate,
                    Status = pe.Status.ToString(),
                    Amount = pe.PaidAmount,
                    OutstandingAmount = pe.UnallocatedAmount > 0 ? pe.UnallocatedAmount : null
                });
            }
        }

        return rows
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Fetches all distinct company names where the customer has submitted transactions.
    /// Per ERPNext get_customer_companies().
    /// </summary>
    public async Task<List<string>> GetCustomerCompaniesAsync(Guid customerId)
    {
        var companyIds = new HashSet<Guid>();

        var custRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Customer, Guid>>();
        var customer = await custRepo.FindAsync(customerId);
        if (customer != null && customer.CompanyId != Guid.Empty)
        {
            companyIds.Add(customer.CompanyId);
        }

        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
        var siQ = await siRepo.GetQueryableAsync();
        var siCompanyIds = siQ
            .Where(si => si.CustomerId == customerId && (si.Status == DocumentStatus.Posted || si.Status == DocumentStatus.Submitted))
            .Select(si => si.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in siCompanyIds)
            companyIds.Add(cId);

        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var soQ = await soRepo.GetQueryableAsync();
        var soCompanyIds = soQ
            .Where(so => so.CustomerId == customerId && so.Status != DocumentStatus.Draft && so.Status != DocumentStatus.Cancelled)
            .Select(so => so.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in soCompanyIds)
            companyIds.Add(cId);

        var quoteRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Quotation, Guid>>();
        var quoteQ = await quoteRepo.GetQueryableAsync();
        var quoteCompanyIds = quoteQ
            .Where(q => q.CustomerId == customerId && q.Status == DocumentStatus.Submitted)
            .Select(q => q.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in quoteCompanyIds)
            companyIds.Add(cId);

        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQ = await peRepo.GetQueryableAsync();
        var peCompanyIds = peQ
            .Where(pe => pe.PartyType == "Customer" && pe.PartyId == customerId && (pe.Status == DocumentStatus.Submitted || pe.Status == DocumentStatus.Posted))
            .Select(pe => pe.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in peCompanyIds)
            companyIds.Add(cId);

        var pleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentLedgerEntry, Guid>>();
        var pleQ = await pleRepo.GetQueryableAsync();
        var pleCompanyIds = pleQ
            .Where(ple => ple.PartyType == "Customer" && ple.PartyId == customerId && !ple.Delinked)
            .Select(ple => ple.CompanyId)
            .Distinct()
            .ToList();
        foreach (var cId in pleCompanyIds)
            companyIds.Add(cId);

        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntry, Guid>>();
        var jeLineRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntryLine, Guid>>();
        var jeLineQ = await jeLineRepo.GetQueryableAsync();
        var jeQ = await jeRepo.GetQueryableAsync();
        var jeCompanyIds = (from line in jeLineQ
                            join je in jeQ on line.JournalEntryId equals je.Id
                            where line.PartyType == "Customer" && line.PartyId == customerId
                               && (je.Status == DocumentStatus.Submitted || je.Status == DocumentStatus.Posted)
                            select je.CompanyId)
                            .Distinct()
                            .ToList();
        foreach (var cId in jeCompanyIds)
            companyIds.Add(cId);

        if (companyIds.Count == 0) return new List<string>();

        var compRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>();
        var compQ = await compRepo.GetQueryableAsync();
        return compQ.Where(c => companyIds.Contains(c.Id)).Select(c => c.Name).OrderBy(n => n).ToList();
    }

    private async Task<(DateTime fromDate, DateTime toDate)> ResolvePeriodAsync(string period, Guid companyId, DateTime asOfDate)
    {
        if (string.Equals(period, "Last 12 months", StringComparison.OrdinalIgnoreCase))
        {
            return (asOfDate.AddMonths(-12), asOfDate);
        }

        if (string.Equals(period, "This quarter", StringComparison.OrdinalIgnoreCase))
        {
            var quarterStartMonth = ((asOfDate.Month - 1) / 3) * 3 + 1;
            var from = new DateTime(asOfDate.Year, quarterStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            return (from, asOfDate);
        }

        var fyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<FiscalYear, Guid>>();
        var fyQ = await fyRepo.GetQueryableAsync();
        var currentFy = fyQ.FirstOrDefault(fy => fy.CompanyId == companyId && fy.StartDate <= asOfDate && fy.EndDate >= asOfDate);

        if (currentFy == null)
        {
            return (asOfDate.AddMonths(-12), asOfDate);
        }

        if (string.Equals(period, "Last fiscal year", StringComparison.OrdinalIgnoreCase))
        {
            var prevDate = currentFy.StartDate.AddDays(-1);
            var prevFy = fyQ.FirstOrDefault(fy => fy.CompanyId == companyId && fy.StartDate <= prevDate && fy.EndDate >= prevDate);
            if (prevFy != null)
            {
                return (prevFy.StartDate, prevFy.EndDate);
            }
        }

        return (currentFy.StartDate, asOfDate < currentFy.EndDate ? asOfDate : currentFy.EndDate);
    }

    private static decimal? CalculatePctChange(decimal current, decimal previous)
    {
        if (previous == 0) return null;
        return Math.Round((current - previous) / Math.Abs(previous) * 100m, 1);
    }
}

