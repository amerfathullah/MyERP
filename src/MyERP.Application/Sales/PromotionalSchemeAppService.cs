using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.PromotionalSchemes.Default)]
public class PromotionalSchemeAppService : ApplicationService, IPromotionalSchemeAppService
{
    private readonly IRepository<PromotionalScheme, Guid> _repository;
    private readonly IRepository<PricingRule, Guid> _pricingRuleRepository;
    private readonly PromotionalSchemeService _schemeService;

    public PromotionalSchemeAppService(
        IRepository<PromotionalScheme, Guid> repository,
        IRepository<PricingRule, Guid> pricingRuleRepository,
        PromotionalSchemeService schemeService)
    {
        _repository = repository;
        _pricingRuleRepository = pricingRuleRepository;
        _schemeService = schemeService;
    }

    public async Task<PagedResultDto<PromotionalSchemeDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(s => s.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(s => s.Title.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        var dtos = items.Select(s => ObjectMapper.Map<PromotionalScheme, PromotionalSchemeDto>(s)).ToList();
        await FillGeneratedRuleCountsAsync(dtos);
        return new PagedResultDto<PromotionalSchemeDto>(totalCount, dtos);
    }

    public async Task<PromotionalSchemeDto> GetAsync(Guid id)
    {
        var scheme = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        var dto = ObjectMapper.Map<PromotionalScheme, PromotionalSchemeDto>(scheme);
        await FillGeneratedRuleCountsAsync(new[] { dto });
        return dto;
    }

    [Authorize(MyERPPermissions.PromotionalSchemes.Create)]
    public async Task<PromotionalSchemeDto> CreateAsync(CreateUpdatePromotionalSchemeDto input)
    {
        ValidateInput(input);

        var scheme = new PromotionalScheme(GuidGenerator.Create(), input.CompanyId, input.Title, input.ApplyOn, CurrentTenant.Id);
        ApplyInput(scheme, input);

        await _schemeService.RegeneratePricingRulesAsync(scheme);
        await _repository.InsertAsync(scheme);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PromotionalScheme", scheme.Id,
            "Created", scheme.CompanyId,
            scheme.Title, "Draft", "Active", CurrentUser.Id,
            $"Promotional scheme '{scheme.Title}' created", CurrentTenant.Id));

        var dto = ObjectMapper.Map<PromotionalScheme, PromotionalSchemeDto>(scheme);
        await FillGeneratedRuleCountsAsync(new[] { dto });
        return dto;
    }

    [Authorize(MyERPPermissions.PromotionalSchemes.Edit)]
    public async Task<PromotionalSchemeDto> UpdateAsync(Guid id, CreateUpdatePromotionalSchemeDto input)
    {
        ValidateInput(input);

        var scheme = (await _repository.WithDetailsAsync()).First(s => s.Id == id);

        scheme.Title = input.Title;
        scheme.ApplyOn = input.ApplyOn;
        ApplyInput(scheme, input);

        await _schemeService.RegeneratePricingRulesAsync(scheme);
        await _repository.UpdateAsync(scheme);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PromotionalScheme", scheme.Id,
            "Updated", scheme.CompanyId,
            scheme.Title, "Active", "Active", CurrentUser.Id,
            $"Promotional scheme '{scheme.Title}' updated", CurrentTenant.Id));

        var dto = ObjectMapper.Map<PromotionalScheme, PromotionalSchemeDto>(scheme);
        await FillGeneratedRuleCountsAsync(new[] { dto });
        return dto;
    }

    [Authorize(MyERPPermissions.PromotionalSchemes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _schemeService.DeleteGeneratedRulesAsync(id);
        await _repository.DeleteAsync(id);
    }

    private static void ValidateInput(CreateUpdatePromotionalSchemeDto input)
    {
        if (input.ValidUpto.HasValue && input.ValidFrom.HasValue && input.ValidUpto.Value < input.ValidFrom.Value)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        foreach (var s in input.PriceDiscountSlabs)
        {
            if (s.DiscountPercentage < 0 || s.DiscountPercentage > 100)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDiscountPercentage);
            }

            if (s.DiscountAmount < 0 || s.Rate < 0 || s.MinQty < 0 || s.MaxQty < 0 || s.MinAmount < 0 || s.MaxAmount < 0)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "PriceDiscountSlabs");
            }
        }
    }

    private static void ApplyInput(PromotionalScheme scheme, CreateUpdatePromotionalSchemeDto input)
    {
        scheme.CompanyId = input.CompanyId;
        scheme.IsDisabled = input.IsDisabled;
        scheme.MixedConditions = input.MixedConditions;
        scheme.IsCumulative = input.IsCumulative;
        scheme.ApplyRuleOnOtherItem = input.ApplyRuleOnOtherItem;
        scheme.OtherApplyOn = input.OtherApplyOn;
        scheme.OtherTargetId = input.OtherTargetId;
        scheme.Selling = input.Selling;
        scheme.Buying = input.Buying;
        scheme.ApplicableFor = input.ApplicableFor;
        scheme.ValidFrom = input.ValidFrom;
        scheme.ValidUpto = input.ValidUpto;
        scheme.CurrencyId = input.CurrencyId;

        scheme.ClearTargets();
        foreach (var t in input.Targets)
            scheme.AddTarget(t.TargetId, t.TargetName);

        scheme.ClearParties();
        foreach (var p in input.Parties)
            scheme.AddParty(p.PartyId, p.PartyName);

        scheme.ClearPriceDiscountSlabs();
        foreach (var s in input.PriceDiscountSlabs)
            scheme.AddPriceDiscountSlab((PricingRuleType)s.RateOrDiscount, s.DiscountPercentage, s.DiscountAmount,
                s.Rate, s.MinQty, s.MaxQty, s.MinAmount, s.MaxAmount, s.Priority, s.WarehouseId, s.Description);

        scheme.ClearProductDiscountSlabs();
        foreach (var s in input.ProductDiscountSlabs)
            scheme.AddProductDiscountSlab(s.FreeItemId, s.FreeQty, s.FreeItemRate, s.SameItem,
                s.MinQty, s.MaxQty, s.MinAmount, s.MaxAmount, s.Priority, s.WarehouseId, s.Description,
                s.IsRecursive, s.RecurseFor, s.RoundFreeQty);
    }

    private async Task FillGeneratedRuleCountsAsync(System.Collections.Generic.IReadOnlyCollection<PromotionalSchemeDto> dtos)
    {
        if (!dtos.Any()) return;
        var schemeIds = dtos.Select(d => d.Id).ToList();
        var query = await _pricingRuleRepository.GetQueryableAsync();
        var counts = query.Where(r => r.PromotionalSchemeId.HasValue && schemeIds.Contains(r.PromotionalSchemeId!.Value))
            .GroupBy(r => r.PromotionalSchemeId!.Value)
            .Select(g => new { SchemeId = g.Key, Count = g.Count() })
            .ToList();

        foreach (var dto in dtos)
            dto.GeneratedRuleCount = counts.FirstOrDefault(c => c.SchemeId == dto.Id)?.Count ?? 0;
    }
}
