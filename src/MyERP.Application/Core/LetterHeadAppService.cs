using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize]
public class LetterHeadAppService : MyERPAppService, ILetterHeadAppService
{
    private readonly IRepository<LetterHead, Guid> _repository;

    public LetterHeadAppService(IRepository<LetterHead, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<LetterHeadDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new LetterHeadMapper().Map(entity);
    }

    public async Task<PagedResultDto<LetterHeadDto>> GetListAsync(GetLetterHeadListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (input.LetterHeadFor.HasValue)
            query = query.Where(x => x.LetterHeadFor == input.LetterHeadFor.Value);

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.LetterHeadName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        return new PagedResultDto<LetterHeadDto>(
            totalCount,
            entities.Select(e => new LetterHeadMapper().Map(e)).ToList());
    }

    public async Task<LetterHeadDto> CreateAsync(CreateUpdateLetterHeadDto input)
    {
        var entity = new LetterHead(GuidGenerator.Create(), input.CompanyId, input.LetterHeadName, input.LetterHeadFor, CurrentTenant.Id)
        {
            HeaderContent = input.HeaderContent,
            FooterContent = input.FooterContent,
            IsDisabled = input.IsDisabled,
        };

        if (input.IsDefault)
        {
            await ClearOtherDefaultsAsync(input.CompanyId, input.LetterHeadFor, exceptId: null);
            entity.IsDefault = true;
        }

        await _repository.InsertAsync(entity);
        return new LetterHeadMapper().Map(entity);
    }

    public async Task<LetterHeadDto> UpdateAsync(Guid id, CreateUpdateLetterHeadDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.LetterHeadName = input.LetterHeadName;
        entity.LetterHeadFor = input.LetterHeadFor;
        entity.HeaderContent = input.HeaderContent;
        entity.FooterContent = input.FooterContent;
        entity.IsDisabled = input.IsDisabled;

        if (input.IsDefault && !entity.IsDefault)
        {
            await ClearOtherDefaultsAsync(input.CompanyId, input.LetterHeadFor, exceptId: id);
        }
        entity.IsDefault = input.IsDefault;

        await _repository.UpdateAsync(entity);
        return new LetterHeadMapper().Map(entity);
    }

    /// <summary>Marks this letter head as the default for its category (DocType vs Report), unsetting any other current default.</summary>
    public async Task<LetterHeadDto> SetDefaultAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        await ClearOtherDefaultsAsync(entity.CompanyId, entity.LetterHeadFor, exceptId: id);
        entity.IsDefault = true;
        await _repository.UpdateAsync(entity);
        return new LetterHeadMapper().Map(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private async Task ClearOtherDefaultsAsync(Guid companyId, LetterHeadFor letterHeadFor, Guid? exceptId)
    {
        var query = await _repository.GetQueryableAsync();
        var currentDefaults = await AsyncExecuter.ToListAsync(
            query.Where(x => x.CompanyId == companyId && x.LetterHeadFor == letterHeadFor && x.IsDefault
                && (!exceptId.HasValue || x.Id != exceptId.Value)));

        foreach (var current in currentDefaults)
        {
            current.IsDefault = false;
            await _repository.UpdateAsync(current);
        }
    }
}
