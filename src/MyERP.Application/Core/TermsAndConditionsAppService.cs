using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize(MyERPPermissions.TermsAndConditions.Default)]
public class TermsAndConditionsAppService : MyERPAppService, ITermsAndConditionsAppService
{
    private readonly IRepository<TermsAndConditions, Guid> _repository;

    public TermsAndConditionsAppService(IRepository<TermsAndConditions, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<TermsAndConditionsDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new TermsAndConditionsMapper().Map(entity);
    }

    public async Task<PagedResultDto<TermsAndConditionsDto>> GetListAsync(GetTermsAndConditionsListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (input.IsSelling.HasValue)
            query = query.Where(x => x.IsSelling == input.IsSelling.Value);
        if (input.IsBuying.HasValue)
            query = query.Where(x => x.IsBuying == input.IsBuying.Value);
        if (input.IsDisabled.HasValue)
            query = query.Where(x => x.IsDisabled == input.IsDisabled.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(x => x.Title.Contains(input.Filter));

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Title)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        return new PagedResultDto<TermsAndConditionsDto>(
            totalCount,
            entities.Select(e => new TermsAndConditionsMapper().Map(e)).ToList());
    }

    [Authorize(MyERPPermissions.TermsAndConditions.Create)]
    public async Task<TermsAndConditionsDto> CreateAsync(CreateUpdateTermsAndConditionsDto input)
    {
        var entity = new TermsAndConditions(GuidGenerator.Create(), input.CompanyId, input.Title, CurrentTenant.Id)
        {
            Terms = input.Terms,
            IsSelling = input.IsSelling,
            IsBuying = input.IsBuying,
            IsDisabled = input.IsDisabled,
            CopyAttachmentsToTransaction = input.CopyAttachmentsToTransaction,
        };

        await _repository.InsertAsync(entity);
        return new TermsAndConditionsMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.TermsAndConditions.Edit)]
    public async Task<TermsAndConditionsDto> UpdateAsync(Guid id, CreateUpdateTermsAndConditionsDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Title = input.Title;
        entity.Terms = input.Terms;
        entity.IsSelling = input.IsSelling;
        entity.IsBuying = input.IsBuying;
        entity.IsDisabled = input.IsDisabled;
        entity.CopyAttachmentsToTransaction = input.CopyAttachmentsToTransaction;

        await _repository.UpdateAsync(entity);
        return new TermsAndConditionsMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.TermsAndConditions.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
