using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
}
