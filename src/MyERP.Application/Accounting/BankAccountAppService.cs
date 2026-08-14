using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class BankAccountAppService : ApplicationService, IBankAccountAppService
{
    private readonly IRepository<BankAccount, Guid> _bankAccountRepo;

    public BankAccountAppService(IRepository<BankAccount, Guid> bankAccountRepo)
    {
        _bankAccountRepo = bankAccountRepo;
    }

    public async Task<PagedResultDto<BankAccountDto>> GetListAsync(GetBankAccountListDto input)
    {
        var query = await _bankAccountRepo.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(b => b.CompanyId == input.CompanyId.Value);
        if (input.IsCompanyAccount.HasValue)
            query = query.Where(b => b.IsCompanyAccount == input.IsCompanyAccount.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(b => b.AccountName.Contains(f) || b.BankName.Contains(f)
                || (b.BankAccountNo != null && b.BankAccountNo.Contains(f)));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(b => b.BankName).ThenBy(b => b.AccountName)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<BankAccountDto>(totalCount,
            items.Select(ObjectMapper.Map<BankAccount, BankAccountDto>).ToList());
    }

    public async Task<BankAccountDto> GetAsync(Guid id)
    {
        var ba = await _bankAccountRepo.GetAsync(id);
        return ObjectMapper.Map<BankAccount, BankAccountDto>(ba);
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<BankAccountDto> CreateAsync(CreateUpdateBankAccountDto input)
    {
        // Validate 1:1 GL account mapping
        var existing = (await _bankAccountRepo.GetQueryableAsync())
            .FirstOrDefault(b => b.AccountId == input.AccountId && b.CompanyId == input.CompanyId);
        if (existing != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("message", $"GL Account is already linked to bank account '{existing.AccountName}'.");
        }

        var ba = new BankAccount(GuidGenerator.Create(), input.CompanyId,
            input.AccountName, input.AccountId, input.BankName, CurrentTenant.Id)
        {
            BankAccountNo = input.BankAccountNo,
            Iban = input.Iban,
            SwiftCode = input.SwiftCode,
            BranchCode = input.BranchCode,
            IsCompanyAccount = input.IsCompanyAccount,
            PartyType = input.PartyType,
            PartyId = input.PartyId,
            CurrencyCode = input.CurrencyCode,
            IsCreditCard = input.IsCreditCard,
        };

        await _bankAccountRepo.InsertAsync(ba);
        return ObjectMapper.Map<BankAccount, BankAccountDto>(ba);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<BankAccountDto> UpdateAsync(Guid id, CreateUpdateBankAccountDto input)
    {
        var ba = await _bankAccountRepo.GetAsync(id);

        // Validate 1:1 if account changed
        if (ba.AccountId != input.AccountId)
        {
            var existing = (await _bankAccountRepo.GetQueryableAsync())
                .FirstOrDefault(b => b.AccountId == input.AccountId && b.CompanyId == input.CompanyId && b.Id != id);
            if (existing != null)
            {
                throw new BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                    .WithData("message", $"GL Account is already linked to bank account '{existing.AccountName}'.");
            }
            ba.AccountId = input.AccountId;
        }

        ba.UpdateDetails(input.AccountName, input.BankName, input.BankAccountNo,
            input.Iban, input.SwiftCode, input.BranchCode);
        ba.IsCompanyAccount = input.IsCompanyAccount;
        ba.PartyType = input.PartyType;
        ba.PartyId = input.PartyId;
        ba.CurrencyCode = input.CurrencyCode;
        ba.IsCreditCard = input.IsCreditCard;

        await _bankAccountRepo.UpdateAsync(ba);
        return ObjectMapper.Map<BankAccount, BankAccountDto>(ba);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<BankAccountDto> SetAsDefaultAsync(Guid id)
    {
        var ba = await _bankAccountRepo.GetAsync(id);

        // Clear previous defaults for same company
        var previousDefaults = (await _bankAccountRepo.GetQueryableAsync())
            .Where(b => b.CompanyId == ba.CompanyId && b.IsDefault && b.Id != id).ToList();
        foreach (var prev in previousDefaults)
        {
            prev.ClearDefault();
            await _bankAccountRepo.UpdateAsync(prev);
        }

        ba.SetAsDefault();
        await _bankAccountRepo.UpdateAsync(ba);
        return ObjectMapper.Map<BankAccount, BankAccountDto>(ba);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<BankAccountDto> DisableAsync(Guid id)
    {
        var ba = await _bankAccountRepo.GetAsync(id);
        ba.Disable();
        await _bankAccountRepo.UpdateAsync(ba);
        return ObjectMapper.Map<BankAccount, BankAccountDto>(ba);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _bankAccountRepo.DeleteAsync(id);
    }
}
