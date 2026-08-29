using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Dunning Type management — per-company collections-level configuration (fee, yearly
/// interest rate, posting accounts, language letter text). Only one type can be default
/// per company; setting a new default silently demotes the previous one (per ERPNext
/// set_default_dunning_type — no throw, unlike Finance Book's demote-manually rule).
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class DunningTypeAppService : ApplicationService, IDunningTypeAppService
{
    private readonly IRepository<DunningType, Guid> _repository;

    public DunningTypeAppService(IRepository<DunningType, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<DunningTypeDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();
        var items = query.OrderBy(x => x.DunningTypeName)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<DunningTypeDto>(totalCount, items.Select(ObjectMapper.Map<DunningType, DunningTypeDto>).ToList());
    }

    public async Task<DunningTypeDto> GetAsync(Guid id)
    {
        var t = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        return ObjectMapper.Map<DunningType, DunningTypeDto>(t);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<DunningTypeDto> CreateAsync(CreateDunningTypeDto input)
    {
        if (input.DunningFee < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "DunningFee");
        }
        if (input.RateOfInterest < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "RateOfInterest");
        }

        await ValidateDunningTypeAccountsAndCostCentersAsync(input.CompanyId, input.IncomeAccountId, input.CostCenterId);

        var t = new DunningType(GuidGenerator.Create(), input.CompanyId, input.DunningTypeName, CurrentTenant.Id);
        ApplyFields(t, input);
        await _repository.InsertAsync(t);

        if (t.IsDefault)
            await DemoteOtherDefaultsAsync(t.CompanyId, t.Id);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new DocumentActivityLog(GuidGenerator.Create(),
            "DunningType", t.Id, "Created", t.CompanyId,
            t.DunningTypeName, "Draft", "Active",
            CurrentUser.Id,
            $"Dunning type '{t.DunningTypeName}' created", t.TenantId));

        return ObjectMapper.Map<DunningType, DunningTypeDto>(t);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Edit)]
    public async Task<DunningTypeDto> UpdateAsync(Guid id, UpdateDunningTypeDto input)
    {
        if (input.DunningFee < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "DunningFee");
        }
        if (input.RateOfInterest < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "RateOfInterest");
        }

        var t = (await _repository.WithDetailsAsync()).First(x => x.Id == id);
        await ValidateDunningTypeAccountsAndCostCentersAsync(t.CompanyId, input.IncomeAccountId, input.CostCenterId);

        t.Rename(input.DunningTypeName);
        ApplyFields(t, input);
        await _repository.UpdateAsync(t);

        if (t.IsDefault)
            await DemoteOtherDefaultsAsync(t.CompanyId, t.Id);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new DocumentActivityLog(GuidGenerator.Create(),
            "DunningType", t.Id, "Updated", t.CompanyId,
            t.DunningTypeName, "Active", "Active",
            CurrentUser.Id,
            $"Dunning type '{t.DunningTypeName}' updated", t.TenantId));

        return ObjectMapper.Map<DunningType, DunningTypeDto>(t);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    private static void ApplyFields(DunningType t, CreateDunningTypeDto input)
    {
        t.IsDefault = input.IsDefault;
        t.DunningFee = input.DunningFee;
        t.RateOfInterest = input.RateOfInterest;
        t.IncomeAccountId = input.IncomeAccountId;
        t.CostCenterId = input.CostCenterId;
        t.SetLetterText(input.LetterText.Select(r => (r.Language, r.IsDefaultLanguage, r.BodyText, r.ClosingText)));
    }

    private async Task DemoteOtherDefaultsAsync(Guid companyId, Guid keepId)
    {
        var query = await _repository.GetQueryableAsync();
        var others = query.Where(x => x.CompanyId == companyId && x.IsDefault && x.Id != keepId).ToList();
        foreach (var other in others)
        {
            other.IsDefault = false;
            await _repository.UpdateAsync(other);
        }
    }

    private async Task ValidateDunningTypeAccountsAndCostCentersAsync(Guid companyId, Guid? incomeAccountId, Guid? costCenterId)
    {
        if (incomeAccountId.HasValue)
        {
            var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
            var account = await accountRepo.FindAsync(incomeAccountId.Value);
            if (account == null || account.CompanyId != companyId)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.EntityNotFound)
                    .WithData("reason", "Income Account does not belong to the specified Company");
            }
            if (!account.IsActive)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("reason", "Income Account is disabled/inactive");
            }
            if (account.IsGroup)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                    .WithData("reason", "Income Account cannot be a group account");
            }
            if (account.AccountType != AccountType.Revenue)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Selected account must be an Income/Revenue account");
            }
        }

        if (costCenterId.HasValue)
        {
            var ccRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CostCenter, Guid>>();
            var cc = await ccRepo.FindAsync(costCenterId.Value);
            if (cc == null || cc.CompanyId != companyId)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.EntityNotFound)
                    .WithData("reason", "Cost Center does not belong to the specified Company");
            }
            if (!cc.IsActive)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Cost Center is disabled/inactive");
            }
            if (cc.IsGroup)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Cost Center cannot be a group cost center");
            }
        }
    }
}
