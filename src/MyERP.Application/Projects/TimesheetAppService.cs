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
public class TimesheetAppService : ApplicationService, ITimesheetAppService
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
        if (input.EndDate < input.StartDate)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        if (input.Details == null || input.Details.Count == 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        if (input.Details.Any(d => d.ToTime < d.FromTime))
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        if (input.Details.Any(d => d.Hours <= 0))
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Hours");
        }

        // Validate internal time log overlap (gotcha #2801)
        for (int i = 0; i < input.Details.Count; i++)
        {
            for (int j = i + 1; j < input.Details.Count; j++)
            {
                var d1 = input.Details[i];
                var d2 = input.Details[j];
                if (d1.FromTime < d2.ToTime && d1.ToTime > d2.FromTime)
                {
                    throw new Volo.Abp.BusinessException("MyERP:15002")
                        .WithData("reason", $"Overlapping time logs between {d1.ActivityType} ({d1.FromTime:HH:mm}-{d1.ToTime:HH:mm}) and {d2.ActivityType} ({d2.FromTime:HH:mm}-{d2.ToTime:HH:mm})");
                }
            }
        }

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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Timesheet", ts.Id,
            "Submitted", ts.CompanyId,
            ts.EmployeeName ?? ts.Id.ToString(), "Draft", "Submitted", CurrentUser.Id,
            $"Timesheet for {ts.EmployeeName} ({ts.StartDate:yyyy-MM-dd} to {ts.EndDate:yyyy-MM-dd}) submitted", CurrentTenant.Id));

        return ObjectMapper.Map<Timesheet, TimesheetDto>(ts);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<TimesheetDto> CancelAsync(Guid id)
    {
        var ts = await _repository.GetAsync(id, includeDetails: true);
        ts.Cancel();
        await _repository.UpdateAsync(ts);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Timesheet", ts.Id,
            "Cancelled", ts.CompanyId,
            ts.EmployeeName ?? ts.Id.ToString(), "Submitted", "Cancelled", CurrentUser.Id,
            $"Timesheet for {ts.EmployeeName} ({ts.StartDate:yyyy-MM-dd} to {ts.EndDate:yyyy-MM-dd}) cancelled", CurrentTenant.Id));

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

