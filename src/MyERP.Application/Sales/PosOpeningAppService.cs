using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// POS Opening Entry AppService — manages POS shift opening.
/// Per ERPNext: one open session per profile, one per user.
/// Flow: Open POS → Ring sales → Close POS → Consolidate.
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class PosOpeningAppService : ApplicationService
{
    private readonly IRepository<PosOpeningEntry, Guid> _repository;

    public PosOpeningAppService(IRepository<PosOpeningEntry, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PosOpeningDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        return MapToDto(entry);
    }

    public async Task<PagedResultDto<PosOpeningDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<PosOpeningStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var list = query.OrderByDescending(x => x.OpeningDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<PosOpeningDto>(count, list.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Opens a new POS session.
    /// Per DO-NOT: blocks if another Open session exists for the same profile or user.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<PosOpeningDto> CreateAsync(CreatePosOpeningDto input)
    {
        // Enforce: one open per profile + one open per user
        var query = await _repository.GetQueryableAsync();

        var existingForProfile = query.FirstOrDefault(
            x => x.PosProfileId == input.PosProfileId && x.Status == PosOpeningStatus.Open);
        if (existingForProfile != null)
            throw new BusinessException("MyERP:16001")
                .WithData("profileId", input.PosProfileId);

        var existingForUser = query.FirstOrDefault(
            x => x.UserId == input.UserId && x.Status == PosOpeningStatus.Open);
        if (existingForUser != null)
            throw new BusinessException("MyERP:16002")
                .WithData("userId", input.UserId);

        var entry = new PosOpeningEntry(
            GuidGenerator.Create(), input.CompanyId, input.PosProfileId,
            input.UserId, CurrentTenant.Id);

        foreach (var pay in input.Payments)
            entry.AddOpeningBalance(pay.ModeOfPaymentId, pay.ModeName, pay.OpeningAmount);

        await _repository.InsertAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    /// <summary>
    /// Gets the currently open POS session for a user (for POS terminal init).
    /// </summary>
    public async Task<PosOpeningDto?> GetCurrentOpenAsync(Guid userId)
    {
        var query = await _repository.GetQueryableAsync();
        var entry = query.FirstOrDefault(x => x.UserId == userId && x.Status == PosOpeningStatus.Open);
        return entry != null ? MapToDto(entry) : null;
    }

    /// <summary>
    /// Cancels a POS opening (only from Closed status).
    /// Per DO-NOT: "Cancel POS Opening Entry while unconsolidated invoices exist"
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<PosOpeningDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    private static PosOpeningDto MapToDto(PosOpeningEntry entry) => new()
    {
        Id = entry.Id,
        CompanyId = entry.CompanyId,
        PosProfileId = entry.PosProfileId,
        UserId = entry.UserId,
        OpeningDate = entry.OpeningDate,
        Status = entry.Status.ToString(),
        TotalOpeningAmount = entry.TotalOpeningAmount,
        PosClosingEntryId = entry.PosClosingEntryId,
        Payments = entry.Payments.Select(p => new PosOpeningPaymentDto
        {
            ModeOfPaymentId = p.ModeOfPaymentId,
            ModeName = p.ModeName,
            OpeningAmount = p.OpeningAmount
        }).ToList()
    };
}

public class PosOpeningDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid UserId { get; set; }
    public DateTime OpeningDate { get; set; }
    public string Status { get; set; } = null!;
    public decimal TotalOpeningAmount { get; set; }
    public Guid? PosClosingEntryId { get; set; }
    public List<PosOpeningPaymentDto> Payments { get; set; } = new();
}

public class PosOpeningPaymentDto
{
    public Guid ModeOfPaymentId { get; set; }
    public string ModeName { get; set; } = null!;
    public decimal OpeningAmount { get; set; }
}

public class CreatePosOpeningDto
{
    public Guid CompanyId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid UserId { get; set; }
    public List<CreatePosOpeningPaymentDto> Payments { get; set; } = new();
}

public class CreatePosOpeningPaymentDto
{
    public Guid ModeOfPaymentId { get; set; }
    public string ModeName { get; set; } = null!;
    public decimal OpeningAmount { get; set; }
}
