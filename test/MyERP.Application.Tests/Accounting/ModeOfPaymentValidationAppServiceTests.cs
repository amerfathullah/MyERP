using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Regression coverage for ERPNext PR #61257:
/// - Default account on ModeOfPayment must match company, be a ledger account, of type Bank/Cash/Receivable, and active.
/// - Disabled ModeOfPayment cannot be used on transactions (PaymentEntry submit/post, POS Profile creation, POS sales).
/// - Disabled ModeOfPayment does NOT block cancellation of existing PaymentEntries.
/// </summary>
public abstract class ModeOfPaymentValidationAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IModeOfPaymentAppService _mopAppService;
    private readonly IPaymentEntryAppService _peAppService;
    private readonly IPosProfileAppService _posProfileAppService;
    private readonly IRepository<Company, Guid> _companyRepo;
    private readonly IRepository<Account, Guid> _accountRepo;

    protected ModeOfPaymentValidationAppServiceTests()
    {
        _mopAppService = GetRequiredService<IModeOfPaymentAppService>();
        _peAppService = GetRequiredService<IPaymentEntryAppService>();
        _posProfileAppService = GetRequiredService<IPosProfileAppService>();
        _companyRepo = GetRequiredService<IRepository<Company, Guid>>();
        _accountRepo = GetRequiredService<IRepository<Account, Guid>>();
    }

    [Fact]
    public async Task ModeOfPayment_ValidAccounts_SaveSuccessfully()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MOP Valid Co"), autoSave: true);
            var bankAcct = await _accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-BANK", "Bank Acc", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    IsGroup = false,
                    IsActive = true
                }, autoSave: true);

            var mop = await _mopAppService.CreateAsync(new CreateUpdateModeOfPaymentDto
            {
                Name = "Bank Transfer Valid",
                Type = "Bank",
                CompanyId = company.Id,
                DefaultAccountId = bankAcct.Id,
                IsActive = true
            });

            mop.ShouldNotBeNull();
            mop.Name.ShouldBe("Bank Transfer Valid");
            mop.DefaultAccountId.ShouldBe(bankAcct.Id);
        });
    }

    [Fact]
    public async Task ModeOfPayment_GroupAccount_ThrowsValidationException()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MOP Group Co"), autoSave: true);
            var groupAcct = await _accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1100-GRP", "Bank Group", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    IsGroup = true,
                    IsActive = true
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _mopAppService.CreateAsync(new CreateUpdateModeOfPaymentDto
                {
                    Name = "Invalid Group MOP",
                    Type = "Bank",
                    CompanyId = company.Id,
                    DefaultAccountId = groupAcct.Id,
                    IsActive = true
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task ModeOfPayment_NonBankCashReceivableAccount_ThrowsValidationException()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MOP Revenue Co"), autoSave: true);
            var revenueAcct = await _accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4100-REV", "Sales Revenue", AccountType.Revenue)
                {
                    AccountSubType = AccountSubType.OperatingRevenue,
                    IsGroup = false,
                    IsActive = true
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _mopAppService.CreateAsync(new CreateUpdateModeOfPaymentDto
                {
                    Name = "Invalid Revenue MOP",
                    Type = "Bank",
                    CompanyId = company.Id,
                    DefaultAccountId = revenueAcct.Id,
                    IsActive = true
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task ModeOfPayment_DisabledAccount_ThrowsValidationException()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MOP Disabled Co"), autoSave: true);
            var disabledAcct = await _accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-DIS", "Disabled Cash", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.CashAccount,
                    IsGroup = false,
                    IsActive = false
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _mopAppService.CreateAsync(new CreateUpdateModeOfPaymentDto
                {
                    Name = "Invalid Disabled Acct MOP",
                    Type = "Cash",
                    CompanyId = company.Id,
                    DefaultAccountId = disabledAcct.Id,
                    IsActive = true
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task ModeOfPayment_WrongCompanyAccount_ThrowsValidationException()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company1 = await _companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MOP Co 1"), autoSave: true);
            var company2 = await _companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MOP Co 2"), autoSave: true);
            var bankAcct = await _accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company1.Id, "1110-CO1", "Co1 Bank", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    IsGroup = false,
                    IsActive = true
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _mopAppService.CreateAsync(new CreateUpdateModeOfPaymentDto
                {
                    Name = "Wrong Company MOP",
                    Type = "Bank",
                    CompanyId = company2.Id,
                    DefaultAccountId = bankAcct.Id,
                    IsActive = true
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task PaymentEntry_DisabledModeOfPayment_ThrowsOnSubmit_AllowsCancel()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MOP PE Co"), autoSave: true);
            await GetRequiredService<IRepository<FiscalYear, Guid>>().InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2025, 1, 1), new DateTime(2027, 12, 31)), autoSave: true);
            await GetRequiredService<IRepository<DocumentSeries, Guid>>().InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PE Series MOP", "PaymentEntry", "PE-MOP-"), autoSave: true);
            var bankAcct = await _accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-PE-B", "PE Bank", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    IsGroup = false,
                    IsActive = true
                }, autoSave: true);
            var cashAcct = await _accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-PE-C", "PE Cash", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.CashAccount,
                    IsGroup = false,
                    IsActive = true
                }, autoSave: true);

            var mop = await _mopAppService.CreateAsync(new CreateUpdateModeOfPaymentDto
            {
                Name = "MOP PE Test",
                Type = "Bank",
                CompanyId = company.Id,
                DefaultAccountId = bankAcct.Id,
                IsActive = true
            });

            // Create PE with active mode
            var pe = await _peAppService.CreateAsync(new CreatePaymentEntryDto
            {
                CompanyId = company.Id,
                PaymentType = PaymentType.InternalTransfer,
                PostingDate = DateTime.UtcNow,
                PaidAmount = 250m,
                PaidFromAccountId = cashAcct.Id,
                PaidToAccountId = bankAcct.Id,
                ModeOfPayment = mop.Name,
            });

            // Disable mode of payment
            await _mopAppService.UpdateAsync(mop.Id, new CreateUpdateModeOfPaymentDto
            {
                Name = mop.Name,
                Type = mop.Type,
                CompanyId = mop.CompanyId,
                DefaultAccountId = mop.DefaultAccountId,
                IsActive = false
            });

            // Submitting payment entry with disabled mode throws
            var ex = await Should.ThrowAsync<BusinessException>(() => _peAppService.SubmitAsync(pe.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Re-enable mode and submit & post
            await _mopAppService.UpdateAsync(mop.Id, new CreateUpdateModeOfPaymentDto
            {
                Name = mop.Name,
                Type = mop.Type,
                CompanyId = mop.CompanyId,
                DefaultAccountId = mop.DefaultAccountId,
                IsActive = true
            });
            await _peAppService.SubmitAsync(pe.Id);
            await _peAppService.PostAsync(pe.Id);

            // Disable mode again
            await _mopAppService.UpdateAsync(mop.Id, new CreateUpdateModeOfPaymentDto
            {
                Name = mop.Name,
                Type = mop.Type,
                CompanyId = mop.CompanyId,
                DefaultAccountId = mop.DefaultAccountId,
                IsActive = false
            });

            // Cancellation must NOT be blocked by disabled mode of payment
            var cancelled = await _peAppService.CancelAsync(pe.Id);
            cancelled.Status.ShouldBe(DocumentStatus.Cancelled.ToString());
        });
    }

    [Fact]
    public async Task PosProfile_DisabledModeOfPayment_ThrowsOnCreate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MOP POS Co"), autoSave: true);
            var bankAcct = await _accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-POS-B", "POS Bank", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    IsGroup = false,
                    IsActive = true
                }, autoSave: true);

            var mop = await _mopAppService.CreateAsync(new CreateUpdateModeOfPaymentDto
            {
                Name = "MOP Retired Mode",
                Type = "Bank",
                CompanyId = company.Id,
                DefaultAccountId = bankAcct.Id,
                IsActive = false
            });

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _posProfileAppService.CreateAsync(new CreateUpdatePosProfileDto
                {
                    CompanyId = company.Id,
                    ProfileName = "Test POS Profile Retired MOP",
                    WarehouseId = Guid.NewGuid(),
                    PaymentMethods = new List<CreateUpdatePosProfilePaymentMethodDto>
                    {
                        new() { ModeOfPaymentId = mop.Id, AccountId = bankAcct.Id, IsDefault = true }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }
}
