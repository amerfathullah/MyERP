using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class PosProfileAppService : CrudAppService<
    PosProfile,
    PosProfileDto,
    Guid,
    GetPosProfileListDto,
    CreateUpdatePosProfileDto>,
    IPosProfileAppService
{
    private readonly IRepository<PosProfile, Guid> _posProfileRepository;

    public PosProfileAppService(IRepository<PosProfile, Guid> repository)
        : base(repository)
    {
        _posProfileRepository = repository;
        GetPolicyName = MyERPPermissions.PosProfiles.Default;
        GetListPolicyName = MyERPPermissions.PosProfiles.Default;
        CreatePolicyName = MyERPPermissions.PosProfiles.Create;
        UpdatePolicyName = MyERPPermissions.PosProfiles.Edit;
        DeletePolicyName = MyERPPermissions.PosProfiles.Delete;
    }

    protected override async Task<IQueryable<PosProfile>> CreateFilteredQueryAsync(GetPosProfileListDto input)
    {
        var query = await base.CreateFilteredQueryAsync(input);

        if (input.CompanyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        }

        if (input.IsDisabled.HasValue)
        {
            query = query.Where(x => x.IsDisabled == input.IsDisabled.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.ProfileName.ToLower().Contains(filter));
        }

        return query;
    }

    public override async Task<PosProfileDto> GetAsync(Guid id)
    {
        var query = await _posProfileRepository.WithDetailsAsync(x => x.PaymentMethods, x => x.Users);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id));
        if (entity == null)
        {
            throw new BusinessException("MyERP:EntityNotFound");
        }

        return ObjectMapper.Map<PosProfile, PosProfileDto>(entity);
    }

    public override async Task<PosProfileDto> CreateAsync(CreateUpdatePosProfileDto input)
    {
        var entity = new PosProfile(
            GuidGenerator.Create(),
            input.CompanyId,
            input.ProfileName,
            input.WarehouseId,
            CurrentTenant.Id)
        {
            PriceListId = input.PriceListId,
            DefaultCustomerId = input.DefaultCustomerId,
            CurrencyCode = input.CurrencyCode ?? "MYR",
            ValidateStock = input.ValidateStock,
            InvoiceType = input.InvoiceType ?? "POS Invoice",
            IsDisabled = input.IsDisabled,
            HideUnavailableItems = input.HideUnavailableItems,
            TaxTemplateId = input.TaxTemplateId,
            WriteOffAccountId = input.WriteOffAccountId,
            WriteOffCostCenterId = input.WriteOffCostCenterId,
            WriteOffLimit = input.WriteOffLimit,
            PostChangeGlEntries = input.PostChangeGlEntries,
            IncomeAccountId = input.IncomeAccountId,
            ExpenseAccountId = input.ExpenseAccountId,
            ProjectId = input.ProjectId,
        };

        if (input.PaymentMethods != null)
        {
            var dupes = input.PaymentMethods.GroupBy(p => p.ModeOfPaymentId).Where(g => g.Count() > 1).ToList();
            if (dupes.Any())
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Duplicate Mode of Payment added in Payment Methods table.");
            }

            foreach (var pm in input.PaymentMethods)
            {
                var method = new PosProfilePaymentMethod(
                    GuidGenerator.Create(),
                    entity.Id,
                    pm.ModeOfPaymentId,
                    pm.AccountId)
                {
                    IsDefault = pm.IsDefault,
                };
                entity.AddPaymentMethod(method);
            }
        }

        if (input.Users != null)
        {
            var userDupes = input.Users.GroupBy(u => u.UserId).Where(g => g.Count() > 1).ToList();
            if (userDupes.Any())
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Duplicate User added in Applicable Users table.");
            }

            foreach (var userDto in input.Users)
            {
                entity.AddUser(userDto.UserId, userDto.IsDefault);
            }
        }

        await _posProfileRepository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<PosProfile, PosProfileDto>(entity);
    }

    public override async Task<PosProfileDto> UpdateAsync(Guid id, CreateUpdatePosProfileDto input)
    {
        var query = await _posProfileRepository.WithDetailsAsync(x => x.PaymentMethods, x => x.Users);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id));
        if (entity == null)
        {
            throw new BusinessException("MyERP:EntityNotFound");
        }

        var newInvoiceType = input.InvoiceType ?? "POS Invoice";
        if (!string.Equals(entity.InvoiceType, newInvoiceType, StringComparison.OrdinalIgnoreCase))
        {
            var openingRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var openingQuery = await openingRepo.GetQueryableAsync();
            var hasOpenSession = openingQuery.Any(e => e.PosProfileId == id && e.Status == PosOpeningStatus.Open);
            if (hasOpenSession)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Cannot change Invoice Type while an open POS Opening Entry exists for this POS Profile. Close all open POS shifts first.");
            }
        }

        if (!entity.IsDisabled && input.IsDisabled)
        {
            var openingRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var openingQuery = await openingRepo.GetQueryableAsync();
            var hasOpenSession = openingQuery.Any(e => e.PosProfileId == id && e.Status == PosOpeningStatus.Open);
            if (hasOpenSession)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PosProfileHasOpenSession)
                    .WithData("reason", "Cannot disable a POS Profile while an open POS Opening Entry exists for it. Close the shift first.");
            }
        }

        entity.ProfileName = input.ProfileName;
        entity.WarehouseId = input.WarehouseId;
        entity.PriceListId = input.PriceListId;
        entity.DefaultCustomerId = input.DefaultCustomerId;
        entity.CurrencyCode = input.CurrencyCode ?? "MYR";
        entity.ValidateStock = input.ValidateStock;
        entity.InvoiceType = newInvoiceType;
        entity.IsDisabled = input.IsDisabled;
        entity.HideUnavailableItems = input.HideUnavailableItems;
        entity.TaxTemplateId = input.TaxTemplateId;
        entity.WriteOffAccountId = input.WriteOffAccountId;
        entity.WriteOffCostCenterId = input.WriteOffCostCenterId;
        entity.WriteOffLimit = input.WriteOffLimit;
        entity.PostChangeGlEntries = input.PostChangeGlEntries;
        entity.IncomeAccountId = input.IncomeAccountId;
        entity.ExpenseAccountId = input.ExpenseAccountId;
        entity.ProjectId = input.ProjectId;

        entity.ClearPaymentMethods();
        if (input.PaymentMethods != null)
        {
            var dupes = input.PaymentMethods.GroupBy(p => p.ModeOfPaymentId).Where(g => g.Count() > 1).ToList();
            if (dupes.Any())
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Duplicate Mode of Payment added in Payment Methods table.");
            }

            foreach (var pm in input.PaymentMethods)
            {
                var method = new PosProfilePaymentMethod(
                    GuidGenerator.Create(),
                    entity.Id,
                    pm.ModeOfPaymentId,
                    pm.AccountId)
                {
                    IsDefault = pm.IsDefault,
                };
                entity.AddPaymentMethod(method);
            }
        }

        entity.ClearUsers();
        if (input.Users != null)
        {
            var userDupes = input.Users.GroupBy(u => u.UserId).Where(g => g.Count() > 1).ToList();
            if (userDupes.Any())
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Duplicate User added in Applicable Users table.");
            }

            foreach (var userDto in input.Users)
            {
                entity.AddUser(userDto.UserId, userDto.IsDefault);
            }
        }

        await _posProfileRepository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<PosProfile, PosProfileDto>(entity);
    }

    [Authorize(MyERPPermissions.PosProfiles.Edit)]
    public async Task<PosProfileDto> EnableAsync(Guid id)
    {
        var entity = await _posProfileRepository.GetAsync(id);
        entity.Enable();
        await _posProfileRepository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<PosProfile, PosProfileDto>(entity);
    }

    [Authorize(MyERPPermissions.PosProfiles.Edit)]
    public async Task<PosProfileDto> DisableAsync(Guid id)
    {
        var entity = await _posProfileRepository.GetAsync(id);

        // Per PosProfile docstring / ERPNext: cannot disable while open POS Opening Entries exist —
        // the cashier session would keep selling against a profile the back office thinks is off.
        var openingRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PosOpeningEntry, Guid>>();
        var openingQuery = await openingRepo.GetQueryableAsync();
        var hasOpenSession = openingQuery.Any(e => e.PosProfileId == id && e.Status == PosOpeningStatus.Open);
        if (hasOpenSession)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PosProfileHasOpenSession)
                .WithData("reason", "Cannot disable a POS Profile while an open POS Opening Entry exists for it. Close the shift first.");
        }

        entity.Disable();
        await _posProfileRepository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<PosProfile, PosProfileDto>(entity);
    }

    /// <summary>
    /// Returns POS profiles available for the current cashier user.
    /// Per ERPNext PR #58508 (commit 9018573179) & PR #58591 (commit 4355f8e60e):
    /// Profiles where current user is assigned take precedence, with IsDefault profiles ranked first.
    /// Profiles with no users configured are accessible to all users.
    /// </summary>
    public async Task<List<PosProfileDto>> GetForCurrentUserAsync(Guid companyId)
    {
        var currentUserId = CurrentUser.Id;
        var query = await _posProfileRepository.WithDetailsAsync(x => x.PaymentMethods, x => x.Users);
        query = query.Where(x => x.CompanyId == companyId && !x.IsDisabled);

        var list = await AsyncExecuter.ToListAsync(query);

        if (currentUserId.HasValue)
        {
            var userProfiles = list.Where(p => p.Users.Count == 0 || p.Users.Any(u => u.UserId == currentUserId.Value)).ToList();
            if (userProfiles.Any())
            {
                userProfiles = userProfiles
                    .OrderByDescending(p => p.Users.Any(u => u.UserId == currentUserId.Value && u.IsDefault))
                    .ThenByDescending(p => p.Users.Any(u => u.UserId == currentUserId.Value))
                    .ThenBy(p => p.ProfileName)
                    .ToList();
                return userProfiles.Select(p => ObjectMapper.Map<PosProfile, PosProfileDto>(p)).ToList();
            }
        }

        return list.Select(p => ObjectMapper.Map<PosProfile, PosProfileDto>(p)).ToList();
    }
}
