using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        var query = await _posProfileRepository.WithDetailsAsync(x => x.PaymentMethods);
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
            TaxTemplateId = input.TaxTemplateId,
            WriteOffAccountId = input.WriteOffAccountId,
            WriteOffCostCenterId = input.WriteOffCostCenterId,
            WriteOffLimit = input.WriteOffLimit,
            PostChangeGlEntries = input.PostChangeGlEntries,
            IncomeAccountId = input.IncomeAccountId,
            ExpenseAccountId = input.ExpenseAccountId,
        };

        if (input.PaymentMethods != null)
        {
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

        await _posProfileRepository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<PosProfile, PosProfileDto>(entity);
    }

    public override async Task<PosProfileDto> UpdateAsync(Guid id, CreateUpdatePosProfileDto input)
    {
        var query = await _posProfileRepository.WithDetailsAsync(x => x.PaymentMethods);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id));
        if (entity == null)
        {
            throw new BusinessException("MyERP:EntityNotFound");
        }

        entity.ProfileName = input.ProfileName;
        entity.WarehouseId = input.WarehouseId;
        entity.PriceListId = input.PriceListId;
        entity.DefaultCustomerId = input.DefaultCustomerId;
        entity.CurrencyCode = input.CurrencyCode ?? "MYR";
        entity.ValidateStock = input.ValidateStock;
        entity.InvoiceType = input.InvoiceType ?? "POS Invoice";
        entity.IsDisabled = input.IsDisabled;
        entity.TaxTemplateId = input.TaxTemplateId;
        entity.WriteOffAccountId = input.WriteOffAccountId;
        entity.WriteOffCostCenterId = input.WriteOffCostCenterId;
        entity.WriteOffLimit = input.WriteOffLimit;
        entity.PostChangeGlEntries = input.PostChangeGlEntries;
        entity.IncomeAccountId = input.IncomeAccountId;
        entity.ExpenseAccountId = input.ExpenseAccountId;

        entity.ClearPaymentMethods();
        if (input.PaymentMethods != null)
        {
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

        await _posProfileRepository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<PosProfile, PosProfileDto>(entity);
    }

    public async Task<PosProfileDto> EnableAsync(Guid id)
    {
        var entity = await _posProfileRepository.GetAsync(id);
        entity.Enable();
        await _posProfileRepository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<PosProfile, PosProfileDto>(entity);
    }

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
}
