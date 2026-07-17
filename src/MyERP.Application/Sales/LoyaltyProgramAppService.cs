using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Sales.DomainServices;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.LoyaltyPrograms.Default)]
public class LoyaltyProgramAppService : ApplicationService
{
    private readonly IRepository<LoyaltyProgram, Guid> _repository;
    private readonly IRepository<LoyaltyPointEntry, Guid> _entryRepository;
    private readonly LoyaltyPointService _loyaltyPointService;

    public LoyaltyProgramAppService(
        IRepository<LoyaltyProgram, Guid> repository,
        IRepository<LoyaltyPointEntry, Guid> entryRepository,
        LoyaltyPointService loyaltyPointService)
    {
        _repository = repository;
        _entryRepository = entryRepository;
        _loyaltyPointService = loyaltyPointService;
    }

    public async Task<LoyaltyProgramDto> GetAsync(Guid id)
    {
        var program = await _repository.GetAsync(id);
        return ObjectMapper.Map<LoyaltyProgram, LoyaltyProgramDto>(program);
    }

    public async Task<PagedResultDto<LoyaltyProgramDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var count = query.Count();
        var list = query.OrderBy(x => x.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<LoyaltyProgramDto>(count, list.Select(x => ObjectMapper.Map<LoyaltyProgram, LoyaltyProgramDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.LoyaltyPrograms.Create)]
    public async Task<LoyaltyProgramDto> CreateAsync(CreateLoyaltyProgramDto input)
    {
        var program = new LoyaltyProgram(
            GuidGenerator.Create(),
            input.CompanyId,
            input.Name,
            input.ConversionFactor,
            input.ExpiryDurationDays,
            CurrentTenant.Id);

        program.ExpenseAccountId = input.ExpenseAccountId;
        program.CostCenterId = input.CostCenterId;

        foreach (var tier in input.Tiers)
        {
            program.AddTier(tier.TierName, tier.MinSpent, tier.CollectionFactor, tier.RedemptionFactor);
        }

        program.Validate();
        await _repository.InsertAsync(program);
        return ObjectMapper.Map<LoyaltyProgram, LoyaltyProgramDto>(program);
    }

    [Authorize(MyERPPermissions.LoyaltyPrograms.Edit)]
    public async Task<LoyaltyProgramDto> UpdateAsync(Guid id, UpdateLoyaltyProgramDto input)
    {
        var program = await _repository.GetAsync(id);
        program.Name = input.Name;
        program.ConversionFactor = input.ConversionFactor;
        program.ExpiryDurationDays = input.ExpiryDurationDays;
        program.IsEnabled = input.IsEnabled;
        program.ExpenseAccountId = input.ExpenseAccountId;
        program.CostCenterId = input.CostCenterId;
        await _repository.UpdateAsync(program);
        return ObjectMapper.Map<LoyaltyProgram, LoyaltyProgramDto>(program);
    }

    [Authorize(MyERPPermissions.LoyaltyPrograms.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    /// <summary>
    /// Get loyalty points balance for a customer.
    /// </summary>
    public async Task<LoyaltyBalanceDto> GetCustomerBalanceAsync(Guid customerId, Guid programId)
    {
        var available = await _loyaltyPointService.GetAvailablePointsAsync(customerId, programId, DateTime.UtcNow);
        var program = await _repository.GetAsync(programId);
        var tierName = await _loyaltyPointService.GetCurrentTierAsync(customerId, programId);

        // Find tier object for redemption value calculation
        var tier = program.Tiers.FirstOrDefault(t => t.TierName == tierName);

        return new LoyaltyBalanceDto
        {
            CustomerId = customerId,
            ProgramId = programId,
            ProgramName = program.Name,
            AvailablePoints = available,
            CurrentTier = tierName,
            RedemptionValue = tier != null ? program.CalculateRedemptionValue(available, tier) : 0
        };
    }

    /// <summary>
    /// Get point history for a customer.
    /// </summary>
    public async Task<List<LoyaltyPointEntryDto>> GetPointHistoryAsync(Guid customerId, Guid programId)
    {
        var query = await _entryRepository.GetQueryableAsync();
        var entries = query
            .Where(e => e.CustomerId == customerId && e.LoyaltyProgramId == programId)
            .OrderByDescending(e => e.PostingDate)
            .Take(100)
            .ToList();

        return entries.Select(e => new LoyaltyPointEntryDto
        {
            Id = e.Id,
            Points = e.Points,
            PostingDate = e.PostingDate,
            ExpiryDate = e.ExpiryDate,
            InvoiceType = e.InvoiceType,
            InvoiceId = e.InvoiceId,
            TierName = e.TierName,
            IsExpired = e.IsExpired,
            IsEarning = e.IsEarning
        }).ToList();
    }

    /// <summary>
    /// Redeem loyalty points for a customer (creates a negative point entry).
    /// Returns the currency value of redeemed points.
    /// </summary>
    [Authorize(MyERPPermissions.LoyaltyPrograms.Edit)]
    public async Task<decimal> RedeemPointsAsync(Guid customerId, Guid programId, int pointsToRedeem, Guid companyId)
    {
        var result = await _loyaltyPointService.RedeemPointsAsync(
            programId, customerId, pointsToRedeem, DateTime.UtcNow, companyId);
        return result.RedemptionValue;
    }
}

#region DTOs

public class LoyaltyProgramDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
    public int ExpiryDurationDays { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public List<LoyaltyProgramTierDto> Tiers { get; set; } = new();
}

public class LoyaltyProgramTierDto
{
    public string TierName { get; set; } = null!;
    public decimal MinSpent { get; set; }
    public decimal CollectionFactor { get; set; }
    public decimal RedemptionFactor { get; set; }
}

public class CreateLoyaltyProgramDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
    public int ExpiryDurationDays { get; set; } = 365;
    public Guid? ExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public List<LoyaltyProgramTierDto> Tiers { get; set; } = new();
}

public class UpdateLoyaltyProgramDto
{
    public string Name { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
    public int ExpiryDurationDays { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
}

public class LoyaltyBalanceDto
{
    public Guid CustomerId { get; set; }
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = null!;
    public int AvailablePoints { get; set; }
    public string? CurrentTier { get; set; }
    public decimal RedemptionValue { get; set; }
}

public class LoyaltyPointEntryDto
{
    public Guid Id { get; set; }
    public int Points { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? InvoiceType { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? TierName { get; set; }
    public bool IsExpired { get; set; }
    public bool IsEarning { get; set; }
}

#endregion
