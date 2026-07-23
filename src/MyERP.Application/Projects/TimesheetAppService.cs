using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects;

[Authorize(MyERPPermissions.Projects.Default)]
public class TimesheetAppService : ApplicationService
{
    private readonly IRepository<Timesheet, Guid> _repository;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public TimesheetAppService(
        IRepository<Timesheet, Guid> repository,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _invoiceRepository = invoiceRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<TimesheetDto> GetAsync(Guid id)
    {
        var ts = await _repository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<Timesheet, TimesheetDto>(ts);
    }

    public async Task<PagedResultDto<TimesheetDto>> GetListAsync(GetTimesheetListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(t => t.CompanyId == input.CompanyId.Value);
        if (input.EmployeeId.HasValue)
            query = query.Where(t => t.EmployeeId == input.EmployeeId.Value);
        if (input.Status.HasValue)
            query = query.Where(t => t.Status == input.Status.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(t => t.EmployeeName != null && t.EmployeeName.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(t => t.StartDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<TimesheetDto>(totalCount, items.Select(x => ObjectMapper.Map<Timesheet, TimesheetDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.Projects.Create)]
    public async Task<TimesheetDto> CreateAsync(CreateTimesheetDto input)
    {
        var ts = new Timesheet(GuidGenerator.Create(), input.CompanyId, input.EmployeeId,
            input.StartDate, input.EndDate, CurrentTenant.Id)
        { EmployeeName = input.EmployeeName, Note = input.Note };

        foreach (var d in input.Details)
        {
            // Auto-resolve billing/costing rates if not explicitly provided
            // Per ERPNext: Employee-specific rate → Activity Type global rate → zero
            var billingRate = d.BillingRate;
            var costingRate = d.CostingRate;

            if ((billingRate == 0 || costingRate == 0) && !string.IsNullOrWhiteSpace(d.ActivityType))
            {
                try
                {
                    var activityCostSvc = LazyServiceProvider.LazyGetRequiredService<MyERP.Projects.DomainServices.ActivityCostResolutionService>();
                    var activityTypeRepo = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<MyERP.Projects.Entities.ActivityType, Guid>>();

                    // Resolve ActivityType Guid from name
                    var atQuery = await activityTypeRepo.GetQueryableAsync();
                    var activityType = atQuery.FirstOrDefault(at => at.Name == d.ActivityType && at.IsEnabled);

                    if (activityType != null)
                    {
                        var (resolvedBilling, resolvedCosting) = await activityCostSvc.ResolveRatesAsync(
                            input.EmployeeId, activityType.Id);
                        if (billingRate == 0 && resolvedBilling > 0) billingRate = resolvedBilling;
                        if (costingRate == 0 && resolvedCosting > 0) costingRate = resolvedCosting;
                    }
                }
                catch (Exception ex) { Logger.LogWarning(ex, "Activity rate resolution failed for {Activity}", d.ActivityType); }
            }

            var detail = new TimesheetDetail(GuidGenerator.Create(), ts.Id,
                d.ActivityType, d.FromTime, d.ToTime, d.Hours)
            {
                ProjectId = d.ProjectId,
                TaskId = d.TaskId,
                IsBillable = d.IsBillable,
                BillingRate = billingRate,
                CostingRate = costingRate,
                Description = d.Description,
            };
            ts.AddDetail(detail);
        }

        await _repository.InsertAsync(ts);
        return ObjectMapper.Map<Timesheet, TimesheetDto>(ts);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<TimesheetDto> SubmitAsync(Guid id)
    {
        var ts = await _repository.GetAsync(id, includeDetails: true);
        ts.Submit();
        await _repository.UpdateAsync(ts);
        return ObjectMapper.Map<Timesheet, TimesheetDto>(ts);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<TimesheetDto> CancelAsync(Guid id)
    {
        var ts = await _repository.GetAsync(id, includeDetails: true);
        ts.Cancel();
        await _repository.UpdateAsync(ts);
        return ObjectMapper.Map<Timesheet, TimesheetDto>(ts);
    }

    /// <summary>
    /// Creates a Sales Invoice from unbilled billable timesheet details.
    /// Per ERPNext: fetches all submitted timesheets with unbilled billable hours for the given customer/project.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<TimesheetBillingResultDto> CreateInvoiceFromTimesheetsAsync(CreateTimesheetInvoiceDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var timesheets = query
            .Where(t => t.CompanyId == input.CompanyId
                && t.Status == TimesheetStatus.Submitted)
            .ToList();

        // Gather unbilled billable details
        var unbilledDetails = timesheets
            .SelectMany(ts => ts.Details.Where(d =>
                d.IsBillable && d.SalesInvoiceId == null && d.BillingAmount > 0
                && (!input.ProjectId.HasValue || d.ProjectId == input.ProjectId)))
            .ToList();

        if (!unbilledDetails.Any())
            throw new Volo.Abp.BusinessException("MyERP:15001")
                .WithData("reason", "No unbilled timesheet entries found");

        var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", input.CompanyId);
        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            invoiceNumber,
            DateTime.UtcNow,
            CurrentTenant.Id);

        foreach (var detail in unbilledDetails)
        {
            invoice.AddItem(
                detail.Id, // use detail ID as item ID for traceability
                $"{detail.ActivityType} - {detail.Hours}h",
                detail.Hours,
                detail.BillingRate,
                0);
        }

        await _invoiceRepository.InsertAsync(invoice, autoSave: true);

        // Mark details as billed
        foreach (var detail in unbilledDetails)
        {
            detail.SalesInvoiceId = invoice.Id;
        }
        foreach (var ts in timesheets.Where(t => t.Details.Any(d => d.SalesInvoiceId == invoice.Id)))
        {
            await _repository.UpdateAsync(ts);
        }

        return new TimesheetBillingResultDto
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            TotalHours = unbilledDetails.Sum(d => d.Hours),
            TotalAmount = invoice.GrandTotal,
            DetailCount = unbilledDetails.Count,
        };
    }

    /// <summary>Returns unbilled billable hours summary for a company/project.</summary>
    public async Task<List<UnbilledTimesheetSummaryDto>> GetUnbilledSummaryAsync(Guid companyId, Guid? projectId)
    {
        var query = await _repository.GetQueryableAsync();
        var timesheets = query
            .Where(t => t.CompanyId == companyId && t.Status == TimesheetStatus.Submitted)
            .ToList();

        var unbilled = timesheets
            .SelectMany(ts => ts.Details.Where(d =>
                d.IsBillable && d.SalesInvoiceId == null && d.BillingAmount > 0
                && (!projectId.HasValue || d.ProjectId == projectId)))
            .GroupBy(d => d.ActivityType)
            .Select(g => new UnbilledTimesheetSummaryDto
            {
                ActivityType = g.Key,
                TotalHours = g.Sum(d => d.Hours),
                TotalAmount = g.Sum(d => d.BillingAmount),
                EntryCount = g.Count(),
            })
            .ToList();

        return unbilled;
    }
}

// DTOs

public class TimesheetDto : AuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public TimesheetStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalHours { get; set; }
    public decimal TotalBillableHours { get; set; }
    public decimal TotalBillingAmount { get; set; }
    public decimal TotalCostingAmount { get; set; }
    public string? Note { get; set; }
    public List<TimesheetDetailDto> Details { get; set; } = new();
}

public class TimesheetDetailDto
{
    public Guid Id { get; set; }
    public string ActivityType { get; set; } = null!;
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal Hours { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public bool IsBillable { get; set; }
    public decimal BillingRate { get; set; }
    public decimal BillingAmount { get; set; }
    public decimal CostingRate { get; set; }
    public decimal CostingAmount { get; set; }
    public string? Description { get; set; }
}

public class CreateTimesheetDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    [Required] public DateTime StartDate { get; set; }
    [Required] public DateTime EndDate { get; set; }
    public string? Note { get; set; }
    public List<CreateTimesheetDetailDto> Details { get; set; } = new();
}

public class CreateTimesheetDetailDto
{
    [Required] public string ActivityType { get; set; } = null!;
    [Required] public DateTime FromTime { get; set; }
    [Required] public DateTime ToTime { get; set; }
    [Required] public decimal Hours { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public bool IsBillable { get; set; }
    public decimal BillingRate { get; set; }
    public decimal CostingRate { get; set; }
    public string? Description { get; set; }
}

public class GetTimesheetListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? EmployeeId { get; set; }
    public TimesheetStatus? Status { get; set; }
    public string? Filter { get; set; }
}

public class CreateTimesheetInvoiceDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid CustomerId { get; set; }
    public Guid? ProjectId { get; set; }
}

public class TimesheetBillingResultDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal TotalHours { get; set; }
    public decimal TotalAmount { get; set; }
    public int DetailCount { get; set; }
}

public class UnbilledTimesheetSummaryDto
{
    public string ActivityType { get; set; } = null!;
    public decimal TotalHours { get; set; }
    public decimal TotalAmount { get; set; }
    public int EntryCount { get; set; }
}

