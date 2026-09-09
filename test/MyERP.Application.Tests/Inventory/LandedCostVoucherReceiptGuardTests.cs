using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Dtos;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's
/// validate_receipt_documents throws if a Landed Cost Voucher's referenced receipt is unsubmitted
/// or belongs to a different company. MyERP only applied these checks inside
/// GetReceiptItemsAsync — the UI helper used to populate the wizard — but CreateAsync itself, a
/// public endpoint callable directly, accepted any ReceiptId/ReceiptType with no re-validation,
/// so a Draft or cross-company Purchase Receipt could be allocated landed costs directly.
/// </summary>
public abstract class LandedCostVoucherReceiptGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_DraftPurchaseReceipt_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var prRepository = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var accountRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var lcvAppService = GetRequiredService<ILandedCostVoucherAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "LCV Guard Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "LCV Guard Supplier"), autoSave: true);
            var expenseAccount = await accountRepository.InsertAsync(
                new MyERP.Accounting.Entities.Account(Guid.NewGuid(), company.Id, "5900", "Freight Expense", MyERP.Accounting.AccountType.Expense), autoSave: true);

            var itemId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var pr = new PurchaseReceipt(Guid.NewGuid(), company.Id, supplier.Id, warehouseId, "PR-LCV-DRAFT-001", DateTime.Today);
            pr.AddItem(itemId, "Widget", quantity: 10m, unitPrice: 5.00m, taxAmount: 0m);
            await prRepository.InsertAsync(pr, autoSave: true); // deliberately left in Draft, never Submitted

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                lcvAppService.CreateAsync(new CreateLandedCostVoucherDto
                {
                    CompanyId = company.Id,
                    PostingDate = DateTime.Today,
                    Items =
                    [
                        new CreateLandedCostItemDto
                        {
                            ReceiptId = pr.Id, ReceiptType = "PurchaseReceipt", ItemId = itemId,
                            Quantity = 10m, Amount = 50m,
                        }
                    ],
                    Charges =
                    [
                        new CreateLandedCostChargeDto { Description = "Freight", ExpenseAccountId = expenseAccount.Id, Amount = 20m }
                    ]
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_PurchaseReceiptFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var prRepository = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var accountRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var lcvAppService = GetRequiredService<ILandedCostVoucherAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "LCV Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "LCV Guard Other Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "LCV Guard Supplier 2"), autoSave: true);
            var expenseAccount = await accountRepository.InsertAsync(
                new MyERP.Accounting.Entities.Account(Guid.NewGuid(), ownerCompany.Id, "5901", "Freight Expense 2", MyERP.Accounting.AccountType.Expense), autoSave: true);

            var itemId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var pr = new PurchaseReceipt(Guid.NewGuid(), otherCompany.Id, supplier.Id, warehouseId, "PR-LCV-XCO-001", DateTime.Today);
            pr.AddItem(itemId, "Widget", quantity: 10m, unitPrice: 5.00m, taxAmount: 0m);
            pr.Submit();
            await prRepository.InsertAsync(pr, autoSave: true);

            // Attempting to allocate landed cost under ownerCompany against otherCompany's receipt.
            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                lcvAppService.CreateAsync(new CreateLandedCostVoucherDto
                {
                    CompanyId = ownerCompany.Id,
                    PostingDate = DateTime.Today,
                    Items =
                    [
                        new CreateLandedCostItemDto
                        {
                            ReceiptId = pr.Id, ReceiptType = "PurchaseReceipt", ItemId = itemId,
                            Quantity = 10m, Amount = 50m,
                        }
                    ],
                    Charges =
                    [
                        new CreateLandedCostChargeDto { Description = "Freight", ExpenseAccountId = expenseAccount.Id, Amount = 20m }
                    ]
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SubmittedSameCompanyReceipt_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var prRepository = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var accountRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var lcvAppService = GetRequiredService<ILandedCostVoucherAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "LCV Guard Happy Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "LCV Guard Supplier 3"), autoSave: true);
            var expenseAccount = await accountRepository.InsertAsync(
                new MyERP.Accounting.Entities.Account(Guid.NewGuid(), company.Id, "5902", "Freight Expense 3", MyERP.Accounting.AccountType.Expense), autoSave: true);

            var seriesRepository = GetRequiredService<IRepository<Core.Entities.DocumentSeries, Guid>>();
            await seriesRepository.InsertAsync(
                new Core.Entities.DocumentSeries(Guid.NewGuid(), company.Id, "LCV Series", "LCV", "LCV-"), autoSave: true);

            var itemId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var pr = new PurchaseReceipt(Guid.NewGuid(), company.Id, supplier.Id, warehouseId, "PR-LCV-OK-001", DateTime.Today);
            pr.AddItem(itemId, "Widget", quantity: 10m, unitPrice: 5.00m, taxAmount: 0m);
            pr.Submit();
            await prRepository.InsertAsync(pr, autoSave: true);

            var dto = await lcvAppService.CreateAsync(new CreateLandedCostVoucherDto
            {
                CompanyId = company.Id,
                PostingDate = DateTime.Today,
                Items =
                [
                    new CreateLandedCostItemDto
                    {
                        ReceiptId = pr.Id, ReceiptType = "PurchaseReceipt", ItemId = itemId,
                        Quantity = 10m, Amount = 50m,
                    }
                ],
                Charges =
                [
                    new CreateLandedCostChargeDto { Description = "Freight", ExpenseAccountId = expenseAccount.Id, Amount = 20m }
                ]
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
