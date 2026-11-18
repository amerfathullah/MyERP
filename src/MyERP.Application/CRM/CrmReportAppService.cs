using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Support.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace MyERP.CRM;

/// <summary>
/// CRM Analytics and Lead Conversion Reports migrating ERPNext crm/report/*:
/// - Lead Details
/// - Lead Conversion Time
/// - Prospects Engaged But Not Converted
/// - Sales Pipeline Analytics
/// </summary>
[Authorize(MyERPPermissions.Leads.Default)]
public class CrmReportAppService : ApplicationService, ICrmReportAppService
{
    private readonly IRepository<Lead, Guid> _leadRepository;
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;
    private readonly IRepository<CrmNote, Guid> _crmNoteRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<Issue, Guid>? _issueRepository;
    private readonly IRepository<IdentityUser, Guid>? _userRepository;

    public CrmReportAppService(
        IRepository<Lead, Guid> leadRepository,
        IRepository<Opportunity, Guid> opportunityRepository,
        IRepository<CrmNote, Guid> crmNoteRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<Issue, Guid>? issueRepository = null,
        IRepository<IdentityUser, Guid>? userRepository = null)
    {
        _leadRepository = leadRepository;
        _opportunityRepository = opportunityRepository;
        _crmNoteRepository = crmNoteRepository;
        _customerRepository = customerRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _issueRepository = issueRepository;
        _userRepository = userRepository;
    }

    public async Task<LeadDetailsReportDto> GetLeadDetailsAsync(LeadDetailsFilterDto input)
    {
        var (from, to) = NormalizeAndValidateDateRange(input.FromDate, input.ToDate);
        var toExclusive = to.AddDays(1);

        var leadQuery = await _leadRepository.GetQueryableAsync();
        var filtered = leadQuery.Where(l =>
            l.CompanyId == input.CompanyId &&
            l.CreationTime >= from &&
            l.CreationTime < toExclusive);

        if (input.Status.HasValue)
            filtered = filtered.Where(l => l.Status == input.Status.Value);
        if (input.Source.HasValue)
            filtered = filtered.Where(l => l.Source == input.Source.Value);
        if (!string.IsNullOrWhiteSpace(input.Country))
            filtered = filtered.Where(l => l.Country == input.Country);
        if (input.AssignedUserId.HasValue)
            filtered = filtered.Where(l => l.AssignedUserId == input.AssignedUserId.Value);

        var leads = filtered.OrderByDescending(l => l.CreationTime).ToList();
        var ownerMap = await LoadUserNamesAsync(leads.Where(l => l.AssignedUserId.HasValue).Select(l => l.AssignedUserId!.Value));

        var rows = leads.Select(l => new LeadDetailsRowDto
        {
            LeadId = l.Id,
            LeadNumber = l.LeadNumber,
            LeadName = l.GetFullName(),
            JobTitle = l.JobTitle,
            Status = l.Status.ToString(),
            Source = l.Source.ToString(),
            UtmSource = l.UtmSource,
            Email = l.Email,
            MobileNo = l.MobileNo,
            Phone = l.Phone,
            LeadOwner = l.AssignedUserId.HasValue
                ? ownerMap.GetValueOrDefault(l.AssignedUserId.Value, l.AssignedUserId.Value.ToString())
                : null,
            OrganizationName = l.CompanyName,
            City = l.City,
            State = l.State,
            Country = l.Country,
            Industry = l.Industry,
            CreationTime = l.CreationTime,
        }).ToList();

        return new LeadDetailsReportDto
        {
            Rows = rows,
            TotalCount = rows.Count,
        };
    }

    public async Task<LeadConversionTimeReportDto> GetLeadConversionTimeAsync(CrmReportDateFilterDto input)
    {
        var (from, to) = NormalizeAndValidateDateRange(input.FromDate, input.ToDate);
        var toExclusive = to.AddDays(1);

        var leadQuery = await _leadRepository.GetQueryableAsync();
        var leads = leadQuery.Where(l => l.CompanyId == input.CompanyId).ToList();
        var custQuery = await _customerRepository.GetQueryableAsync();
        var customers = custQuery.Where(c => c.CompanyId == input.CompanyId).ToList();

        // Map converted customer -> lead (either Lead.ConvertedCustomerId or Customer.LeadId)
        var leadById = leads.ToDictionary(l => l.Id);
        var convertedPairs = new List<(Lead Lead, Customer Customer)>();
        var seenLeadIds = new HashSet<Guid>();

        foreach (var cust in customers)
        {
            if (cust.LeadId.HasValue && leadById.TryGetValue(cust.LeadId.Value, out var leadByCust) && seenLeadIds.Add(leadByCust.Id))
            {
                convertedPairs.Add((leadByCust, cust));
            }
        }
        foreach (var lead in leads.Where(l => l.ConvertedCustomerId.HasValue && !seenLeadIds.Contains(l.Id)))
        {
            var cust = customers.FirstOrDefault(c => c.Id == lead.ConvertedCustomerId!.Value);
            if (cust != null && seenLeadIds.Add(lead.Id))
            {
                convertedPairs.Add((lead, cust));
            }
        }

        if (convertedPairs.Count == 0)
        {
            return new LeadConversionTimeReportDto();
        }

        var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var postedInvoices = siQuery
            .Where(si => si.CompanyId == input.CompanyId && si.Status == DocumentStatus.Posted)
            .ToList();
        var firstInvoiceByCustomer = postedInvoices
            .GroupBy(si => si.CustomerId)
            .ToDictionary(g => g.Key, g => g.Min(si => si.IssueDate));

        var oppQuery = await _opportunityRepository.GetQueryableAsync();
        var oppsByLead = oppQuery
            .Where(o => o.CompanyId == input.CompanyId && o.LeadId.HasValue)
            .ToList()
            .GroupBy(o => o.LeadId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(o => o.Id).ToHashSet());

        var noteQuery = await _crmNoteRepository.GetQueryableAsync();
        var allNotes = noteQuery.Where(n => n.ParentType == "Lead" || n.ParentType == "Opportunity").ToList();

        var issueRepo = _issueRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Issue, Guid>>();
        Dictionary<Guid, int> issueCountByCustomer = new();
        if (issueRepo != null)
        {
            var issueQuery = await issueRepo.GetQueryableAsync();
            issueCountByCustomer = issueQuery
                .Where(i => i.CompanyId == input.CompanyId && i.CustomerId.HasValue)
                .ToList()
                .GroupBy(i => i.CustomerId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var rows = new List<LeadConversionTimeRowDto>();
        foreach (var (lead, cust) in convertedPairs)
        {
            var convertedDate = firstInvoiceByCustomer.TryGetValue(cust.Id, out var firstInvDate)
                ? firstInvDate
                : cust.CreationTime;

            if (convertedDate < from || convertedDate >= toExclusive)
                continue;

            var oppIds = oppsByLead.GetValueOrDefault(lead.Id, new HashSet<Guid>());
            var interactionCount = allNotes.Count(n =>
                (n.ParentType == "Lead" && n.ParentId == lead.Id) ||
                (n.ParentType == "Opportunity" && oppIds.Contains(n.ParentId)));

            var durationDays = Math.Max(0m, Math.Round((decimal)(convertedDate.Date - lead.CreationTime.Date).TotalDays, 2));
            var ticketCount = issueCountByCustomer.GetValueOrDefault(cust.Id, 0);

            rows.Add(new LeadConversionTimeRowDto
            {
                LeadId = lead.Id,
                LeadNumber = lead.LeadNumber,
                LeadName = lead.GetFullName(),
                CustomerId = cust.Id,
                CustomerName = cust.Name,
                Interactions = interactionCount,
                DurationInDays = durationDays,
                SupportTickets = ticketCount,
                LeadCreatedOn = lead.CreationTime,
                ConvertedOn = convertedDate,
            });
        }

        rows = rows.OrderByDescending(r => r.ConvertedOn).ToList();

        return new LeadConversionTimeReportDto
        {
            Rows = rows,
            TotalConverted = rows.Count,
            AvgInteractions = rows.Count > 0 ? Math.Round(rows.Average(r => (decimal)r.Interactions), 2) : 0m,
            AvgDurationInDays = rows.Count > 0 ? Math.Round(rows.Average(r => r.DurationInDays), 2) : 0m,
            AvgSupportTickets = rows.Count > 0 ? Math.Round(rows.Average(r => (decimal)r.SupportTickets), 2) : 0m,
        };
    }

    public async Task<ProspectsEngagedReportDto> GetProspectsEngagedButNotConvertedAsync(ProspectsEngagedFilterDto input)
    {
        var leadAgeDays = input.LeadAgeDays > 0 ? input.LeadAgeDays : 60;
        var minInteractions = input.MinInteractions > 0 ? input.MinInteractions : 1;
        var cutoffDate = DateTime.UtcNow.AddDays(-leadAgeDays);

        var leadQuery = await _leadRepository.GetQueryableAsync();
        var candidateLeads = leadQuery.Where(l =>
            l.CompanyId == input.CompanyId &&
            l.Status != LeadStatus.Converted &&
            !l.ConvertedCustomerId.HasValue &&
            l.CreationTime >= cutoffDate);

        if (input.LeadId.HasValue)
            candidateLeads = candidateLeads.Where(l => l.Id == input.LeadId.Value);

        var leads = candidateLeads.ToList();
        if (leads.Count == 0)
        {
            return new ProspectsEngagedReportDto();
        }

        var leadIds = leads.Select(l => l.Id).ToHashSet();
        var oppQuery = await _opportunityRepository.GetQueryableAsync();
        var opps = oppQuery
            .Where(o => o.CompanyId == input.CompanyId && o.LeadId.HasValue && leadIds.Contains(o.LeadId.Value))
            .ToList();
        var oppById = opps.ToDictionary(o => o.Id);
        var oppsByLead = opps.GroupBy(o => o.LeadId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var noteQuery = await _crmNoteRepository.GetQueryableAsync();
        var notes = noteQuery.Where(n => n.ParentType == "Lead" || n.ParentType == "Opportunity").ToList();

        var rows = new List<ProspectsEngagedRowDto>();
        foreach (var lead in leads)
        {
            var leadOpps = oppsByLead.GetValueOrDefault(lead.Id, new List<Opportunity>());
            var oppIdSet = leadOpps.Select(o => o.Id).ToHashSet();

            var matchingNotes = notes
                .Where(n =>
                    (n.ParentType == "Lead" && n.ParentId == lead.Id) ||
                    (n.ParentType == "Opportunity" && oppIdSet.Contains(n.ParentId)))
                .OrderByDescending(n => n.AddedOn)
                .ToList();

            if (matchingNotes.Count < minInteractions)
                continue;

            var latest = matchingNotes[0];
            string refName = lead.LeadNumber;
            if (latest.ParentType == "Opportunity" && oppById.TryGetValue(latest.ParentId, out var opp))
            {
                refName = opp.OpportunityNumber;
            }

            rows.Add(new ProspectsEngagedRowDto
            {
                LeadId = lead.Id,
                LeadNumber = lead.LeadNumber,
                LeadName = lead.GetFullName(),
                OrganizationName = lead.CompanyName,
                ReferenceType = latest.ParentType,
                ReferenceId = latest.ParentId,
                ReferenceName = refName,
                InteractionCount = matchingNotes.Count,
                LastCommunication = latest.NoteText,
                LastCommunicationDate = latest.AddedOn,
            });
        }

        rows = rows.OrderByDescending(r => r.LastCommunicationDate).ToList();

        return new ProspectsEngagedReportDto
        {
            Rows = rows,
            TotalCount = rows.Count,
        };
    }

    public async Task<SalesPipelineAnalyticsReportDto> GetSalesPipelineAnalyticsAsync(SalesPipelineAnalyticsFilterDto input)
    {
        var (from, to) = NormalizeAndValidateDateRange(input.FromDate, input.ToDate);
        var toExclusive = to.AddDays(1);
        var pipelineBy = string.Equals(input.PipelineBy, "Owner", StringComparison.OrdinalIgnoreCase)
            ? "Owner"
            : "Sales Stage";
        var basedOn = string.Equals(input.BasedOn, "Number", StringComparison.OrdinalIgnoreCase)
            ? "Number"
            : "Amount";
        var range = string.Equals(input.Range, "Quarterly", StringComparison.OrdinalIgnoreCase)
            ? "Quarterly"
            : "Monthly";

        var oppQuery = await _opportunityRepository.GetQueryableAsync();
        var filtered = oppQuery.Where(o =>
            o.CompanyId == input.CompanyId &&
            o.ExpectedClosingDate.HasValue &&
            o.ExpectedClosingDate.Value >= from &&
            o.ExpectedClosingDate.Value < toExclusive);

        if (input.OpportunityType.HasValue)
            filtered = filtered.Where(o => o.OpportunityType == input.OpportunityType.Value);
        if (input.Status.HasValue)
            filtered = filtered.Where(o => o.Status == input.Status.Value);

        var opps = filtered.ToList();
        var periods = BuildPipelinePeriods(from, to, range);
        var periodLabels = periods.Select(p => p.Label).ToArray();
        var ownerMap = pipelineBy == "Owner"
            ? await LoadUserNamesAsync(opps.Where(o => o.AssignedUserId.HasValue).Select(o => o.AssignedUserId!.Value))
            : new Dictionary<Guid, string>();

        var rows = new List<SalesPipelineAnalyticsRowDto>();
        var periodTotals = new decimal[periods.Count];

        foreach (var group in opps.GroupBy(o => ResolvePipelineGroup(o, pipelineBy, ownerMap)))
        {
            var values = new decimal[periods.Count];
            foreach (var opp in group)
            {
                var closingDate = opp.ExpectedClosingDate!.Value.Date;
                for (int idx = 0; idx < periods.Count; idx++)
                {
                    if (closingDate >= periods[idx].Start && closingDate <= periods[idx].End)
                    {
                        var val = basedOn == "Number"
                            ? 1m
                            : Math.Round(opp.OpportunityAmount * (opp.ConversionRate > 0 ? opp.ConversionRate : 1m), 2);
                        values[idx] += val;
                        periodTotals[idx] += val;
                        break;
                    }
                }
            }

            rows.Add(new SalesPipelineAnalyticsRowDto
            {
                GroupKey = group.Key.Key,
                GroupLabel = group.Key.Label,
                PeriodValues = values,
                Total = values.Sum(),
            });
        }

        rows = rows.OrderByDescending(r => r.Total).ThenBy(r => r.GroupLabel).ToList();

        return new SalesPipelineAnalyticsReportDto
        {
            PipelineBy = pipelineBy,
            BasedOn = basedOn,
            Range = range,
            PeriodLabels = periodLabels,
            Rows = rows,
            PeriodTotals = periodTotals,
            GrandTotal = periodTotals.Sum(),
        };
    }

    private async Task<Dictionary<Guid, string>> LoadUserNamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        var map = new Dictionary<Guid, string>();
        if (ids.Count == 0) return map;

        var userRepo = _userRepository ?? LazyServiceProvider?.LazyGetService<IRepository<IdentityUser, Guid>>();
        if (userRepo == null) return map;

        try
        {
            var q = await userRepo.GetQueryableAsync();
            foreach (var u in q.Where(x => ids.Contains(x.Id)).ToList())
            {
                map[u.Id] = string.IsNullOrWhiteSpace(u.Name) ? u.UserName : u.Name;
            }
        }
        catch
        {
            // Fallback when identity repository is unavailable in isolated unit tests
        }
        return map;
    }

    private static (string Key, string Label) ResolvePipelineGroup(
        Opportunity opp,
        string pipelineBy,
        Dictionary<Guid, string> ownerMap)
    {
        if (pipelineBy == "Owner")
        {
            return opp.AssignedUserId.HasValue
                ? (opp.AssignedUserId.Value.ToString(), ownerMap.GetValueOrDefault(opp.AssignedUserId.Value, opp.AssignedUserId.Value.ToString()))
                : ("Unassigned", "Unassigned");
        }

        var stage = !string.IsNullOrWhiteSpace(opp.SalesStage) ? opp.SalesStage : "Unclassified";
        return (stage, stage);
    }

    private static (DateTime From, DateTime To) NormalizeAndValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        var to = (toDate ?? DateTime.UtcNow).Date;
        var from = (fromDate ?? to.AddMonths(-3)).Date;
        if (from > to)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be after To Date.");
        }
        return (from, to);
    }

    private static List<(DateTime Start, DateTime End, string Label)> BuildPipelinePeriods(DateTime from, DateTime to, string range)
    {
        var periods = new List<(DateTime Start, DateTime End, string Label)>();
        bool multiYear = from.Year != to.Year;

        if (range == "Quarterly")
        {
            var cursor = new DateTime(from.Year, ((from.Month - 1) / 3) * 3 + 1, 1);
            while (cursor <= to)
            {
                var end = cursor.AddMonths(3).AddDays(-1);
                if (end > to) end = to;
                int q = ((cursor.Month - 1) / 3) + 1;
                var label = multiYear ? $"Quarter {q} {cursor.Year}" : $"Quarter {q}";
                periods.Add((cursor, end, label));
                cursor = end.AddDays(1);
            }
            return periods;
        }

        var mCursor = new DateTime(from.Year, from.Month, 1);
        while (mCursor <= to)
        {
            var end = mCursor.AddMonths(1).AddDays(-1);
            if (end > to) end = to;
            var monthAbbr = mCursor.ToString("MMM", CultureInfo.InvariantCulture);
            var label = multiYear ? $"{monthAbbr} {mCursor.Year}" : monthAbbr;
            periods.Add((mCursor, end, label));
            mCursor = end.AddDays(1);
        }
        return periods;
    }
}
