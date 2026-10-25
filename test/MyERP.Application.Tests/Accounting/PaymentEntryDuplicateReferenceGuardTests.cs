using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's
/// validate_duplicate_entry rejects a Payment Entry whose References list allocates against the
/// same (reference_doctype, reference_name) pair twice. MyERP's PaymentEntryAppService.CreateAsync
/// inserted every reference row as-is with no such check, letting a caller split a payment's
/// allocation across two rows against the identical invoice - double-allocating a single payment
/// against one invoice's outstanding balance.
/// </summary>
public abstract class PaymentEntryDuplicateReferenceGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_DuplicateReferenceRow_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
            var paymentEntryAppService = GetRequiredService<IPaymentEntryAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PE Dup Ref Guard Co"), autoSave: true);
            var bankAccount = await accountRepository.InsertAsync(
                new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "1121-PEDUP", "Test Bank", AccountType.Asset), autoSave: true);
            var receivableAccount = await accountRepository.InsertAsync(
                new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "1130-PEDUP", "Test Receivable", AccountType.Asset), autoSave: true);

            var invoiceId = Guid.NewGuid();

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                paymentEntryAppService.CreateAsync(new CreatePaymentEntryDto
                {
                    CompanyId = company.Id,
                    PaymentType = PaymentType.Receive,
                    PostingDate = DateTime.UtcNow,
                    PaidAmount = 100m,
                    PaidFromAccountId = receivableAccount.Id,
                    PaidToAccountId = bankAccount.Id,
                    References = new List<PaymentReferenceDto>
                    {
                        new() { ReferenceType = "SalesInvoice", ReferenceId = invoiceId, AllocatedAmount = 50m },
                        new() { ReferenceType = "SalesInvoice", ReferenceId = invoiceId, AllocatedAmount = 50m },
                    }
                }));
        });
    }

    private async Task<(CreatePaymentEntryDto Dto, Guid CompanyId)> BuildBankReceiptAsync(string tag)
    {
        var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), $"PE Bank Ref Co {tag}"), autoSave: true);
        var accountRepository = GetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
        var bank = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"1122-{tag}", "Bank", AccountType.Asset) { AccountSubType = AccountSubType.BankAccount }, autoSave: true);
        var receivable = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"1131-{tag}", "Receivable", AccountType.Asset), autoSave: true);
        await GetRequiredService<IRepository<DocumentSeries, Guid>>().InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"PE Series {tag}", "PaymentEntry", $"PEB{tag}-"), autoSave: true);
        return (new CreatePaymentEntryDto
        {
            CompanyId = company.Id,
            PaymentType = PaymentType.Receive,
            PostingDate = DateTime.UtcNow,
            PaidAmount = 100m,
            PaidFromAccountId = receivable.Id,
            PaidToAccountId = bank.Id,
        }, company.Id);
    }

    [Fact]
    public async Task CreateAsync_BankAccountWithoutReference_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (dto, _) = await BuildBankReceiptAsync("A");
            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => GetRequiredService<IPaymentEntryAppService>().CreateAsync(dto));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task CreateAsync_BankAccountWithReferenceNoAndDate_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (dto, _) = await BuildBankReceiptAsync("B");
            dto.ReferenceNumber = "CHQ-001";
            dto.ReferenceDate = DateTime.UtcNow.Date;
            var created = await GetRequiredService<IPaymentEntryAppService>().CreateAsync(dto);
            created.ReferenceDate.ShouldBe(dto.ReferenceDate);
        });
    }

    [Fact]
    public async Task CreateAsync_DisabledCustomerParty_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "PE Disabled Party Co"), autoSave: true);
            var customer = await GetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>().InsertAsync(
                new MyERP.Sales.Entities.Customer(Guid.NewGuid(), company.Id, "PE Disabled Party Cust") { IsActive = false }, autoSave: true);
            var accountRepository = GetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
            var bank = await accountRepository.InsertAsync(new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "1123-PEDIS", "Bank", AccountType.Asset), autoSave: true);
            var receivable = await accountRepository.InsertAsync(new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "1132-PEDIS", "Receivable", AccountType.Asset), autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                GetRequiredService<IPaymentEntryAppService>().CreateAsync(new CreatePaymentEntryDto
                {
                    CompanyId = company.Id,
                    PaymentType = PaymentType.Receive,
                    PostingDate = DateTime.UtcNow,
                    PaidAmount = 100m,
                    PaidFromAccountId = receivable.Id,
                    PaidToAccountId = bank.Id,
                    PartyType = "Customer",
                    PartyId = customer.Id,
                }));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyDisabled);
        });
    }

    [Fact]
    public async Task CreateAsync_UnregisteredPartyType_ThrowsValidationFailed_PR60804()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "PE Unreg Party Co"), autoSave: true);
            var accountRepository = GetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
            var bank = await accountRepository.InsertAsync(new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "1123-PEUNREG", "Bank", AccountType.Asset), autoSave: true);
            var receivable = await accountRepository.InsertAsync(new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "1132-PEUNREG", "Receivable", AccountType.Asset), autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                GetRequiredService<IPaymentEntryAppService>().CreateAsync(new CreatePaymentEntryDto
                {
                    CompanyId = company.Id,
                    PaymentType = PaymentType.Receive,
                    PostingDate = DateTime.UtcNow,
                    PaidAmount = 100m,
                    PaidFromAccountId = receivable.Id,
                    PaidToAccountId = bank.Id,
                    PartyType = "User",
                    PartyId = Guid.NewGuid(),
                }));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            ex.Data["detail"]!.ToString()!.ShouldContain("User is not a valid Party Type");
        });
    }

    [Fact]
    public async Task CreateAsync_AccountTypeMismatch_ThrowsValidationFailed_PR60804()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "PE Mismatch Co"), autoSave: true);
            var customer = await GetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>().InsertAsync(
                new MyERP.Sales.Entities.Customer(Guid.NewGuid(), company.Id, "PE Cust Parity"), autoSave: true);
            var accountRepository = GetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
            var bank = await accountRepository.InsertAsync(new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "1123-PEMIS", "Bank", AccountType.Asset), autoSave: true);
            // Creditors account (Payable) used for Customer (Receivable)
            var creditors = await accountRepository.InsertAsync(new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "2001-PEMIS", "Creditors", AccountType.Liability)
            {
                AccountSubType = AccountSubType.AccountsPayable
            }, autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                GetRequiredService<IPaymentEntryAppService>().CreateAsync(new CreatePaymentEntryDto
                {
                    CompanyId = company.Id,
                    PaymentType = PaymentType.Receive,
                    PostingDate = DateTime.UtcNow,
                    PaidAmount = 100m,
                    PaidFromAccountId = creditors.Id,
                    PaidToAccountId = bank.Id,
                    PartyType = "Customer",
                    PartyId = customer.Id,
                }));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            ex.Data["detail"]!.ToString()!.ShouldContain("Account Creditors and Party Type Customer have different account types");
        });
    }

    [Fact]
    public async Task CreateAsync_JournalEntry_UnregisteredPartyType_ThrowsValidationFailed_PR60804()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "JE Unreg Party Co"), autoSave: true);
            await GetRequiredService<IRepository<DocumentSeries, Guid>>().InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "JE Series", "JournalEntry", "JE-"), autoSave: true);
            var accountRepository = GetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
            var bank = await accountRepository.InsertAsync(new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "1121-JEUNREG", "Bank", AccountType.Asset), autoSave: true);
            var expense = await accountRepository.InsertAsync(new Accounting.Entities.Account(Guid.NewGuid(), company.Id, "5001-JEUNREG", "Expense", AccountType.Expense), autoSave: true);
            var fiscalYear = await GetRequiredService<IRepository<FiscalYear, Guid>>().InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                GetRequiredService<IJournalEntryAppService>().CreateAsync(new CreateJournalEntryDto
                {
                    CompanyId = company.Id,
                    FiscalYearId = fiscalYear.Id,
                    PostingDate = DateTime.UtcNow,
                    Lines = new List<CreateJournalEntryLineDto>
                    {
                        new() { AccountId = expense.Id, Amount = 100m, IsDebit = true, PartyType = "User", PartyId = Guid.NewGuid() },
                        new() { AccountId = bank.Id, Amount = 100m, IsDebit = false }
                    }
                }));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            ex.Data["detail"]!.ToString()!.ShouldContain("Row 1: User is not a valid Party Type");
        });
    }
}
