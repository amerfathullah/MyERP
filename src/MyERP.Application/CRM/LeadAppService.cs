using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Leads.Default)]
public class LeadAppService : ApplicationService, ILeadAppService
{
    private readonly IRepository<Lead, Guid> _leadRepository;
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<CrmNote, Guid> _noteRepository;

    public LeadAppService(
        IRepository<Lead, Guid> leadRepository,
        IRepository<Opportunity, Guid> opportunityRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<CrmNote, Guid> noteRepository)
    {
        _leadRepository = leadRepository;
        _opportunityRepository = opportunityRepository;
        _customerRepository = customerRepository;
        _noteRepository = noteRepository;
    }

    public async Task<LeadDto> GetAsync(Guid id)
    {
        var lead = await _leadRepository.GetAsync(id);
        return ObjectMapper.Map<Lead, LeadDto>(lead);
    }

    public async Task<PagedResultDto<LeadDto>> GetListAsync(GetLeadListDto input)
    {
        var query = await _leadRepository.GetQueryableAsync();

        if (input.Status.HasValue)
            query = query.Where(l => l.Status == input.Status.Value);
        if (input.Source.HasValue)
            query = query.Where(l => l.Source == input.Source.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(l => l.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(l =>
                l.FirstName.Contains(filter) ||
                (l.LastName != null && l.LastName.Contains(filter)) ||
                (l.CompanyName != null && l.CompanyName.Contains(filter)) ||
                (l.Email != null && l.Email.Contains(filter)));
        }

        var totalCount = query.Count();

        if (!string.IsNullOrWhiteSpace(input.Sorting))
            query = ApplySorting(query, input.Sorting);
        else
            query = query.OrderByDescending(l => l.CreationTime);

        var items = query.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<LeadDto>(totalCount, items.Select(ObjectMapper.Map<Lead, LeadDto>).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<LeadDto> CreateAsync(CreateLeadDto input)
    {
        // Email uniqueness validation (per ERPNext: prevents duplicate leads with same email)
        var leadManager = LazyServiceProvider.LazyGetRequiredService<MyERP.CRM.DomainServices.LeadManager>();
        await leadManager.ValidateEmailUniquenessAsync(input.Email);

        var leadNumber = $"LEAD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var lead = new Lead(
            GuidGenerator.Create(),
            input.CompanyId,
            leadNumber,
            input.FirstName,
            CurrentTenant.Id)
        {
            LastName = input.LastName,
            CompanyName = input.CompanyName,
            Email = input.Email,
            Phone = input.Phone,
            MobileNo = input.MobileNo,
            JobTitle = input.JobTitle,
            Website = input.Website,
            Source = input.Source,
            City = input.City,
            State = input.State,
            Country = input.Country,
            Industry = input.Industry,
            AnnualRevenue = input.AnnualRevenue,
            AssignedUserId = input.AssignedUserId,
            Notes = input.Notes,
        };

        await _leadRepository.InsertAsync(lead);
        return ObjectMapper.Map<Lead, LeadDto>(lead);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<LeadDto> UpdateAsync(Guid id, UpdateLeadDto input)
    {
        var lead = await _leadRepository.GetAsync(id);

        lead.FirstName = input.FirstName;
        lead.LastName = input.LastName;
        lead.CompanyName = input.CompanyName;
        lead.Email = input.Email;
        lead.Phone = input.Phone;
        lead.MobileNo = input.MobileNo;
        lead.JobTitle = input.JobTitle;
        lead.Website = input.Website;
        lead.Source = input.Source;
        lead.City = input.City;
        lead.State = input.State;
        lead.Country = input.Country;
        lead.Industry = input.Industry;
        lead.AnnualRevenue = input.AnnualRevenue;
        lead.AssignedUserId = input.AssignedUserId;
        lead.Notes = input.Notes;

        await _leadRepository.UpdateAsync(lead);
        return ObjectMapper.Map<Lead, LeadDto>(lead);
    }

    [Authorize(MyERPPermissions.Leads.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _leadRepository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<LeadDto> QualifyAsync(Guid id)
    {
        var lead = await _leadRepository.GetAsync(id);
        lead.Qualify();
        await _leadRepository.UpdateAsync(lead);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Lead", lead.Id,
            "Qualified", lead.CompanyId,
            lead.LeadNumber, "Open", "Qualified", CurrentUser.Id,
            $"Lead {lead.LeadNumber} ({lead.GetFullName()}) marked as Qualified", CurrentTenant.Id));

        return ObjectMapper.Map<Lead, LeadDto>(lead);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<LeadDto> MarkLostAsync(Guid id)
    {
        var lead = await _leadRepository.GetAsync(id);
        lead.MarkLost();
        await _leadRepository.UpdateAsync(lead);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Lead", lead.Id,
            "MarkedLost", lead.CompanyId,
            lead.LeadNumber, lead.Status.ToString(), "Lost", CurrentUser.Id,
            $"Lead {lead.LeadNumber} ({lead.GetFullName()}) marked as Lost", CurrentTenant.Id));

        return ObjectMapper.Map<Lead, LeadDto>(lead);
    }

    [Authorize(MyERPPermissions.Leads.Convert)]
    public async Task<OpportunityDto> ConvertToOpportunityAsync(ConvertLeadToOpportunityDto input)
    {
        var lead = await _leadRepository.GetAsync(input.LeadId);

        var oppNumber = $"OPP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var opportunity = new Opportunity(
            GuidGenerator.Create(),
            lead.CompanyId,
            oppNumber,
            input.Title,
            CurrentTenant.Id)
        {
            LeadId = lead.Id,
            OpportunityType = input.OpportunityType,
            OpportunityAmount = input.OpportunityAmount,
            SalesStage = input.SalesStage ?? "Prospecting",
            ExpectedClosingDate = input.ExpectedClosingDate,
            ContactName = lead.GetFullName(),
            ContactEmail = lead.Email,
            ContactPhone = lead.Phone ?? lead.MobileNo,
            AssignedUserId = lead.AssignedUserId,
            Territory = lead.State,
        };

        await _opportunityRepository.InsertAsync(opportunity);
        lead.ConvertToOpportunity(opportunity.Id);
        await _leadRepository.UpdateAsync(lead);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Lead", lead.Id,
            "ConvertedToOpportunity", lead.CompanyId,
            lead.LeadNumber, lead.Status.ToString(), "Converted", CurrentUser.Id,
            $"Lead {lead.LeadNumber} ({lead.GetFullName()}) converted to Opportunity {oppNumber}", CurrentTenant.Id));

        return ObjectMapper.Map<Opportunity, OpportunityDto>(opportunity);
    }

    /// <summary>
    /// Convert a Lead to a Customer.
    /// Per ERPNext: if lead has CompanyName → Customer type is Company;
    /// otherwise → type is Individual with contact_person = lead full name.
    /// Also carries over email, phone, address, industry.
    /// </summary>
    [Authorize(MyERPPermissions.Leads.Convert)]
    public async Task<Guid> ConvertToCustomerAsync(ConvertLeadToCustomerDto input)
    {
        var lead = await _leadRepository.GetAsync(input.LeadId);

        // Determine customer name: override, or CompanyName, or FullName
        var customerName = !string.IsNullOrWhiteSpace(input.CustomerName)
            ? input.CustomerName
            : !string.IsNullOrWhiteSpace(lead.CompanyName)
                ? lead.CompanyName
                : lead.GetFullName();

        var customer = new Customer(GuidGenerator.Create(), lead.CompanyId, customerName, CurrentTenant.Id)
        {
            ContactPerson = lead.GetFullName(),
            Email = lead.Email,
            Phone = lead.Phone ?? lead.MobileNo,
            Website = lead.Website,
            City = lead.City,
            State = lead.State,
            Country = lead.Country,
            Tin = input.Tin,
            CustomerGroupId = input.CustomerGroupId,
            TerritoryId = input.TerritoryId,
        };

        await _customerRepository.InsertAsync(customer);

        // Per ERPNext lead.py: qualify/convert also creates a Contact from the lead's own
        // name/email/phone, distinct from the Customer's flat ContactPerson/Email/Phone fields.
        var contactFromLead = MyERP.CRM.DomainServices.LeadManager.BuildContactFromLead(lead);
        if (contactFromLead != null)
        {
            var contactRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.Contact, Guid>>();
            await contactRepo.InsertAsync(new Core.Entities.Contact(
                GuidGenerator.Create(), contactFromLead.FirstName, "Customer", customer.Id, CurrentTenant.Id)
            {
                LastName = contactFromLead.LastName,
                Email = contactFromLead.Email,
                Phone = contactFromLead.Phone,
                MobileNo = contactFromLead.MobileNo,
                IsPrimaryContact = true,
            });
        }

        // Mark lead as converted
        lead.ConvertToCustomer(customer.Id);
        await _leadRepository.UpdateAsync(lead);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Lead", lead.Id,
            "ConvertedToCustomer", lead.CompanyId,
            lead.LeadNumber, lead.Status.ToString(), "Converted", CurrentUser.Id,
            $"Lead {lead.LeadNumber} ({lead.GetFullName()}) converted to Customer {customerName}", CurrentTenant.Id));

        return customer.Id;
    }

    public async Task<List<CrmNoteDto>> GetNotesAsync(Guid id)
    {
        var query = await _noteRepository.GetQueryableAsync();
        var notes = query.Where(n => n.ParentType == "Lead" && n.ParentId == id)
            .OrderByDescending(n => n.AddedOn).ToList();
        return notes.Select(MapNoteToDto).ToList();
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<CrmNoteDto> AddNoteAsync(Guid id, AddCrmNoteDto input)
    {
        var lead = await _leadRepository.GetAsync(id);
        var note = lead.AddNote(GuidGenerator.Create(), input.NoteText, CurrentUser.Id ?? Guid.Empty);
        await _noteRepository.InsertAsync(note);
        return MapNoteToDto(note);
    }

    private static CrmNoteDto MapNoteToDto(CrmNote note) => new()
    {
        Id = note.Id,
        ParentType = note.ParentType,
        ParentId = note.ParentId,
        NoteText = note.NoteText,
        AddedByUserId = note.AddedByUserId,
        AddedOn = note.AddedOn,
    };

    private static IQueryable<Lead> ApplySorting(IQueryable<Lead> query, string sorting)
    {
        return sorting.ToLower() switch
        {
            "firstname asc" => query.OrderBy(l => l.FirstName),
            "firstname desc" => query.OrderByDescending(l => l.FirstName),
            "creationtime asc" => query.OrderBy(l => l.CreationTime),
            "creationtime desc" => query.OrderByDescending(l => l.CreationTime),
            "status asc" => query.OrderBy(l => l.Status),
            "status desc" => query.OrderByDescending(l => l.Status),
            _ => query.OrderByDescending(l => l.CreationTime),
        };
    }
}

