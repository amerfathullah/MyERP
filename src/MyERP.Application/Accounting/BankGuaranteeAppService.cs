using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.BankGuarantees.Default)]
public class BankGuaranteeAppService : MyERPAppService, IBankGuaranteeAppService
{
    private readonly IRepository<BankGuarantee, Guid> _repository;

    public BankGuaranteeAppService(IRepository<BankGuarantee, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<BankGuaranteeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new BankGuaranteeMapper().Map(entity);
    }

    public async Task<PagedResultDto<BankGuaranteeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );

        return new PagedResultDto<BankGuaranteeDto>(
            totalCount,
            entities.Select(e => new BankGuaranteeMapper().Map(e)).ToList()
        );
    }

    [Authorize(MyERPPermissions.BankGuarantees.Create)]
    public async Task<BankGuaranteeDto> CreateAsync(CreateUpdateBankGuaranteeDto input)
    {
        var entity = new BankGuarantee(
            GuidGenerator.Create(),
            input.CompanyId,
            input.BgType,
            input.Amount,
            input.StartDate,
            input.ValidityDays,
            input.CustomerId,
            input.SupplierId,
            CurrentTenant.Id)
        {
            ReferenceDocType = input.ReferenceDocType,
            ReferenceDocId = input.ReferenceDocId,
            ReferenceDocName = input.ReferenceDocName,
            CustomerName = input.CustomerName,
            SupplierName = input.SupplierName,
            ProjectId = input.ProjectId,
            ProjectName = input.ProjectName,
            Bank = input.Bank,
            BankAccountId = input.BankAccountId,
            BankAccountNumber = input.BankAccountNumber,
            Account = input.Account,
            Iban = input.Iban,
            BranchCode = input.BranchCode,
            SwiftNumber = input.SwiftNumber,
            BankGuaranteeNumber = input.BankGuaranteeNumber,
            NameOfBeneficiary = input.NameOfBeneficiary,
            MarginMoney = input.MarginMoney,
            Charges = input.Charges,
            FixedDepositNumber = input.FixedDepositNumber,
            ClausesAndConditions = input.ClausesAndConditions
        };

        if (input.Amount <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Amount");
        }

        entity.RecalculateEndDate();
        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "BankGuarantee", entity.Id,
            "Created", entity.CompanyId,
            entity.BankGuaranteeNumber ?? entity.Id.ToString()[..8], "Draft", "Draft",
            CurrentUser.Id,
            $"Bank guarantee '{entity.BankGuaranteeNumber}' created with amount {entity.Amount:C}", CurrentTenant.Id));

        return new BankGuaranteeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BankGuarantees.Edit)]
    public async Task<BankGuaranteeDto> UpdateAsync(Guid id, CreateUpdateBankGuaranteeDto input)
    {
        if (input.Amount <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "Amount");
        }

        var entity = await _repository.GetAsync(id);
        entity.BgType = input.BgType;
        entity.ReferenceDocType = input.ReferenceDocType;
        entity.ReferenceDocId = input.ReferenceDocId;
        entity.ReferenceDocName = input.ReferenceDocName;
        entity.CustomerId = input.CustomerId;
        entity.CustomerName = input.CustomerName;
        entity.SupplierId = input.SupplierId;
        entity.SupplierName = input.SupplierName;
        entity.ProjectId = input.ProjectId;
        entity.ProjectName = input.ProjectName;
        entity.Amount = input.Amount;
        entity.StartDate = input.StartDate;
        entity.ValidityDays = input.ValidityDays;
        entity.Bank = input.Bank;
        entity.BankAccountId = input.BankAccountId;
        entity.BankAccountNumber = input.BankAccountNumber;
        entity.Account = input.Account;
        entity.Iban = input.Iban;
        entity.BranchCode = input.BranchCode;
        entity.SwiftNumber = input.SwiftNumber;
        entity.BankGuaranteeNumber = input.BankGuaranteeNumber;
        entity.NameOfBeneficiary = input.NameOfBeneficiary;
        entity.MarginMoney = input.MarginMoney;
        entity.Charges = input.Charges;
        entity.FixedDepositNumber = input.FixedDepositNumber;
        entity.ClausesAndConditions = input.ClausesAndConditions;

        entity.RecalculateEndDate();
        entity.ValidateParty();

        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "BankGuarantee", entity.Id,
            "Updated", entity.CompanyId,
            entity.BankGuaranteeNumber ?? entity.Id.ToString()[..8], "Draft", "Draft",
            CurrentUser.Id,
            $"Bank guarantee '{entity.BankGuaranteeNumber}' updated", CurrentTenant.Id));

        return new BankGuaranteeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BankGuarantees.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.BankGuarantees.Submit)]
    public async Task<BankGuaranteeDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Submit();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "BankGuarantee", entity.Id,
            "Submitted", entity.CompanyId,
            entity.BankGuaranteeNumber ?? entity.Id.ToString()[..8], "Draft", "Submitted",
            CurrentUser.Id,
            $"Bank guarantee '{entity.BankGuaranteeNumber}' submitted", CurrentTenant.Id));

        return new BankGuaranteeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BankGuarantees.Cancel)]
    public async Task<BankGuaranteeDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "BankGuarantee", entity.Id,
            "Cancelled", entity.CompanyId,
            entity.BankGuaranteeNumber ?? entity.Id.ToString()[..8], "Submitted", "Cancelled",
            CurrentUser.Id,
            $"Bank guarantee '{entity.BankGuaranteeNumber}' cancelled", CurrentTenant.Id));

        return new BankGuaranteeMapper().Map(entity);
    }
}
