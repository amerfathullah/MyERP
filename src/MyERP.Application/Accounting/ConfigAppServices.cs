using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

// --- Accounting Period ---
[Authorize(MyERPPermissions.Accounts.Default)]
public class AccountingPeriodAppService : ApplicationService, IAccountingPeriodAppService
{
    private readonly IRepository<AccountingPeriod, Guid> _repository;
    public AccountingPeriodAppService(IRepository<AccountingPeriod, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<AccountingPeriodDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(a => a.StartDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<AccountingPeriodDto>(totalCount, items.Select(ObjectMapper.Map<AccountingPeriod, AccountingPeriodDto>).ToList());
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<AccountingPeriodDto> CloseAsync(Guid id)
    {
        var ap = await _repository.GetAsync(id);
        ap.Close();
        await _repository.UpdateAsync(ap);
        return ObjectMapper.Map<AccountingPeriod, AccountingPeriodDto>(ap);
    }
}

// --- Mode of Payment ---
[Authorize(MyERPPermissions.Accounts.Default)]
public class ModeOfPaymentAppService : ApplicationService, IModeOfPaymentAppService
{
    private readonly IRepository<ModeOfPayment, Guid> _repository;
    public ModeOfPaymentAppService(IRepository<ModeOfPayment, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ModeOfPaymentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(m => m.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ModeOfPaymentDto>(totalCount, items.Select(ObjectMapper.Map<ModeOfPayment, ModeOfPaymentDto>).ToList());
    }

    public async Task<ModeOfPaymentDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<ModeOfPayment, ModeOfPaymentDto>(entity);
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<ModeOfPaymentDto> CreateAsync(CreateUpdateModeOfPaymentDto input)
    {
        var entity = new ModeOfPayment(GuidGenerator.Create(), input.Name, input.Type, CurrentTenant.Id)
        {
            IsActive = input.IsActive,
            DefaultAccountId = input.DefaultAccountId,
            CompanyId = input.CompanyId
        };
        await _repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<ModeOfPayment, ModeOfPaymentDto>(entity);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<ModeOfPaymentDto> UpdateAsync(Guid id, CreateUpdateModeOfPaymentDto input)
    {
        var entity = await _repository.GetAsync(id);

        // Gotcha #1520: Cannot disable Mode of Payment if configured in active POS Profile
        if (entity.IsActive && !input.IsActive)
        {
            var posProfileRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.PosProfile, Guid>>();
            var activeProfilesWithMop = (await posProfileRepo.GetListAsync(p => !p.IsDisabled, includeDetails: true))
                .Where(p => p.PaymentMethods.Any(pm => pm.ModeOfPaymentId == id))
                .Select(p => p.ProfileName)
                .ToList();

            if (activeProfilesWithMop.Count > 0)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("message", $"Cannot disable Mode of Payment '{entity.Name}' as it is configured in active POS Profile(s): {string.Join(", ", activeProfilesWithMop)}");
            }
        }

        entity.Name = Volo.Abp.Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name), 100);
        entity.Type = input.Type;
        entity.IsActive = input.IsActive;
        entity.DefaultAccountId = input.DefaultAccountId;
        entity.CompanyId = input.CompanyId;

        await _repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<ModeOfPayment, ModeOfPaymentDto>(entity);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);

        // Gotcha #1520: Cannot delete Mode of Payment if configured in active POS Profile
        var posProfileRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.PosProfile, Guid>>();
        var activeProfilesWithMop = (await posProfileRepo.GetListAsync(p => !p.IsDisabled, includeDetails: true))
            .Where(p => p.PaymentMethods.Any(pm => pm.ModeOfPaymentId == id))
            .Select(p => p.ProfileName)
            .ToList();

        if (activeProfilesWithMop.Count > 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("message", $"Cannot delete Mode of Payment '{entity.Name}' as it is configured in active POS Profile(s): {string.Join(", ", activeProfilesWithMop)}");
        }

        await _repository.DeleteAsync(entity, autoSave: true);
    }
}

// --- UOM Conversion ---
[Authorize(MyERPPermissions.Items.Default)]
public class UomConversionAppService : ApplicationService, IUomConversionAppService
{
    private readonly IRepository<Inventory.Entities.UomConversion, Guid> _repository;
    public UomConversionAppService(IRepository<Inventory.Entities.UomConversion, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<UomConversionDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(u => u.FromUom).ThenBy(u => u.ToUom)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<UomConversionDto>(totalCount, items.Select(ObjectMapper.Map<Inventory.Entities.UomConversion, UomConversionDto>).ToList());
    }
}
