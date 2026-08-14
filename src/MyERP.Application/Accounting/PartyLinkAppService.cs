using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Manages inter-company party links (Customer ↔ Supplier bidirectional mapping).
/// Per ERPNext: PartyLink enables inter-company transaction creation.
/// Per DO-NOT: same party cannot appear in multiple links.
/// </summary>
[Authorize(MyERPPermissions.Accounts.Default)]
public class PartyLinkAppService : ApplicationService, IPartyLinkAppService
{
    private readonly IRepository<PartyLink, Guid> _repository;

    public PartyLinkAppService(IRepository<PartyLink, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<PartyLinkDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query
            .OrderByDescending(l => l.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<PartyLinkDto>(
            totalCount,
            items.Select(l => new PartyLinkDto
            {
                Id = l.Id,
                PrimaryPartyType = l.PrimaryPartyType,
                PrimaryPartyId = l.PrimaryPartyId,
                SecondaryPartyType = l.SecondaryPartyType,
                SecondaryPartyId = l.SecondaryPartyId,
            }).ToList());
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<PartyLinkDto> CreateAsync(CreatePartyLinkDto input)
    {
        // Validate no duplicate links for either party
        var query = await _repository.GetQueryableAsync();
        var existingPrimary = query.Any(l =>
            l.PrimaryPartyId == input.PrimaryPartyId || l.SecondaryPartyId == input.PrimaryPartyId);
        var existingSecondary = query.Any(l =>
            l.PrimaryPartyId == input.SecondaryPartyId || l.SecondaryPartyId == input.SecondaryPartyId);

        if (existingPrimary || existingSecondary)
        {
            throw new BusinessException("MyERP:00005")
                .WithData("reason", "One or both parties are already linked.");
        }

        var link = new PartyLink(
            GuidGenerator.Create(),
            input.PrimaryPartyType, input.PrimaryPartyId,
            input.SecondaryPartyType, input.SecondaryPartyId,
            CurrentTenant.Id);

        await _repository.InsertAsync(link);

        return new PartyLinkDto
        {
            Id = link.Id,
            PrimaryPartyType = link.PrimaryPartyType,
            PrimaryPartyId = link.PrimaryPartyId,
            SecondaryPartyType = link.SecondaryPartyType,
            SecondaryPartyId = link.SecondaryPartyId,
        };
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
