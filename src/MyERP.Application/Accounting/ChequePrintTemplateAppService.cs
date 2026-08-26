using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.ChequePrintTemplates.Default)]
public class ChequePrintTemplateAppService : MyERPAppService, IChequePrintTemplateAppService
{
    private readonly IRepository<ChequePrintTemplate, Guid> _repository;

    public ChequePrintTemplateAppService(IRepository<ChequePrintTemplate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ChequePrintTemplateDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new ChequePrintTemplateMapper().Map(entity);
    }

    public async Task<PagedResultDto<ChequePrintTemplateDto>> GetListAsync(GetChequePrintTemplateListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.BankName.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.BankName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new ChequePrintTemplateMapper().Map).ToList();
        return new PagedResultDto<ChequePrintTemplateDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.ChequePrintTemplates.Create)]
    public async Task<ChequePrintTemplateDto> CreateAsync(CreateUpdateChequePrintTemplateDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.BankName.ToLower() == input.BankName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Cheque print template for bank '{input.BankName}' already exists.");
        }

        var entity = new ChequePrintTemplate(
            GuidGenerator.Create(),
            input.BankName.Trim(),
            input.ChequeSize,
            input.ChequeWidth,
            input.ChequeHeight,
            CurrentTenant.Id)
        {
            StartingPositionFromTopEdge = input.StartingPositionFromTopEdge,
            ScannedCheque = input.ScannedCheque?.Trim(),
            IsAccountPayable = input.IsAccountPayable,
            AccPayDistFromTopEdge = input.AccPayDistFromTopEdge,
            AccPayDistFromLeftEdge = input.AccPayDistFromLeftEdge,
            MessageToShow = input.MessageToShow?.Trim(),
            DateDistFromTopEdge = input.DateDistFromTopEdge,
            DateDistFromLeftEdge = input.DateDistFromLeftEdge,
            PayerNameFromTopEdge = input.PayerNameFromTopEdge,
            PayerNameFromLeftEdge = input.PayerNameFromLeftEdge,
            AmtInWordsFromTopEdge = input.AmtInWordsFromTopEdge,
            AmtInWordsFromLeftEdge = input.AmtInWordsFromLeftEdge,
            AmtInWordWidth = input.AmtInWordWidth,
            AmtInWordsLineSpacing = input.AmtInWordsLineSpacing,
            AmtInFiguresFromTopEdge = input.AmtInFiguresFromTopEdge,
            AmtInFiguresFromLeftEdge = input.AmtInFiguresFromLeftEdge,
            AccNoDistFromTopEdge = input.AccNoDistFromTopEdge,
            AccNoDistFromLeftEdge = input.AccNoDistFromLeftEdge,
            SignatoryFromTopEdge = input.SignatoryFromTopEdge,
            SignatoryFromLeftEdge = input.SignatoryFromLeftEdge,
            HasPrintFormat = input.HasPrintFormat
        };

        await _repository.InsertAsync(entity);
        return new ChequePrintTemplateMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.ChequePrintTemplates.Edit)]
    public async Task<ChequePrintTemplateDto> UpdateAsync(Guid id, CreateUpdateChequePrintTemplateDto input)
    {
        var entity = await _repository.GetAsync(id);

        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.Id != id && x.BankName.ToLower() == input.BankName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Cheque print template for bank '{input.BankName}' already exists.");
        }

        entity.SetBankName(input.BankName.Trim());
        entity.ChequeSize = input.ChequeSize;
        entity.StartingPositionFromTopEdge = input.StartingPositionFromTopEdge;
        entity.ChequeWidth = input.ChequeWidth;
        entity.ChequeHeight = input.ChequeHeight;
        entity.ScannedCheque = input.ScannedCheque?.Trim();
        entity.IsAccountPayable = input.IsAccountPayable;
        entity.AccPayDistFromTopEdge = input.AccPayDistFromTopEdge;
        entity.AccPayDistFromLeftEdge = input.AccPayDistFromLeftEdge;
        entity.MessageToShow = input.MessageToShow?.Trim();
        entity.DateDistFromTopEdge = input.DateDistFromTopEdge;
        entity.DateDistFromLeftEdge = input.DateDistFromLeftEdge;
        entity.PayerNameFromTopEdge = input.PayerNameFromTopEdge;
        entity.PayerNameFromLeftEdge = input.PayerNameFromLeftEdge;
        entity.AmtInWordsFromTopEdge = input.AmtInWordsFromTopEdge;
        entity.AmtInWordsFromLeftEdge = input.AmtInWordsFromLeftEdge;
        entity.AmtInWordWidth = input.AmtInWordWidth;
        entity.AmtInWordsLineSpacing = input.AmtInWordsLineSpacing;
        entity.AmtInFiguresFromTopEdge = input.AmtInFiguresFromTopEdge;
        entity.AmtInFiguresFromLeftEdge = input.AmtInFiguresFromLeftEdge;
        entity.AccNoDistFromTopEdge = input.AccNoDistFromTopEdge;
        entity.AccNoDistFromLeftEdge = input.AccNoDistFromLeftEdge;
        entity.SignatoryFromTopEdge = input.SignatoryFromTopEdge;
        entity.SignatoryFromLeftEdge = input.SignatoryFromLeftEdge;
        entity.HasPrintFormat = input.HasPrintFormat;

        await _repository.UpdateAsync(entity);
        return new ChequePrintTemplateMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.ChequePrintTemplates.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<ChequePrintPreviewDto> GeneratePreviewAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var html = entity.GenerateHtmlTemplate();
        return new ChequePrintPreviewDto { HtmlContent = html };
    }
}
