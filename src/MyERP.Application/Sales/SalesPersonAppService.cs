using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesPersons.Default)]
public class SalesPersonAppService : ApplicationService, ISalesPersonAppService
{
    private readonly IRepository<SalesPerson, Guid> _repository;

    public SalesPersonAppService(IRepository<SalesPerson, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<SalesPersonDto> GetAsync(Guid id)
    {
        var sp = await _repository.GetAsync(id);
        return ObjectMapper.Map<SalesPerson, SalesPersonDto>(sp);
    }

    public async Task<PagedResultDto<SalesPersonDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var count = query.Count();
        var list = query.OrderBy(x => x.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<SalesPersonDto>(count, list.Select(x => ObjectMapper.Map<SalesPerson, SalesPersonDto>(x)).ToList());
    }

    /// <summary>
    /// Get sales persons hierarchy (tree structure).
    /// </summary>
    public async Task<List<SalesPersonDto>> GetTreeAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var all = query.OrderBy(x => x.Name).ToList();
        return all.Select(x => ObjectMapper.Map<SalesPerson, SalesPersonDto>(x)).ToList();
    }

    /// <summary>
    /// Gets daily activity timeline data for a sales person over the past year.
    /// Per ERPNext PR #61952 (commit 1674287d0b): counts only SUBMITTED documents
    /// (Sales Order, Delivery Note, Sales Invoice) where this sales person is linked in sales team.
    /// </summary>
    public async Task<Dictionary<string, int>> GetTimelineDataAsync(Guid id)
    {
        var oneYearAgo = DateTime.UtcNow.Date.AddYears(-1);
        var result = new Dictionary<string, int>();

        var teamRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesTeamEntry, Guid>>();
        var teamQuery = await teamRepo.GetQueryableAsync();
        var teamEntries = teamQuery
            .Where(t => t.SalesPersonId == id)
            .Select(t => new { t.ParentType, t.ParentId })
            .ToList();

        if (teamEntries.Count == 0)
        {
            return result;
        }

        // 1. Sales Invoices
        var siIds = teamEntries.Where(t => t.ParentType == "SalesInvoice").Select(t => t.ParentId).Distinct().ToList();
        if (siIds.Count > 0)
        {
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
            var siQuery = await siRepo.GetQueryableAsync();
            var siDates = siQuery
                .Where(si => siIds.Contains(si.Id) && si.Status == Core.DocumentStatus.Submitted && si.IssueDate >= oneYearAgo)
                .Select(si => si.IssueDate.Date)
                .ToList();

            foreach (var d in siDates)
            {
                var key = d.ToString("yyyy-MM-dd");
                result[key] = result.GetValueOrDefault(key, 0) + 1;
            }
        }

        // 2. Sales Orders
        var soIds = teamEntries.Where(t => t.ParentType == "SalesOrder").Select(t => t.ParentId).Distinct().ToList();
        if (soIds.Count > 0)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
            var soQuery = await soRepo.GetQueryableAsync();
            var soDates = soQuery
                .Where(so => soIds.Contains(so.Id) && so.Status == Core.DocumentStatus.Submitted && so.OrderDate >= oneYearAgo)
                .Select(so => so.OrderDate.Date)
                .ToList();

            foreach (var d in soDates)
            {
                var key = d.ToString("yyyy-MM-dd");
                result[key] = result.GetValueOrDefault(key, 0) + 1;
            }
        }

        // 3. Delivery Notes
        var dnIds = teamEntries.Where(t => t.ParentType == "DeliveryNote").Select(t => t.ParentId).Distinct().ToList();
        if (dnIds.Count > 0)
        {
            var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
            var dnQuery = await dnRepo.GetQueryableAsync();
            var dnDates = dnQuery
                .Where(dn => dnIds.Contains(dn.Id) && dn.Status == Core.DocumentStatus.Submitted && dn.PostingDate >= oneYearAgo)
                .Select(dn => dn.PostingDate.Date)
                .ToList();

            foreach (var d in dnDates)
            {
                var key = d.ToString("yyyy-MM-dd");
                result[key] = result.GetValueOrDefault(key, 0) + 1;
            }
        }

        return result;
    }

    [Authorize(MyERPPermissions.SalesPersons.Create)]
    public async Task<SalesPersonDto> CreateAsync(CreateSalesPersonDto input)
    {
        if (input.CommissionRate < 0 || input.CommissionRate > 100)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
        }

        var newId = GuidGenerator.Create();
        var parentId = await ResolveDefaultRootParentAsync(input.ParentSalesPersonId, newId, input.Name);

        var sp = new SalesPerson(
            newId,
            input.Name,
            parentId,
            CurrentTenant.Id);

        sp.IsGroup = input.IsGroup;
        sp.EmployeeId = input.EmployeeId;
        sp.SetCommissionRate(input.CommissionRate);

        await _repository.InsertAsync(sp);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesPerson", sp.Id,
            "Created", Guid.Empty,
            sp.Name, "Draft", "Active",
            CurrentUser.Id,
            $"Sales person '{sp.Name}' created with commission rate {sp.CommissionRate}%", CurrentTenant.Id));

        return ObjectMapper.Map<SalesPerson, SalesPersonDto>(sp);
    }

    [Authorize(MyERPPermissions.SalesPersons.Edit)]
    public async Task<SalesPersonDto> UpdateAsync(Guid id, UpdateSalesPersonDto input)
    {
        if (input.CommissionRate < 0 || input.CommissionRate > 100)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
        }

        if (input.ParentSalesPersonId == id)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "A sales person cannot be their own parent.");
        }

        var sp = await _repository.GetAsync(id);
        sp.SetCommissionRate(input.CommissionRate);
        sp.IsGroup = input.IsGroup;
        sp.EmployeeId = input.EmployeeId;
        sp.ParentSalesPersonId = await ResolveDefaultRootParentAsync(input.ParentSalesPersonId, id, sp.Name);
        await _repository.UpdateAsync(sp);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesPerson", sp.Id,
            "Updated", Guid.Empty,
            sp.Name, "Active", "Active",
            CurrentUser.Id,
            $"Sales person '{sp.Name}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<SalesPerson, SalesPersonDto>(sp);
    }

    /// <summary>
    /// Defaults an empty tree parent to the root Sales Person group unless the record itself is the root
    /// (ERPNext commit 0e78d62389: set_root_as_default_parent).
    /// </summary>
    private async Task<Guid?> ResolveDefaultRootParentAsync(Guid? parentId, Guid currentId, string currentName)
    {
        if (parentId.HasValue && parentId.Value != Guid.Empty)
        {
            return parentId.Value == currentId ? null : parentId;
        }

        var query = await _repository.GetQueryableAsync();
        var root = query
            .Where(x => x.ParentSalesPersonId == null && x.IsGroup)
            .OrderBy(x => x.CreationTime)
            .FirstOrDefault();

        if (root != null && root.Id != currentId && !string.Equals(root.Name, currentName, StringComparison.OrdinalIgnoreCase))
        {
            return root.Id;
        }

        return null;
    }

    /// <summary>
    /// Add a sales target for a fiscal year.
    /// </summary>
    [Authorize(MyERPPermissions.SalesPersons.Edit)]
    public async Task AddTargetAsync(Guid id, CreateSalesTargetDto input)
    {
        var sp = await _repository.GetAsync(id);
        sp.AddTarget(input.FiscalYearId, input.TargetQty, input.TargetAmount, input.ItemGroupId);
        await _repository.UpdateAsync(sp);
    }

    /// <summary>
    /// Disable a sales person (cannot be assigned to new transactions).
    /// </summary>
    [Authorize(MyERPPermissions.SalesPersons.Edit)]
    public async Task DisableAsync(Guid id)
    {
        var sp = await _repository.GetAsync(id);

        // Per ERPNext PR #61835 (commit 1fe56c013a): reject disabling sales person if linked to unfinished Sales Orders or unbilled Delivery Notes
        var teamRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesTeamEntry, Guid>>();
        var teamQuery = await teamRepo.GetQueryableAsync();
        var linkedTeam = teamQuery.Where(t => t.SalesPersonId == id).ToList();

        if (linkedTeam.Count > 0)
        {
            var soIds = linkedTeam.Where(t => t.ParentType == "SalesOrder").Select(t => t.ParentId).Distinct().ToList();
            if (soIds.Count > 0)
            {
                var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
                var soQuery = await soRepo.GetQueryableAsync();
                var unfinishedSo = soQuery.FirstOrDefault(so =>
                    soIds.Contains(so.Id)
                    && so.Status != Core.DocumentStatus.Draft
                    && so.Status != Core.DocumentStatus.Cancelled
                    && so.Status != Core.DocumentStatus.Completed
                    && so.Status != Core.DocumentStatus.Closed);

                if (unfinishedSo != null)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Sales Person '{sp.Name}' cannot be disabled because Sales Order '{unfinishedSo.OrderNumber}' is unfinished.");
                }
            }

            var dnIds = linkedTeam.Where(t => t.ParentType == "DeliveryNote").Select(t => t.ParentId).Distinct().ToList();
            if (dnIds.Count > 0)
            {
                var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
                var dnQuery = await dnRepo.WithDetailsAsync(d => d.Items);
                var dnList = dnQuery.Where(dn =>
                    dnIds.Contains(dn.Id)
                    && dn.Status != Core.DocumentStatus.Draft
                    && dn.Status != Core.DocumentStatus.Cancelled
                    && dn.Status != Core.DocumentStatus.Closed).ToList();

                // Per ERPNext commit 9fa7b05e44: a returned or fully returned Delivery Note keeps PerBilled < 100,
                // so check BillingStatus in ("To Bill", "Partially Billed") rather than PerBilled < 100.
                var unfinishedDn = dnList.FirstOrDefault(dn =>
                    dn.BillingStatus == "To Bill" || dn.BillingStatus == "Partially Billed");
                if (unfinishedDn != null)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Sales Person '{sp.Name}' cannot be disabled because Delivery Note '{unfinishedDn.DeliveryNumber}' is unfinished.");
                }
            }
        }

        sp.Disable();
        await _repository.UpdateAsync(sp);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SalesPerson", sp.Id,
            "Disabled", Guid.Empty,
            sp.Name, "Active", "Disabled",
            CurrentUser.Id,
            $"Sales person '{sp.Name}' disabled", CurrentTenant.Id));
    }

    [Authorize(MyERPPermissions.SalesPersons.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
