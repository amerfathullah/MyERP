using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// CRUD for reusable Journal Entry templates. Validation per general-ledger-full.md
/// "Journal Entry Template — Entity Validation": every row's account must belong to the
/// template's company, and PartyType is only allowed on Receivable/Payable accounts.
/// </summary>
[Authorize(MyERPPermissions.Accounts.Default)]
public class JournalEntryTemplateAppService : ApplicationService, IJournalEntryTemplateAppService
{
    private readonly IRepository<JournalEntryTemplate, Guid> _repository;
    private readonly IRepository<Account, Guid> _accountRepository;

    public JournalEntryTemplateAppService(
        IRepository<JournalEntryTemplate, Guid> repository,
        IRepository<Account, Guid> accountRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
    }

    public async Task<JournalEntryTemplateDto> GetAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync();
        var template = query.First(x => x.Id == id);
        return await ToDtoAsync(template);
    }

    public async Task<PagedResultDto<JournalEntryTemplateDto>> GetListAsync(GetJournalEntryTemplateListDto input)
    {
        var query = await _repository.WithDetailsAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();
        var templates = query
            .OrderBy(x => x.TemplateName)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = new System.Collections.Generic.List<JournalEntryTemplateDto>();
        foreach (var t in templates)
            dtos.Add(await ToDtoAsync(t));

        return new PagedResultDto<JournalEntryTemplateDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<JournalEntryTemplateDto> CreateAsync(CreateUpdateJournalEntryTemplateDto input)
    {
        await ValidateLinesAsync(input);

        var template = new JournalEntryTemplate(GuidGenerator.Create(), input.CompanyId, input.TemplateName, CurrentTenant.Id)
        {
            VoucherType = input.VoucherType,
            IsActive = input.IsActive,
        };
        foreach (var line in input.Lines)
            template.AddLine(line.AccountId, line.IsDebit, line.DefaultAmount, line.PartyType, line.Description);

        await _repository.InsertAsync(template);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "JournalEntryTemplate", template.Id,
            "Created", template.CompanyId,
            template.TemplateName, "Draft", "Active",
            CurrentUser.Id,
            $"Journal entry template '{template.TemplateName}' created", CurrentTenant.Id));

        return await ToDtoAsync(template);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<JournalEntryTemplateDto> UpdateAsync(Guid id, CreateUpdateJournalEntryTemplateDto input)
    {
        await ValidateLinesAsync(input);

        var query = await _repository.WithDetailsAsync();
        var template = query.First(x => x.Id == id);

        template.SetName(input.TemplateName);
        template.VoucherType = input.VoucherType;
        template.IsActive = input.IsActive;
        template.ClearLines();
        foreach (var line in input.Lines)
            template.AddLine(line.AccountId, line.IsDebit, line.DefaultAmount, line.PartyType, line.Description);

        await _repository.UpdateAsync(template);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "JournalEntryTemplate", template.Id,
            "Updated", template.CompanyId,
            template.TemplateName, "Active", "Active",
            CurrentUser.Id,
            $"Journal entry template '{template.TemplateName}' updated", CurrentTenant.Id));

        return await ToDtoAsync(template);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private async Task ValidateLinesAsync(CreateUpdateJournalEntryTemplateDto input)
    {
        if (input.Lines.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.UnbalancedJournalEntry)
                .WithData("reason", "Template must have at least one line.");

        var accountIds = input.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accountQuery = await _accountRepository.GetQueryableAsync();
        var accounts = accountQuery.Where(a => accountIds.Contains(a.Id)).ToList().ToDictionary(a => a.Id);

        foreach (var line in input.Lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account))
                throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                    .WithData("reason", $"Account {line.AccountId} not found.");

            if (account.CompanyId != input.CompanyId)
                throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                    .WithData("reason", $"Account '{account.AccountName}' does not belong to the template's company.");

            if (!string.IsNullOrEmpty(line.PartyType)
                && account.AccountSubType != AccountSubType.AccountsReceivable
                && account.AccountSubType != AccountSubType.AccountsPayable)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PartyNotAllowedOnAccount)
                    .WithData("partyType", line.PartyType)
                    .WithData("account", account.AccountName);
            }
        }
    }

    private async Task<JournalEntryTemplateDto> ToDtoAsync(JournalEntryTemplate template)
    {
        var accountQuery = await _accountRepository.GetQueryableAsync();
        var accountIds = template.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = accountQuery
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionary(a => a.Id, a => new { a.AccountCode, a.AccountName });

        return new JournalEntryTemplateDto
        {
            Id = template.Id,
            CompanyId = template.CompanyId,
            TemplateName = template.TemplateName,
            VoucherType = template.VoucherType,
            IsActive = template.IsActive,
            CreationTime = template.CreationTime,
            Lines = template.Lines.Select(l =>
            {
                accounts.TryGetValue(l.AccountId, out var acct);
                return new JournalEntryTemplateLineDto
                {
                    Id = l.Id,
                    AccountId = l.AccountId,
                    AccountCode = acct?.AccountCode,
                    AccountName = acct?.AccountName,
                    IsDebit = l.IsDebit,
                    DefaultAmount = l.DefaultAmount,
                    PartyType = l.PartyType,
                    Description = l.Description,
                };
            }).ToList(),
        };
    }
}
