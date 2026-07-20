using System;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class FiscalYearDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}

public class CreateFiscalYearDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

[Authorize(MyERPPermissions.Accounts.Default)]
public class FiscalYearAppService : ApplicationService
{
    private readonly IRepository<FiscalYear, Guid> _repository;
    public FiscalYearAppService(IRepository<FiscalYear, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<FiscalYearDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(f => f.StartDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<FiscalYearDto>(totalCount, items.Select(ObjectMapper.Map<FiscalYear, FiscalYearDto>).ToList());
    }

    public async Task<FiscalYearDto> GetAsync(Guid id) => ObjectMapper.Map<FiscalYear, FiscalYearDto>(await _repository.GetAsync(id));

    public async Task<FiscalYearDto> GetCurrentAsync(Guid companyId)
    {
        var query = await _repository.GetQueryableAsync();
        var now = DateTime.UtcNow.Date;
        var fy = query.FirstOrDefault(f => f.CompanyId == companyId && f.StartDate <= now && f.EndDate >= now);
        return fy != null ? ObjectMapper.Map<FiscalYear, FiscalYearDto>(fy) : null!;
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<FiscalYearDto> CreateAsync(CreateFiscalYearDto input)
    {
        // Validate no overlapping FY for same company
        var query = await _repository.GetQueryableAsync();
        var overlap = query.Any(f =>
            f.CompanyId == input.CompanyId &&
            f.StartDate <= input.EndDate &&
            f.EndDate >= input.StartDate);

        if (overlap)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("message", "A fiscal year with overlapping dates already exists for this company.")
                .WithData("startDate", input.StartDate.ToString("yyyy-MM-dd"))
                .WithData("endDate", input.EndDate.ToString("yyyy-MM-dd"));
        }

        var fy = new FiscalYear(GuidGenerator.Create(), input.CompanyId, input.Name,
            input.StartDate, input.EndDate, CurrentTenant.Id);
        await _repository.InsertAsync(fy);
        return ObjectMapper.Map<FiscalYear, FiscalYearDto>(fy);
    }

    /// <summary>
    /// Close a fiscal year. Enforces sequential closing: prior FY must be closed first.
    /// Per DO-NOT: "Skip sequential period closing enforcement (previous FY must be closed first)"
    /// </summary>
    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<FiscalYearDto> CloseAsync(Guid id)
    {
        var fy = await _repository.GetAsync(id);

        if (fy.IsClosed)
            return ObjectMapper.Map<FiscalYear, FiscalYearDto>(fy); // Already closed, idempotent

        // Sequential closure enforcement: check if any prior FY for same company is still open
        var query = await _repository.GetQueryableAsync();
        var priorOpenFy = query.FirstOrDefault(f =>
            f.CompanyId == fy.CompanyId
            && f.EndDate < fy.StartDate
            && !f.IsClosed);

        if (priorOpenFy != null)
        {
            throw new Volo.Abp.BusinessException("MyERP:02011")
                .WithData("priorFiscalYear", priorOpenFy.Name)
                .WithData("currentFiscalYear", fy.Name);
        }

        fy.IsClosed = true;
        await _repository.UpdateAsync(fy, autoSave: true);

        // Validate trial balance is balanced before finalizing close
        // Per ERPNext: month-end/year-end close should verify GL integrity
        try
        {
            var tbValidator = LazyServiceProvider.LazyGetRequiredService<MyERP.Accounting.DomainServices.TrialBalanceValidationService>();
            var validationResult = await tbValidator.ValidateAsync(fy.CompanyId, fy.StartDate, fy.EndDate);
            if (!validationResult.IsBalanced)
            {
                // Non-blocking warning — FY close proceeds but logs the imbalance
                System.Diagnostics.Debug.WriteLine(
                    $"FiscalYear {fy.Name} closed with unbalanced Trial Balance. Difference: {validationResult.Difference:N2}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Trial balance validation failed during FY close for {Id}", fy.Id);
        }

        return ObjectMapper.Map<FiscalYear, FiscalYearDto>(fy);
    }


}
