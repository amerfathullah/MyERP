using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Manages Authorization Rules — configurable approval thresholds for high-value transactions.
/// Per ERPNext: 3-tier evaluation (user → role → global), company-specific overrides,
/// self-approval blocked, discount rules capped at 100%.
/// </summary>
[Authorize(MyERPPermissions.ApprovalWorkflows.Default)]
public class AuthorizationRuleAppService : ApplicationService
{
    private readonly IRepository<AuthorizationRule, Guid> _repository;

    public AuthorizationRuleAppService(IRepository<AuthorizationRule, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<AuthorizationRuleDto> GetAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        return MapToDto(rule);
    }

    public async Task<PagedResultDto<AuthorizationRuleDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(r => r.CompanyId == input.CompanyId.Value || r.CompanyId == null);

        var count = query.Count();
        var list = query.OrderBy(r => r.TransactionType).ThenBy(r => r.ThresholdValue)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<AuthorizationRuleDto>(count, list.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.ApprovalWorkflows.Create)]
    public async Task<AuthorizationRuleDto> CreateAsync(CreateAuthorizationRuleDto input)
    {
        var rule = new AuthorizationRule(
            GuidGenerator.Create(),
            input.TransactionType,
            input.BasedOn,
            input.ThresholdValue,
            input.CompanyId,
            CurrentTenant.Id);

        rule.SystemUserId = input.SystemUserId;
        rule.SystemRole = input.SystemRole;
        rule.ApprovingRole = input.ApprovingRole;
        rule.ApprovingUserId = input.ApprovingUserId;
        rule.CustomerId = input.CustomerId;

        rule.Validate();
        await _repository.InsertAsync(rule);
        return MapToDto(rule);
    }

    [Authorize(MyERPPermissions.ApprovalWorkflows.Edit)]
    public async Task<AuthorizationRuleDto> UpdateAsync(Guid id, UpdateAuthorizationRuleDto input)
    {
        var rule = await _repository.GetAsync(id);
        rule.ThresholdValue = input.ThresholdValue;
        rule.ApprovingRole = input.ApprovingRole;
        rule.ApprovingUserId = input.ApprovingUserId;
        rule.SystemUserId = input.SystemUserId;
        rule.SystemRole = input.SystemRole;
        rule.CustomerId = input.CustomerId;
        rule.Validate();
        await _repository.UpdateAsync(rule);
        return MapToDto(rule);
    }

    [Authorize(MyERPPermissions.ApprovalWorkflows.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    private static AuthorizationRuleDto MapToDto(AuthorizationRule r) => new()
    {
        Id = r.Id,
        CompanyId = r.CompanyId,
        TransactionType = r.TransactionType,
        BasedOn = r.BasedOn.ToString(),
        ThresholdValue = r.ThresholdValue,
        SystemUserId = r.SystemUserId,
        SystemRole = r.SystemRole,
        ApprovingRole = r.ApprovingRole,
        ApprovingUserId = r.ApprovingUserId,
        CustomerId = r.CustomerId
    };
}

#region DTOs

public class AuthorizationRuleDto
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string TransactionType { get; set; } = null!;
    public string BasedOn { get; set; } = null!;
    public decimal ThresholdValue { get; set; }
    public Guid? SystemUserId { get; set; }
    public string? SystemRole { get; set; }
    public string? ApprovingRole { get; set; }
    public Guid? ApprovingUserId { get; set; }
    public Guid? CustomerId { get; set; }
}

public class CreateAuthorizationRuleDto
{
    public Guid? CompanyId { get; set; }
    public string TransactionType { get; set; } = null!;
    public AuthorizationBasedOn BasedOn { get; set; }
    public decimal ThresholdValue { get; set; }
    public Guid? SystemUserId { get; set; }
    public string? SystemRole { get; set; }
    public string? ApprovingRole { get; set; }
    public Guid? ApprovingUserId { get; set; }
    public Guid? CustomerId { get; set; }
}

public class UpdateAuthorizationRuleDto
{
    public decimal ThresholdValue { get; set; }
    public Guid? SystemUserId { get; set; }
    public string? SystemRole { get; set; }
    public string? ApprovingRole { get; set; }
    public Guid? ApprovingUserId { get; set; }
    public Guid? CustomerId { get; set; }
}

#endregion
