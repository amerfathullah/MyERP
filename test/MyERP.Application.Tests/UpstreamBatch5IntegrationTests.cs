using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 5 upstream ERPNext parity:
/// 1) CostCenterAllocationAppService validates company, group, and disabled cost centers (674a548db7).
/// 2) CostCenterAllocationAppService rejects duplicate active allocations for same main cost center &amp; valid_from date + accepts decimal percentages summing to 100 (2683d487a6, d0dde24142).
/// 3) PeriodClosingVoucherAppService validates closing account company, group, disabled, and frozen state (b5cdfa2c13).
/// 4) PeriodClosingVoucherAppService requires every earlier period with GL activity to be closed, covering partly closed prior years and empty years in between (044510375a).
/// 5) PeriodClosingVoucherAppService allows closing a period whose P&amp;L nets to zero and still rebuilds AccountClosingBalance (c2c6810199).
/// 6) ProductionPlanAppService &amp; MasterProductionScheduleAppService validate BOM active/item ownership (including sub-assembly BOM rows) and fall back to item default warehouse when requested without BOM/plan warehouse (f820b711cc, d08131b6e8, b8e3c8d8f2).
/// </summary>
public abstract class UpstreamBatch5IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CostCenterAllocationAppService_ValidatesCompanyGroupAndDisabledCostCenters_674a548db7()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var allocAppService = GetRequiredService<ICostCenterAllocationAppService>();

            var company1 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "CCA Valid Co 1"), autoSave: true);
            var company2 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "CCA Valid Co 2"), autoSave: true);

            var validMainCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company1.Id, "Main CC"), autoSave: true);
            var validChildCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company1.Id, "Child CC"), autoSave: true);
            var otherCoCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company2.Id, "Other Co CC"), autoSave: true);
            var groupCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company1.Id, "Group CC") { IsGroup = true }, autoSave: true);
            var disabledCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company1.Id, "Disabled CC") { IsActive = false }, autoSave: true);

            // 1) Main CC from another company -> CompanyMismatch
            var exOtherCoMain = await Should.ThrowAsync<BusinessException>(() =>
                allocAppService.CreateAsync(new CreateCostCenterAllocationDto
                {
                    CompanyId = company1.Id,
                    MainCostCenterId = otherCoCc.Id,
                    ValidFrom = new DateTime(2026, 1, 1),
                    Entries = new List<CreateCostCenterAllocationEntryDto>
                    {
                        new() { ChildCostCenterId = validChildCc.Id, Percentage = 100m }
                    }
                }));
            exOtherCoMain.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);

            // 2) Child CC from another company -> CompanyMismatch
            var exOtherCoChild = await Should.ThrowAsync<BusinessException>(() =>
                allocAppService.CreateAsync(new CreateCostCenterAllocationDto
                {
                    CompanyId = company1.Id,
                    MainCostCenterId = validMainCc.Id,
                    ValidFrom = new DateTime(2026, 1, 1),
                    Entries = new List<CreateCostCenterAllocationEntryDto>
                    {
                        new() { ChildCostCenterId = otherCoCc.Id, Percentage = 100m }
                    }
                }));
            exOtherCoChild.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);

            // 3) Group CC -> ValidationFailed
            var exGroup = await Should.ThrowAsync<BusinessException>(() =>
                allocAppService.CreateAsync(new CreateCostCenterAllocationDto
                {
                    CompanyId = company1.Id,
                    MainCostCenterId = groupCc.Id,
                    ValidFrom = new DateTime(2026, 1, 1),
                    Entries = new List<CreateCostCenterAllocationEntryDto>
                    {
                        new() { ChildCostCenterId = validChildCc.Id, Percentage = 100m }
                    }
                }));
            exGroup.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (exGroup.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("group cost center");

            // 4) Disabled CC -> ValidationFailed
            var exDisabled = await Should.ThrowAsync<BusinessException>(() =>
                allocAppService.CreateAsync(new CreateCostCenterAllocationDto
                {
                    CompanyId = company1.Id,
                    MainCostCenterId = validMainCc.Id,
                    ValidFrom = new DateTime(2026, 1, 1),
                    Entries = new List<CreateCostCenterAllocationEntryDto>
                    {
                        new() { ChildCostCenterId = disabledCc.Id, Percentage = 100m }
                    }
                }));
            exDisabled.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (exDisabled.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("disabled");
        });
    }

    [Fact]
    public async Task CostCenterAllocationAppService_RejectsDuplicateMainCostCenterAndValidFrom_AndAcceptsDecimalPercentages_2683d487a6_d0dde24142()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var allocAppService = GetRequiredService<ICostCenterAllocationAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "CCA Dup Co"), autoSave: true);
            var mainCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, "Main Dup CC"), autoSave: true);
            var c1 = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, "Child 1"), autoSave: true);
            var c2 = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, "Child 2"), autoSave: true);
            var c3 = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, "Child 3"), autoSave: true);
            var c4 = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, "Child 4"), autoSave: true);

            var validFrom = new DateTime(2026, 3, 1);

            var first = await allocAppService.CreateAsync(new CreateCostCenterAllocationDto
            {
                CompanyId = company.Id,
                MainCostCenterId = mainCc.Id,
                ValidFrom = validFrom,
                Entries = new List<CreateCostCenterAllocationEntryDto>
                {
                    new() { ChildCostCenterId = c1.Id, Percentage = 64.07m },
                    new() { ChildCostCenterId = c2.Id, Percentage = 3.95m },
                    new() { ChildCostCenterId = c3.Id, Percentage = 17.15m },
                    new() { ChildCostCenterId = c4.Id, Percentage = 14.83m },
                }
            });
            first.IsActive.ShouldBeTrue();
            first.Entries.Count.ShouldBe(4);

            // Creating a second active allocation for same MainCostCenterId and ValidFrom must fail
            var dupEx = await Should.ThrowAsync<BusinessException>(() =>
                allocAppService.CreateAsync(new CreateCostCenterAllocationDto
                {
                    CompanyId = company.Id,
                    MainCostCenterId = mainCc.Id,
                    ValidFrom = validFrom,
                    Entries = new List<CreateCostCenterAllocationEntryDto>
                    {
                        new() { ChildCostCenterId = c1.Id, Percentage = 100m }
                    }
                }));
            dupEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Deactivate first allocation -> creating replacement on same date succeeds
            await allocAppService.ToggleActiveAsync(first.Id);
            var second = await allocAppService.CreateAsync(new CreateCostCenterAllocationDto
            {
                CompanyId = company.Id,
                MainCostCenterId = mainCc.Id,
                ValidFrom = validFrom,
                Entries = new List<CreateCostCenterAllocationEntryDto>
                {
                    new() { ChildCostCenterId = c1.Id, Percentage = 100m }
                }
            });
            second.IsActive.ShouldBeTrue();

            // Re-activating first while second is active on same date must fail
            await Should.ThrowAsync<BusinessException>(() => allocAppService.ToggleActiveAsync(first.Id));
        });
    }

    [Fact]
    public async Task PeriodClosingVoucherAppService_ValidatesClosingAccountCompanyGroupDisabledAndFrozen_b5cdfa2c13()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var pcvAppService = GetRequiredService<IPeriodClosingVoucherAppService>();

            var company1 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PCV Acc Guard Co 1"), autoSave: true);
            var company2 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PCV Acc Guard Co 2"), autoSave: true);
            var fy1 = await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company1.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var otherCoAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company2.Id, "3100-OTH", "Other Co Retained Earnings", AccountType.Equity), autoSave: true);
            var groupAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company1.Id, "3100-GRP", "Group Equity", AccountType.Equity) { IsGroup = true }, autoSave: true);
            var disabledAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company1.Id, "3100-DIS", "Disabled Equity", AccountType.Equity) { IsActive = false }, autoSave: true);
            var frozenAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company1.Id, "3100-FRZ", "Frozen Equity", AccountType.Equity) { IsFrozen = true }, autoSave: true);

            // 1) Other company closing account -> CompanyMismatch
            var exCo = await Should.ThrowAsync<BusinessException>(() =>
                pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
                {
                    CompanyId = company1.Id,
                    FiscalYearId = fy1.Id,
                    PostingDate = new DateTime(2026, 12, 31),
                    TransactionDate = new DateTime(2026, 12, 31),
                    ClosingAccountId = otherCoAcc.Id,
                }));
            exCo.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);

            // 2) Group closing account -> ValidationFailed
            var exGroup = await Should.ThrowAsync<BusinessException>(() =>
                pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
                {
                    CompanyId = company1.Id,
                    FiscalYearId = fy1.Id,
                    PostingDate = new DateTime(2026, 12, 31),
                    TransactionDate = new DateTime(2026, 12, 31),
                    ClosingAccountId = groupAcc.Id,
                }));
            exGroup.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (exGroup.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("group account");

            // 3) Disabled closing account -> ValidationFailed
            var exDis = await Should.ThrowAsync<BusinessException>(() =>
                pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
                {
                    CompanyId = company1.Id,
                    FiscalYearId = fy1.Id,
                    PostingDate = new DateTime(2026, 12, 31),
                    TransactionDate = new DateTime(2026, 12, 31),
                    ClosingAccountId = disabledAcc.Id,
                }));
            exDis.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (exDis.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("disabled");

            // 4) Frozen closing account -> ValidationFailed
            var exFrz = await Should.ThrowAsync<BusinessException>(() =>
                pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
                {
                    CompanyId = company1.Id,
                    FiscalYearId = fy1.Id,
                    PostingDate = new DateTime(2026, 12, 31),
                    TransactionDate = new DateTime(2026, 12, 31),
                    ClosingAccountId = frozenAcc.Id,
                }));
            exFrz.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (exFrz.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("frozen");
        });
    }

    [Fact]
    public async Task PeriodClosingVoucherAppService_RequiresEveryEarlierPeriodClosed_PartlyClosedAndEmptyYearInBetween_044510375a()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var jeRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var pcvAppService = GetRequiredService<IPeriodClosingVoucherAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PCV Earlier Periods Co"), autoSave: true);
            var fy2021 = await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2021", new DateTime(2021, 1, 1), new DateTime(2021, 12, 31)), autoSave: true);
            var fy2022 = await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2022", new DateTime(2022, 1, 1), new DateTime(2022, 12, 31)), autoSave: true);
            var fy2023 = await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2023", new DateTime(2023, 1, 1), new DateTime(2023, 12, 31)), autoSave: true);

            var bankAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-PCV", "Bank", AccountType.Asset), autoSave: true);
            var revAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4100-PCV", "Sales", AccountType.Revenue), autoSave: true);
            var retainedAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "3100-PCV", "Retained Earnings", AccountType.Equity), autoSave: true);

            // Post GL activity in Jan 2021 and close up to 2021-02-28 (partly closed FY 2021)
            var jeJan2021 = new JournalEntry(Guid.NewGuid(), company.Id, fy2021.Id, new DateTime(2021, 1, 15));
            jeJan2021.AddLine(bankAcc.Id, 100m, isDebit: true);
            jeJan2021.AddLine(revAcc.Id, 100m, isDebit: false);
            jeJan2021.Post();
            await jeRepo.InsertAsync(jeJan2021, autoSave: true);

            var pcvFeb2021 = await pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
            {
                CompanyId = company.Id,
                FiscalYearId = fy2021.Id,
                PostingDate = new DateTime(2021, 2, 28),
                TransactionDate = new DateTime(2021, 2, 28),
                ClosingAccountId = retainedAcc.Id,
            });
            await pcvAppService.SubmitAsync(pcvFeb2021.Id);

            // Post second GL entry in June 2021 (after Feb 2021 PCV, so FY 2021 is only partly closed)
            var jeJun2021 = new JournalEntry(Guid.NewGuid(), company.Id, fy2021.Id, new DateTime(2021, 6, 15));
            jeJun2021.AddLine(bankAcc.Id, 200m, isDebit: true);
            jeJun2021.AddLine(revAcc.Id, 200m, isDebit: false);
            jeJun2021.Post();
            await jeRepo.InsertAsync(jeJun2021, autoSave: true);

            // 1) Trying to close FY 2022 while FY 2021 is only partly closed must fail
            var pcv2022 = await pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
            {
                CompanyId = company.Id,
                FiscalYearId = fy2022.Id,
                PostingDate = new DateTime(2022, 12, 31),
                TransactionDate = new DateTime(2022, 12, 31),
                ClosingAccountId = retainedAcc.Id,
            });
            var exPartlyClosed = await Should.ThrowAsync<BusinessException>(() => pcvAppService.SubmitAsync(pcv2022.Id));
            exPartlyClosed.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (exPartlyClosed.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Previous Year is not closed");

            // 2) Trying to close FY 2023 (with empty FY 2022 in between) while FY 2021 has unclosed Jun 2021 GL entry must also fail
            var pcv2023 = await pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
            {
                CompanyId = company.Id,
                FiscalYearId = fy2023.Id,
                PostingDate = new DateTime(2023, 12, 31),
                TransactionDate = new DateTime(2023, 12, 31),
                ClosingAccountId = retainedAcc.Id,
            });
            var exEmptyYearBetween = await Should.ThrowAsync<BusinessException>(() => pcvAppService.SubmitAsync(pcv2023.Id));
            exEmptyYearBetween.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // 3) Close the remainder of FY 2021 at 2021-12-31 -> now FY 2023 (with empty FY 2022) can be closed!
            var pcvDec2021 = await pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
            {
                CompanyId = company.Id,
                FiscalYearId = fy2021.Id,
                PostingDate = new DateTime(2021, 12, 31),
                TransactionDate = new DateTime(2021, 12, 31),
                ClosingAccountId = retainedAcc.Id,
            });
            await pcvAppService.SubmitAsync(pcvDec2021.Id);

            var submitted2023 = await pcvAppService.SubmitAsync(pcv2023.Id);
            submitted2023.Status.ShouldBe((int)DocumentStatus.Submitted);
        });
    }

    [Fact]
    public async Task PeriodClosingVoucherAppService_AllowsZeroNetProfitAndLoss_AndRebuildsClosingBalances_c2c6810199()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var jeRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var acbRepo = GetRequiredService<IRepository<AccountClosingBalance, Guid>>();
            var pcvAppService = GetRequiredService<IPeriodClosingVoucherAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PCV Zero Net PL Co"), autoSave: true);
            var fy = await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var bankAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-ZPL", "Bank", AccountType.Asset), autoSave: true);
            var revAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4100-ZPL", "Sales", AccountType.Revenue), autoSave: true);
            var retainedAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "3100-ZPL", "Retained Earnings", AccountType.Equity), autoSave: true);

            // Sale (+100) and Refund (-100) in same period so P&L nets to 0
            var jeSale = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 5, 10));
            jeSale.AddLine(bankAcc.Id, 100m, isDebit: true);
            jeSale.AddLine(revAcc.Id, 100m, isDebit: false);
            jeSale.Post();
            await jeRepo.InsertAsync(jeSale, autoSave: true);

            var jeRefund = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 5, 12));
            jeRefund.AddLine(revAcc.Id, 100m, isDebit: true);
            jeRefund.AddLine(bankAcc.Id, 100m, isDebit: false);
            jeRefund.Post();
            await jeRepo.InsertAsync(jeRefund, autoSave: true);

            var pcv = await pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
            {
                CompanyId = company.Id,
                FiscalYearId = fy.Id,
                PostingDate = new DateTime(2026, 12, 31),
                TransactionDate = new DateTime(2026, 12, 31),
                ClosingAccountId = retainedAcc.Id,
            });

            var submitted = await pcvAppService.SubmitAsync(pcv.Id);
            submitted.Status.ShouldBe((int)DocumentStatus.Submitted);

            // AccountClosingBalance is rebuilt for the period even when P&L nets to zero
            var acbList = await acbRepo.GetListAsync(a => a.CompanyId == company.Id && a.Period == "2026-12");
            acbList.ShouldNotBeEmpty();
        });
    }

    [Fact]
    public async Task ProductionPlanAndMps_ValidateBomOwnershipAndFallbackItemDefaultWarehouse_f820b711cc_d08131b6e8_b8e3c8d8f2()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var ppAppService = GetRequiredService<IProductionPlanAppService>();
            var mpsAppService = GetRequiredService<IMasterProductionScheduleAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Mfg Parity Co"), autoSave: true);
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PP-Series", "PP", "PP-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "MR-Series", "MR", "MR-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "MPS-Series", "MPS", "MPS-"), autoSave: true);

            var defaultRmWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Default RM Stores"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-B5", "Finished Good B5", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "OTH-B5", "Other Item B5", ItemType.Goods), autoSave: true);
            var subAssyItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SA-B5", "Sub Assembly B5", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-B5", "Raw Material B5", ItemType.Goods)
                {
                    DefaultWarehouseId = defaultRmWh.Id
                }, autoSave: true);

            var wrongBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-OTH-B5", otherItem.Id)
            {
                Quantity = 1m,
                IsActive = true
            };
            wrongBom.Items.Add(new BomItem(Guid.NewGuid(), wrongBom.Id, rmItem.Id, rmItem.ItemName, 1m, 10m));
            await bomRepo.InsertAsync(wrongBom, autoSave: true);

            // 1) ProductionPlan CreateAsync rejects BOM that does not produce the planned item (d08131b6e8)
            var exWrongPlannedBom = await Should.ThrowAsync<BusinessException>(() =>
                ppAppService.CreateAsync(new CreateProductionPlanDto
                {
                    CompanyId = company.Id,
                    PostingDate = new DateTime(2026, 6, 1),
                    Items = new List<CreateProductionPlanItemDto>
                    {
                        new()
                        {
                            ItemId = fgItem.Id,
                            ItemName = fgItem.ItemName,
                            BomId = wrongBom.Id,
                            PlannedQty = 2m
                        }
                    }
                }));
            exWrongPlannedBom.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (exWrongPlannedBom.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("does not belong to Item");

            // 2) ProductionPlan CalculateMaterialRequirementsAsync rejects sub-assembly row with wrong SubBomId (d08131b6e8)
            var fgBomWithBadSubBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-BAD-SA", fgItem.Id)
            {
                Quantity = 1m,
                IsActive = true
            };
            fgBomWithBadSubBom.Items.Add(new BomItem(Guid.NewGuid(), fgBomWithBadSubBom.Id, subAssyItem.Id, subAssyItem.ItemName, 1m, 20m)
            {
                SubBomId = wrongBom.Id
            });
            await bomRepo.InsertAsync(fgBomWithBadSubBom, autoSave: true);

            var planBadSubBom = await ppAppService.CreateAsync(new CreateProductionPlanDto
            {
                CompanyId = company.Id,
                PostingDate = new DateTime(2026, 6, 1),
                Items = new List<CreateProductionPlanItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        BomId = fgBomWithBadSubBom.Id,
                        PlannedQty = 1m
                    }
                }
            });

            var exBadSubBom = await Should.ThrowAsync<BusinessException>(() =>
                ppAppService.CalculateMaterialRequirementsAsync(planBadSubBom.Id));
            exBadSubBom.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // 3) Valid FG BOM without SourceWarehouseId and Plan without ForWarehouseId falls back to RM item's DefaultWarehouseId (f820b711cc)
            var validFgBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-VALID-B5", fgItem.Id)
            {
                Quantity = 1m,
                IsActive = true,
                IsDefault = true
            };
            validFgBom.Items.Add(new BomItem(Guid.NewGuid(), validFgBom.Id, rmItem.Id, rmItem.ItemName, 3m, 10m));
            await bomRepo.InsertAsync(validFgBom, autoSave: true);

            var validPlan = await ppAppService.CreateAsync(new CreateProductionPlanDto
            {
                CompanyId = company.Id,
                PostingDate = new DateTime(2026, 6, 1),
                ForWarehouseId = null,
                Items = new List<CreateProductionPlanItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        BomId = validFgBom.Id,
                        PlannedQty = 2m
                    }
                }
            });

            var calculatedPlan = await ppAppService.CalculateMaterialRequirementsAsync(validPlan.Id);
            calculatedPlan.MaterialRequirements.Count.ShouldBe(1);
            calculatedPlan.MaterialRequirements[0].ItemId.ShouldBe(rmItem.Id);
            calculatedPlan.MaterialRequirements[0].WarehouseId.ShouldBe(defaultRmWh.Id);

            await ppAppService.SubmitAsync(validPlan.Id);
            await ppAppService.GenerateMaterialRequestsAsync(validPlan.Id);

            var mrs = await mrRepo.GetListAsync(m => m.CompanyId == company.Id);
            mrs.Count.ShouldBe(1);
            mrs[0].TargetWarehouseId.ShouldBe(defaultRmWh.Id);

            // 4) MasterProductionSchedule rejects BOM that does not make the item on the MPS row (b8e3c8d8f2)
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "MPS Cust"), autoSave: true);
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-MPS-B5", new DateTime(2026, 6, 1))
            {
                DeliveryDate = new DateTime(2026, 6, 20)
            };
            so.AddItem(fgItem.Id, fgItem.ItemName, 5m, 100m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var mps = await mpsAppService.CreateAsync(new CreateMasterProductionScheduleDto
            {
                CompanyId = company.Id,
                PostingDate = new DateTime(2026, 6, 1),
                FromDate = new DateTime(2026, 6, 1),
                ParentWarehouseId = defaultRmWh.Id
            });
            mps = await mpsAppService.ComputeActualDemandAsync(mps.Id);
            mps.Items.Count.ShouldBe(1);

            var mpsRow = mps.Items[0];
            mpsRow.BomId = wrongBom.Id;

            var exMpsWrongBom = await Should.ThrowAsync<BusinessException>(() =>
                mpsAppService.UpdateAsync(mps.Id, new UpdateMasterProductionScheduleDto
                {
                    PostingDate = mps.PostingDate,
                    FromDate = mps.FromDate,
                    ParentWarehouseId = mps.ParentWarehouseId,
                    Items = new List<MasterProductionScheduleItemDto> { mpsRow }
                }));
            exMpsWrongBom.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (exMpsWrongBom.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("does not belong to Item");

            // Assigning valid BOM for fgItem succeeds
            mpsRow.BomId = validFgBom.Id;
            var updatedMps = await mpsAppService.UpdateAsync(mps.Id, new UpdateMasterProductionScheduleDto
            {
                PostingDate = mps.PostingDate,
                FromDate = mps.FromDate,
                ParentWarehouseId = mps.ParentWarehouseId,
                Items = new List<MasterProductionScheduleItemDto> { mpsRow }
            });
            updatedMps.Items[0].BomId.ShouldBe(validFgBom.Id);
        });
    }

    [Fact]
    public async Task UpstreamBatch_Mps_ExcludesClosedSalesOrderRowsAndListsOrderOnce_67493_67495()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mpsAppService = GetRequiredService<IMasterProductionScheduleAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MPS Closed Row Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "MPS", "MPS", "MPS-67493-"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "MPS WH"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "MPS Cust 67493"), autoSave: true);

            var closedItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "MPS-CLS-1", "Closed Item", ItemType.Goods) { Uom = "Nos", MaintainStock = true }, autoSave: true);
            var openItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "MPS-OPN-1", "Open Item", ItemType.Goods) { Uom = "Nos", MaintainStock = true }, autoSave: true);

            // SO with 2 open rows for openItem + 1 closed row for closedItem -> still open overall, listed ONCE
            var mixedSo = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-MIXED-67493", new DateTime(2026, 6, 1))
            {
                DeliveryDate = new DateTime(2026, 6, 20)
            };
            mixedSo.AddItem(closedItem.Id, closedItem.ItemName, 4m, 100m, 0m);
            mixedSo.AddItem(openItem.Id, openItem.ItemName, 3m, 100m, 0m);
            mixedSo.AddItem(openItem.Id, openItem.ItemName, 2m, 100m, 0m);
            var closedRow = mixedSo.Items.First(i => i.ItemId == closedItem.Id);
            mixedSo.Submit();
            mixedSo.CloseItem(closedRow.Id);
            await soRepo.InsertAsync(mixedSo, autoSave: true);

            var mps = await mpsAppService.CreateAsync(new CreateMasterProductionScheduleDto
            {
                CompanyId = company.Id,
                PostingDate = new DateTime(2026, 6, 1),
                FromDate = new DateTime(2026, 6, 1),
                ParentWarehouseId = wh.Id
            });

            // Filtering by closedItem must exclude mixedSo since its closedItem row is closed
            var fetchedForClosedItem = await mpsAppService.FetchSalesOrdersAsync(mps.Id, new FetchSalesOrdersDto { ItemId = closedItem.Id });
            fetchedForClosedItem.SalesOrders.ShouldBeEmpty();

            // Fetching by openItem lists mixedSo once even though it has 2 open rows for openItem
            var fetchedForOpenItem = await mpsAppService.FetchSalesOrdersAsync(mps.Id, new FetchSalesOrdersDto { ItemId = openItem.Id });
            fetchedForOpenItem.SalesOrders.Count.ShouldBe(1);
            fetchedForOpenItem.SalesOrders[0].SalesOrderId.ShouldBe(mixedSo.Id);

            // ComputeActualDemand must include only openItem (3 + 2 = 5) and exclude closedItem
            var computed = await mpsAppService.ComputeActualDemandAsync(mps.Id);
            computed.Items.Count.ShouldBe(1);
            computed.Items[0].ItemId.ShouldBe(openItem.Id);
            computed.Items[0].PlannedQty.ShouldBe(5m);
        });
    }

    [Fact]
    public async Task ItemGroupAppService_TreeIntegrity_RejectsLeafParentCyclesAndChildDeletion_3f9ed2864d_f34b9fde0d()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var itemGroupAppService = GetRequiredService<IItemGroupAppService>();

            var root = await itemGroupAppService.CreateAsync(new CreateItemGroupDto
            {
                Name = "IG Root 3f9e",
                IsGroup = true
            });
            var childGroup = await itemGroupAppService.CreateAsync(new CreateItemGroupDto
            {
                Name = "IG Child Group 3f9e",
                ParentId = root.Id,
                IsGroup = true
            });
            var leaf = await itemGroupAppService.CreateAsync(new CreateItemGroupDto
            {
                Name = "IG Leaf 3f9e",
                ParentId = childGroup.Id,
                IsGroup = false
            });
            leaf.ParentId.ShouldBe(childGroup.Id);

            // Reject inserting under a leaf (3f9ed2864d)
            var insertUnderLeafEx = await Should.ThrowAsync<BusinessException>(() =>
                itemGroupAppService.CreateAsync(new CreateItemGroupDto
                {
                    Name = "IG Under Leaf",
                    ParentId = leaf.Id,
                    IsGroup = false
                }));
            insertUnderLeafEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (insertUnderLeafEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("leaf group");

            // Reject moving under a leaf (3f9ed2864d)
            var moveUnderLeafEx = await Should.ThrowAsync<BusinessException>(() =>
                itemGroupAppService.UpdateAsync(childGroup.Id, new CreateItemGroupDto
                {
                    Name = childGroup.Name,
                    ParentId = leaf.Id,
                    IsGroup = true
                }));
            moveUnderLeafEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Reject moving under self
            var selfParentEx = await Should.ThrowAsync<BusinessException>(() =>
                itemGroupAppService.UpdateAsync(root.Id, new CreateItemGroupDto
                {
                    Name = root.Name,
                    ParentId = root.Id,
                    IsGroup = true
                }));
            selfParentEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Reject moving under own descendant (f34b9fde0d)
            var descendantEx = await Should.ThrowAsync<BusinessException>(() =>
                itemGroupAppService.UpdateAsync(root.Id, new CreateItemGroupDto
                {
                    Name = root.Name,
                    ParentId = childGroup.Id,
                    IsGroup = true
                }));
            descendantEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (descendantEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("descendant");

            // Reject converting group with children to leaf
            var convertToLeafEx = await Should.ThrowAsync<BusinessException>(() =>
                itemGroupAppService.UpdateAsync(childGroup.Id, new CreateItemGroupDto
                {
                    Name = childGroup.Name,
                    ParentId = root.Id,
                    IsGroup = false
                }));
            convertToLeafEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Reject deleting group with children
            var deleteWithChildrenEx = await Should.ThrowAsync<BusinessException>(() =>
                itemGroupAppService.DeleteAsync(childGroup.Id));
            deleteWithChildrenEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Deleting leaf first succeeds, then childGroup can be deleted
            await itemGroupAppService.DeleteAsync(leaf.Id);
            await itemGroupAppService.DeleteAsync(childGroup.Id);
        });
    }

    [Fact]
    public async Task ItemGroup_CopyCompleteDefaultsIntoNewItem_AndPreserveSnapshotOnGroupEdit_e563af5dc1_cb0ea4aa7b()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var accountRepo = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IG Defaults Snapshot Co"), autoSave: true);
            var wh1 = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "IG Default WH 1"), autoSave: true);
            var wh2 = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "IG Default WH 2"), autoSave: true);
            var incomeAcc1 = await accountRepo.InsertAsync(new MyERP.Accounting.Entities.Account(Guid.NewGuid(), company.Id, "4100-IG1", "Sales IG 1", MyERP.Accounting.AccountType.Revenue), autoSave: true);
            var incomeAcc2 = await accountRepo.InsertAsync(new MyERP.Accounting.Entities.Account(Guid.NewGuid(), company.Id, "4100-IG2", "Sales IG 2", MyERP.Accounting.AccountType.Revenue), autoSave: true);
            var expenseAcc = await accountRepo.InsertAsync(new MyERP.Accounting.Entities.Account(Guid.NewGuid(), company.Id, "5100-IG", "COGS IG", MyERP.Accounting.AccountType.Expense), autoSave: true);

            var itemGroupAppService = GetRequiredService<IItemGroupAppService>();
            var itemAppService = GetRequiredService<IItemAppService>();
            var defaultsService = GetRequiredService<MyERP.Inventory.DomainServices.ItemDefaultsResolutionService>();

            var group = await itemGroupAppService.CreateAsync(new CreateItemGroupDto
            {
                Name = "Snapshotted Electronics",
                IsGroup = false,
                DefaultWarehouseId = wh1.Id,
                DefaultIncomeAccountId = incomeAcc1.Id,
                DefaultExpenseAccountId = expenseAcc.Id
            });

            // Creating an item under the group snapshots all group defaults into the item and ItemDefault
            var item1 = await itemAppService.CreateAsync(new CreateUpdateItemDto
            {
                CompanyId = company.Id,
                ItemCode = "ITM-SNAP-001",
                ItemName = "Snapshotted Item 1",
                ItemType = ItemType.Goods,
                ItemGroupId = group.Id,
                Uom = "Unit",
                MaintainStock = true,
                IsActive = true
            });
            item1.ItemGroupId.ShouldBe(group.Id);
            item1.DefaultWarehouseId.ShouldBe(wh1.Id);
            item1.DefaultIncomeAccountId.ShouldBe(incomeAcc1.Id);
            item1.DefaultExpenseAccountId.ShouldBe(expenseAcc.Id);

            // Editing the Item Group defaults later does NOT alter item1's snapshotted defaults, while a new item2 gets the updated defaults
            await itemGroupAppService.UpdateAsync(group.Id, new CreateItemGroupDto
            {
                Name = group.Name,
                IsGroup = false,
                DefaultWarehouseId = wh2.Id,
                DefaultIncomeAccountId = incomeAcc2.Id,
                DefaultExpenseAccountId = expenseAcc.Id
            });

            (await defaultsService.ResolveWarehouseAsync(item1.Id, company.Id)).ShouldBe(wh1.Id);
            (await defaultsService.ResolveIncomeAccountAsync(item1.Id, company.Id)).ShouldBe(incomeAcc1.Id);

            var item2 = await itemAppService.CreateAsync(new CreateUpdateItemDto
            {
                CompanyId = company.Id,
                ItemCode = "ITM-SNAP-002",
                ItemName = "Snapshotted Item 2",
                ItemType = ItemType.Goods,
                ItemGroupId = group.Id,
                Uom = "Unit",
                MaintainStock = true,
                IsActive = true
            });
            item2.DefaultWarehouseId.ShouldBe(wh2.Id);
            item2.DefaultIncomeAccountId.ShouldBe(incomeAcc2.Id);

            // Company resolved defaults endpoint returns company defaults for authorized user (cb0ea4aa7b)
            var resolvedCompanyDefaults = await itemGroupAppService.GetCompanyResolvedDefaultsAsync(company.Id);
            resolvedCompanyDefaults.CompanyId.ShouldBe(company.Id);
        });
    }

    [Fact]
    public async Task MasterProductionSchedule_ClearsSavedDemandWhenAllOrdersClosed_AndRefusesDuplicateScioOrderItem_be9881cba9_a736bb99fc()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MPS Clear & SCIO Dup Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "MPS", "MPS", "MPS-CLR-"), autoSave: true);
            var parentWh = new Warehouse(Guid.NewGuid(), company.Id, "MPS Parent Group WH") { IsGroup = true };
            await whRepo.InsertAsync(parentWh, autoSave: true);
            var leafWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "MPS Leaf WH"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "MPS-CLR-001", "MPS Clearable FG", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);

            var customer = await custRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "MPS Customer"), autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-MPS-CLR", new DateTime(2026, 6, 1))
            {
                DeliveryDate = new DateTime(2026, 6, 15)
            };
            so.AddItem(item.Id, item.ItemName, 8m, 100m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var mpsAppService = GetRequiredService<IMasterProductionScheduleAppService>();
            var mps = await mpsAppService.CreateAsync(new CreateMasterProductionScheduleDto
            {
                CompanyId = company.Id,
                PostingDate = new DateTime(2026, 6, 1),
                FromDate = new DateTime(2026, 6, 1),
                ParentWarehouseId = parentWh.Id
            });

            await mpsAppService.FetchSalesOrdersAsync(mps.Id, new FetchSalesOrdersDto());
            var withDemand = await mpsAppService.ComputeActualDemandAsync(mps.Id);
            withDemand.Items.Count.ShouldBe(1);
            withDemand.Items[0].PlannedQty.ShouldBe(8m);

            // Close the Sales Order item row and re-run ComputeActualDemandAsync -> saved demand rows are cleared (be9881cba9)
            so.CloseItem(so.Items[0].Id);
            await soRepo.UpdateAsync(so, autoSave: true);

            var cleared = await mpsAppService.ComputeActualDemandAsync(mps.Id);
            cleared.Items.ShouldBeEmpty();
            var reloaded = await mpsAppService.GetAsync(mps.Id);
            reloaded.Items.ShouldBeEmpty();

            // SubcontractingInwardOrder refuses two rows linked to the same Sales Order Item row (a736bb99fc)
            var suppRepo = GetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var supplier = new MyERP.Purchasing.Entities.Supplier(Guid.NewGuid(), company.Id, "SCIO Supplier");
            await suppRepo.InsertAsync(supplier, autoSave: true);

            var scioAppService = GetRequiredService<MyERP.Purchasing.ISubcontractingInwardOrderAppService>();
            var dupEx = await Should.ThrowAsync<BusinessException>(() =>
                scioAppService.CreateAsync(new MyERP.Purchasing.CreateSubcontractingInwardOrderDto
                {
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    SalesOrderId = so.Id,
                    OrderDate = new DateTime(2026, 6, 2),
                    Items = new List<MyERP.Purchasing.CreateScioItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 4m, Rate = 50m, WarehouseId = leafWh.Id },
                        new() { ItemId = item.Id, Quantity = 4m, Rate = 50m, WarehouseId = leafWh.Id }
                    }
                }));
            dupEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (dupEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("same Sales Order Item row");
        });
    }

    [Fact]
    public async Task MaterialRequest_UpdateItemRatesFromBuyingPriceList_AndBillableSalesOrdersItemFilter_e65c791473_b099a6aa25()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var itemPriceRepo = GetRequiredService<IRepository<ItemPrice, Guid>>();
            var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Rate & Billable SO Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "MR", "MR", "MR-RATE-"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main WH"), autoSave: true);

            var itemA = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "MR-ITM-A", "MR Item A", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);
            var itemB = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "MR-ITM-B", "MR Item B", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);

            var buyingPl = await priceListRepo.InsertAsync(
                new PriceList(Guid.NewGuid(), "Standard Buying B5", "MYR", isBuying: true, isSelling: false), autoSave: true);
            await itemPriceRepo.InsertAsync(
                new ItemPrice(Guid.NewGuid(), itemA.Id, buyingPl.Id, 42.50m, "Unit", "MYR"), autoSave: true);

            var mrAppService = GetRequiredService<IMaterialRequestAppService>();
            var mr = await mrAppService.CreateAsync(new CreateMaterialRequestDto
            {
                CompanyId = company.Id,
                RequestType = MaterialRequestType.Purchase,
                RequestDate = new DateTime(2026, 6, 10),
                TargetWarehouseId = wh.Id,
                Items = new List<CreateMaterialRequestItemDto>
                {
                    new() { ItemId = itemA.Id, ItemName = itemA.ItemName, Quantity = 4m, Uom = "Unit", WarehouseId = wh.Id },
                    new() { ItemId = itemB.Id, ItemName = itemB.ItemName, Quantity = 2m, Uom = "Unit", WarehouseId = wh.Id }
                }
            });

            var updatedMr = await mrAppService.UpdateItemRatesAsync(mr.Id, buyingPl.Id);
            var rowA = updatedMr.Items.Single(i => i.ItemId == itemA.Id);
            rowA.PriceListRate.ShouldBe(42.50m);
            rowA.Rate.ShouldBe(42.50m);
            rowA.Amount.ShouldBe(170.00m);

            var rowB = updatedMr.Items.Single(i => i.ItemId == itemB.Id);
            rowB.PriceListRate.ShouldBe(0m);
            rowB.Rate.ShouldBe(0m);
            rowB.Amount.ShouldBe(0m);

            // Billable Sales Orders filtered by SalesOrderItem.ItemId (b099a6aa25)
            var customer = await custRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Billable Filter Cust"), autoSave: true);
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-BILL-FLT-01", new DateTime(2026, 6, 10));
            so.AddItem(itemA.Id, itemA.ItemName, 5m, 100m, 0m);
            so.AddItem(itemB.Id, itemB.ItemName, 3m, 80m, 0m);
            so.Submit();
            // Item A is fully billed; Item B is unbilled
            so.Items.Single(i => i.ItemId == itemA.Id).BilledQty = 5m;
            await soRepo.InsertAsync(so, autoSave: true);

            var soAppService = GetRequiredService<ISalesOrderAppService>();
            var byItemA = await soAppService.GetBillableSalesOrdersAsync(customer.Id, company.Id, itemA.Id);
            byItemA.ShouldBeEmpty();

            var byItemB = await soAppService.GetBillableSalesOrdersAsync(customer.Id, company.Id, itemB.Id);
            byItemB.Count.ShouldBe(1);
            byItemB[0].Id.ShouldBe(so.Id);
        });
    }

    [Fact]
    public async Task SubcontractingOrder_CloseAndReopen_UsesPendingReserveNetOfSuppliedQty_AndScioSkipsClosedSalesOrderRows_42258d5660_c3b83d3d20()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var suppRepo = GetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var scoRepo = GetRequiredService<IRepository<MyERP.Purchasing.Entities.SubcontractingOrder, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SCO Reserve & SCIO Closed Row Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCO", "SCO", "SCO-RES-"), autoSave: true);

            var reserveWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "SCO Reserve WH"), autoSave: true);
            var suppWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "SCO Supplier WH"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SCO-FG-01", "SCO Finished Good", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SCO-RM-01", "SCO Raw Material", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);

            var supplier = await suppRepo.InsertAsync(new MyERP.Purchasing.Entities.Supplier(Guid.NewGuid(), company.Id, "SCO Supplier"), autoSave: true);

            var sco = new MyERP.Purchasing.Entities.SubcontractingOrder(Guid.NewGuid(), company.Id, "SCO-RES-0001", new DateTime(2026, 6, 15), supplier.Id)
            {
                SupplierWarehouseId = suppWh.Id
            };
            sco.AddItem(new MyERP.Purchasing.Entities.SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItem.Id, fgItem.ItemName, 10m, 50m));
            // RequiredQty = 10, TransferredQty = 6, ConsumedQty = 2 -> only 4 remains reserved in ReserveWarehouse (6 already transferred out!)
            var suppliedRow = new MyERP.Purchasing.Entities.SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmItem.Id, rmItem.ItemName, 10m)
            {
                ReserveWarehouseId = reserveWh.Id,
                TransferredQty = 6m,
                ConsumedQty = 2m
            };
            sco.AddSuppliedItem(suppliedRow);
            sco.Submit();
            await scoRepo.InsertAsync(sco, autoSave: true);

            var binService = GetRequiredService<MyERP.Inventory.DomainServices.BinService>();
            await binService.UpdateReservedQtyForSubContractAsync(rmItem.Id, reserveWh.Id, 4m);

            var subcontractingAppService = GetRequiredService<MyERP.Purchasing.ISubcontractingAppService>();
            await subcontractingAppService.CloseOrderAsync(sco.Id);

            var binAfterClose = (await binRepo.GetQueryableAsync()).Single(b => b.ItemId == rmItem.Id && b.WarehouseId == reserveWh.Id);
            binAfterClose.ReservedQtyForSubContract.ShouldBe(0m);

            await subcontractingAppService.ReopenOrderAsync(sco.Id);
            var binAfterReopen = (await binRepo.GetQueryableAsync()).Single(b => b.ItemId == rmItem.Id && b.WarehouseId == reserveWh.Id);
            binAfterReopen.ReservedQtyForSubContract.ShouldBe(4m);

            // SCIO MapFromSalesOrderAsync skips closed SO rows and CreateAsync rejects closed SO rows (c3b83d3d20)
            var customer = await custRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "SCIO Cust"), autoSave: true);
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-SCIO-CLOSED-01", new DateTime(2026, 6, 15));
            so.AddItem(fgItem.Id, fgItem.ItemName, 5m, 100m, 0m);
            so.AddItem(rmItem.Id, rmItem.ItemName, 3m, 40m, 0m);
            so.Submit();
            so.CloseItem(so.Items[0].Id);
            await soRepo.InsertAsync(so, autoSave: true);

            var scioAppService = GetRequiredService<MyERP.Purchasing.ISubcontractingInwardOrderAppService>();
            var mappedScio = await scioAppService.MapFromSalesOrderAsync(new MyERP.Purchasing.MapSubcontractingInwardOrderFromSalesOrderDto
            {
                SalesOrderId = so.Id,
                SupplierId = supplier.Id
            });
            mappedScio.Items.Count.ShouldBe(1);
            mappedScio.Items[0].ItemId.ShouldBe(rmItem.Id);

            var closedRowEx = await Should.ThrowAsync<BusinessException>(() =>
                scioAppService.CreateAsync(new MyERP.Purchasing.CreateSubcontractingInwardOrderDto
                {
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    SalesOrderId = so.Id,
                    OrderDate = new DateTime(2026, 6, 16),
                    Items = new List<MyERP.Purchasing.CreateScioItemDto>
                    {
                        new() { ItemId = fgItem.Id, Quantity = 5m, Rate = 100m, WarehouseId = reserveWh.Id }
                    }
                }));
            (closedRowEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("closed Sales Order Item");
        });
    }

    [Fact]
    public async Task ProductionPlan_AndWorkOrder_RefuseClosedSalesOrderRows_AndClosedWoCommitsOnlyProducedQty_bf675b5480_bcb2d15e31_98fc122a1f()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP Closed SO & WO Commit Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "BOM", "BOM", "BOM-PP-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PP", "PP", "PP-CLS-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "WO", "WO", "WO-CLS-"), autoSave: true);

            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "PP Main WH"), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PP-FG-01", "PP Finished Good", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PP-RM-01", "PP Raw Material", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);

            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            // BOM rejects FgBasedOperatingCost combined with WithOperations (98fc122a1f)
            await Should.ThrowAsync<BusinessException>(() =>
                mfgAppService.CreateBomAsync(new CreateBomDto
                {
                    CompanyId = company.Id,
                    ItemId = fgItem.Id,
                    Quantity = 1m,
                    Uom = "Unit",
                    WithOperations = true,
                    FgBasedOperatingCost = true,
                    OperatingCostPerBomQuantity = 15m,
                    Items = new List<CreateBomItemDto>
                    {
                        new() { ItemId = rmItem.Id, ItemName = rmItem.ItemName, Quantity = 1m, Rate = 10m, Uom = "Unit" }
                    }
                }));

            var bom = await mfgAppService.CreateBomAsync(new CreateBomDto
            {
                CompanyId = company.Id,
                ItemId = fgItem.Id,
                Quantity = 1m,
                Uom = "Unit",
                IsDefault = true,
                SourceWarehouseId = wh.Id,
                TargetWarehouseId = wh.Id,
                Items = new List<CreateBomItemDto>
                {
                    new() { ItemId = rmItem.Id, ItemName = rmItem.ItemName, Quantity = 1m, Rate = 10m, Uom = "Unit" }
                }
            });

            var customer = await custRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "PP Customer"), autoSave: true);
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PP-CLS-01", new DateTime(2026, 6, 15));
            so.AddItem(fgItem.Id, fgItem.ItemName, 10m, 100m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);
            var soItemId = so.Items[0].Id;

            // Create a Production Plan with PlannedQty = 10 against soItemId, then close the SO row before submitting PP -> SubmitAsync throws! (bf675b5480)
            var ppAppService = GetRequiredService<IProductionPlanAppService>();
            var draftPlan = await ppAppService.CreateAsync(new CreateProductionPlanDto
            {
                CompanyId = company.Id,
                PostingDate = new DateTime(2026, 6, 15),
                Items = new List<CreateProductionPlanItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        BomId = bom.Id,
                        PlannedQty = 10m,
                        WarehouseId = wh.Id,
                        SalesOrderId = so.Id,
                        SalesOrderItemId = soItemId
                    }
                }
            });

            so.CloseItem(soItemId);
            await soRepo.UpdateAsync(so, autoSave: true);

            var ppSubmitEx = await Should.ThrowAsync<BusinessException>(() => ppAppService.SubmitAsync(draftPlan.Id));
            (ppSubmitEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("is closed, so it cannot be planned");

            // CreateWorkOrdersFromSalesOrderAsync skips the closed SO row (33988eeeee)
            var batchWoFromClosedSo = await mfgAppService.CreateWorkOrdersFromSalesOrderAsync(so.Id);
            batchWoFromClosedSo.CreatedCount.ShouldBe(0);
            batchWoFromClosedSo.SkippedCount.ShouldBe(1);

            // Reopen the SO row, submit the Production Plan, and verify a Closed Work Order with ProducedQuantity=0 commits 0 to OrderedQty (bcb2d15e31)
            so.ReopenItem(soItemId);
            await soRepo.UpdateAsync(so, autoSave: true);

            var submittedPlan = await ppAppService.SubmitAsync(draftPlan.Id);
            var planItemId = submittedPlan.PlannedItems[0].Id;

            var closedWo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-CLOSED-001", fgItem.Id, bom.Id, 10m)
            {
                ProductionPlanId = submittedPlan.Id,
                ProductionPlanItemId = planItemId,
                SalesOrderId = so.Id,
                SalesOrderItemId = soItemId,
                SourceWarehouseId = wh.Id,
                WipWarehouseId = wh.Id,
                FgWarehouseId = wh.Id
            };
            closedWo.Submit();
            closedWo.Close();
            await woRepo.InsertAsync(closedWo, autoSave: true);

            var reloadedPlan = await ppAppService.GetAsync(submittedPlan.Id);
            reloadedPlan.PlannedItems[0].OrderedQty.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task PurchaseInvoice_UnbilledReceiptsDeductReturnReceipts_AndSalesOrderCommissionRateAndClosedDnRowGuards_d82d54cf42_e460e60a30_2a8c73991c_33988eeeee()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var suppRepo = GetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var prRepo = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var spRepo = GetRequiredService<IRepository<SalesPerson, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Return Receipt & SO/DN Guard Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "DeliveryNote", "DeliveryNote", "DN-CLS-"), autoSave: true);

            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main Receipt WH"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PR-RET-ITM", "Receipt Item", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);
            var supplier = await suppRepo.InsertAsync(new MyERP.Purchasing.Entities.Supplier(Guid.NewGuid(), company.Id, "PR Return Supplier"), autoSave: true);

            // Original Purchase Receipt for 10 units
            var origPr = new PurchaseReceipt(Guid.NewGuid(), company.Id, supplier.Id, wh.Id, "PR-ORIG-001", new DateTime(2026, 6, 10));
            origPr.AddItem(item.Id, item.ItemName, 10m, 50m, 0m, "Unit");
            origPr.Submit();
            await prRepo.InsertAsync(origPr, autoSave: true);

            // Submitted Return Purchase Receipt for -3 units against origPr
            var retPr = new PurchaseReceipt(Guid.NewGuid(), company.Id, supplier.Id, wh.Id, "PR-RET-001", new DateTime(2026, 6, 11))
            {
                IsReturn = true,
                ReturnAgainstId = origPr.Id
            };
            retPr.AddItem(item.Id, item.ItemName, -3m, 50m, 0m, "Unit");
            retPr.Submit();
            await prRepo.InsertAsync(retPr, autoSave: true);

            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();
            var unbilledItems = await piAppService.GetUnbilledReceiptItemsAsync(supplier.Id, company.Id);
            unbilledItems.Count.ShouldBe(1);
            unbilledItems[0].Quantity.ShouldBe(7m); // 10 received - 3 returned = 7 unbilled! (d82d54cf42, e460e60a30)

            var unbilledPrItems = await piAppService.GetUnbilledPurchaseReceiptItemsAsync(supplier.Id, company.Id);
            unbilledPrItems.Count.ShouldBe(1);
            unbilledPrItems[0].Quantity.ShouldBe(7m);

            // Sales Order rejects out-of-range CommissionRate (< 0 or > 100) on UpdateSalesTeamAsync (2a8c73991c)
            var item2 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PR-RET-ITM-2", "Open Sibling Item", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);
            var customer = await custRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Guard Customer"), autoSave: true);
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-GUARD-001", new DateTime(2026, 6, 10));
            so.AddItem(item.Id, item.ItemName, 5m, 100m, 0m);
            so.AddItem(item2.Id, item2.ItemName, 3m, 80m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var salesPerson = await spRepo.InsertAsync(new SalesPerson(Guid.NewGuid(), "Guard Sales Person"), autoSave: true);
            var soAppService = GetRequiredService<ISalesOrderAppService>();
            Should.Throw<BusinessException>(() => SalesOrder.ValidateCommissionRateBounds(120m));
            await Should.ThrowAsync<Volo.Abp.AbpException>(() =>
                soAppService.UpdateSalesTeamAsync(so.Id, new List<SalesTeamAllocationInputDto>
                {
                    new() { SalesPersonId = salesPerson.Id, AllocatedPercentage = 100m, CommissionRate = 120m }
                }));

            // DeliveryNoteAppService.CreateAsync rejects delivery against a closed Sales Order item row (33988eeeee)
            var soItemId = so.Items[0].Id;
            so.CloseItem(soItemId);
            await soRepo.UpdateAsync(so, autoSave: true);

            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();
            var closedDnEx = await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    WarehouseId = wh.Id,
                    PostingDate = new DateTime(2026, 6, 12),
                    SalesOrderId = so.Id,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new()
                        {
                            ItemId = item.Id,
                            Description = item.ItemName,
                            Quantity = 2m,
                            UnitPrice = 100m,
                            Uom = "Unit",
                            SalesOrderItemId = soItemId
                        }
                    }
                }));
            (closedDnEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("is closed");
        });
    }

    [Fact]
    public async Task StockEntry_SubmitEnforcesMultiFgRepackManualRate_AndProjectPurchaseCostIncludesDirectNonStockInvoices_4cbe5b28e3_4b368ed5d4()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seRepo = GetRequiredService<IRepository<StockEntry, Guid>>();
            var suppRepo = GetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var projRepo = GetRequiredService<IRepository<MyERP.Projects.Entities.Project, Guid>>();
            var piRepo = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Repack & Project Cost Co"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Repack WH"), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RPK-RM-1", "Repack Raw Material", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);
            var fgItem1 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RPK-FG-1", "Repack FG 1", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);
            var fgItem2 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RPK-FG-2", "Repack FG 2", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);

            // Draft Repack StockEntry with two distinct finished good items where one has SetBasicRateManually = false (4cbe5b28e3, e2ba9cb546)
            var repackSe = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.Repack, new DateTime(2026, 6, 15))
            {
                EntryNumber = "SE-RPK-MULTI-001"
            };
            repackSe.AddItem(rmItem.Id, 10m, sourceWarehouseId: wh.Id, targetWarehouseId: null, valuationRate: 20m, isFinishedItem: false);
            repackSe.AddItem(fgItem1.Id, 5m, sourceWarehouseId: null, targetWarehouseId: wh.Id, valuationRate: 20m, isFinishedItem: false, setBasicRateManually: true);
            repackSe.AddItem(fgItem2.Id, 5m, sourceWarehouseId: null, targetWarehouseId: wh.Id, valuationRate: 20m, isFinishedItem: false, setBasicRateManually: false);
            await seRepo.InsertAsync(repackSe, autoSave: true);

            var seAppService = GetRequiredService<IStockEntryAppService>();
            var repackEx = await Should.ThrowAsync<BusinessException>(() => seAppService.SubmitAsync(repackSe.Id));
            repackEx.Code.ShouldBe(MyERPDomainErrorCodes.RepackMultiFgManualRate);

            // Project stock tracking includes direct non-UpdateStock Purchase Invoice not linked to a Purchase Receipt (4b368ed5d4)
            var project = await projRepo.InsertAsync(
                new MyERP.Projects.Entities.Project(Guid.NewGuid(), company.Id, "PROJ-DIRECT-PI", "Direct Non-Stock PI Project")
                {
                    EstimatedCost = 1000m
                },
                autoSave: true);
            var supplier = await suppRepo.InsertAsync(new MyERP.Purchasing.Entities.Supplier(Guid.NewGuid(), company.Id, "Direct PI Supplier"), autoSave: true);

            var directPi = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PI-DIRECT-001", new DateTime(2026, 6, 15))
            {
                ProjectId = project.Id,
                UpdateStock = false
            };
            directPi.AddItem(rmItem.Id, rmItem.ItemName, 4m, 75m, 0m);
            directPi.Submit();
            await piRepo.InsertAsync(directPi, autoSave: true);

            var projectAppService = GetRequiredService<MyERP.Projects.IProjectAppService>();
            var report = await projectAppService.GetProjectWiseStockTrackingAsync(
                new MyERP.Projects.ProjectWiseStockTrackingRequestDto
                {
                    CompanyId = company.Id,
                    ProjectId = project.Id
                });

            report.Rows.Count.ShouldBe(1);
            report.Rows[0].CostOfPurchasedItems.ShouldBe(300m); // 4 * 75 = 300 included even with UpdateStock = false!
        });
    }

    [Fact]
    public async Task JobCard_ForQuantityRescaleAfterTransfer_KeepsTransferredItemsAndRequiredQtyFloor_25e2944174_c530d6f106()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var jcRepo = GetRequiredService<IRepository<JobCard, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "JC Rescale Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE", "SE", "SE-JCR-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "StockEntry", "StockEntry", "SE-JCR-"), autoSave: true);

            var srcWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "JC Source WH"), autoSave: true);
            var wipWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "JC WIP WH"), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "JC-FG-1", "JC Finished Good", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "JC-RM-1", "JC Raw Material", ItemType.Goods) { Uom = "Kg", MaintainStock = true }, autoSave: true);

            var opId = Guid.NewGuid();
            var bom = await bomRepo.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-JCR-001", fgItem.Id) { Quantity = 1m, IsActive = true },
                autoSave: true);

            // Work Order for qty = 4 requiring 8 Kg of RM
            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-JCR-001", fgItem.Id, bom.Id, 4m)
            {
                SourceWarehouseId = srcWh.Id,
                WipWarehouseId = wipWh.Id,
                FgWarehouseId = wipWh.Id
            };
            var woReqItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, rmItem.ItemName, 8m)
            {
                SourceWarehouseId = srcWh.Id,
                StockUom = "Kg",
                TransferredQuantity = 4m // 4 Kg already transferred for ForQuantity = 2
            };
            wo.RequiredItems.Add(woReqItem);
            wo.Submit();
            await woRepo.InsertAsync(wo, autoSave: true);

            var jc = new JobCard(Guid.NewGuid(), company.Id, wo.Id, opId, forQuantity: 2m, sequenceId: 1)
            {
                WipWarehouseId = wipWh.Id,
                TransferredQty = 2m
            };
            jc.UpdateTransferStatus(true, true);
            await jcRepo.InsertAsync(jc, autoSave: true);

            var jcAppService = GetRequiredService<IJobCardAppService>();

            // 1. Raise ForQuantity from 2 to 4 after transfer (25e2944174, 7a63b772b2)
            var raisedDto = await jcAppService.UpdateAsync(jc.Id, new CreateJobCardDto
            {
                CompanyId = company.Id,
                WorkOrderId = wo.Id,
                OperationId = opId,
                ForQuantity = 4m,
                SequenceId = 1,
                PlannedTimeInMins = 60m
            });
            raisedDto.ForQuantity.ShouldBe(4m);

            var rawMaterialsAfterRaise = await jcAppService.GetRawMaterialsAsync(jc.Id);
            rawMaterialsAfterRaise.Count.ShouldBe(1);
            rawMaterialsAfterRaise[0].RequiredQty.ShouldBe(8m);
            rawMaterialsAfterRaise[0].TransferredQty.ShouldBe(4m);

            // Next Material Transfer transfers the remaining 4 Kg (8 - 4 = 4)
            var secondTransfer = await jcAppService.CreateMaterialTransferAsync(jc.Id);
            secondTransfer.Items.Count.ShouldBe(1);
            secondTransfer.Items[0].Quantity.ShouldBe(4m);

            // 2. Lower ForQuantity to 1 after 4 Kg transferred -> RequiredQty stays floored at TransferredQty = 4 Kg (c530d6f106, c5e001f4dd)
            await jcAppService.UpdateAsync(jc.Id, new CreateJobCardDto
            {
                CompanyId = company.Id,
                WorkOrderId = wo.Id,
                OperationId = opId,
                ForQuantity = 1m,
                SequenceId = 1,
                PlannedTimeInMins = 60m
            });
            var rawMaterialsAfterLower = await jcAppService.GetRawMaterialsAsync(jc.Id);
            rawMaterialsAfterLower.Count.ShouldBe(1);
            rawMaterialsAfterLower[0].RequiredQty.ShouldBe(4m); // max(scaled=2, transferred=4) = 4!
        });
    }

    [Fact]
    public async Task PickList_ExcludesRejectedWarehouseStock_AndResolvesCustomerCurrencyWithoutSalesOrder_784d9cbe09_8fbc02f81b()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PickList Parity Co") { CurrencyCode = "MYR" }, autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "DeliveryNote", "DeliveryNote", "DN-PLP-"), autoSave: true);

            var stdWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "PL Standard WH") { WarehouseType = WarehouseType.Standard }, autoSave: true);
            var rejWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "PL Rejected WH") { WarehouseType = WarehouseType.Rejected }, autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PL-ITM-1", "Pick Item", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);

            // Rejected warehouse has 50 units in bin, standard warehouse has 10 units
            var rejBin = new Bin(Guid.NewGuid(), item.Id, rejWh.Id);
            rejBin.UpdateActualQty(50m, 500m);
            await binRepo.InsertAsync(rejBin, autoSave: true);

            var stdBin = new Bin(Guid.NewGuid(), item.Id, stdWh.Id);
            stdBin.UpdateActualQty(10m, 100m);
            await binRepo.InsertAsync(stdBin, autoSave: true);

            var customer = await custRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "USD Pick Customer") { DefaultCurrency = "USD" }, autoSave: true);

            var plAppService = GetRequiredService<IPickListAppService>();

            // 1. Pick List targeting the Rejected Warehouse allocates 0 units (784d9cbe09, 9f33bb012e, 6bfba876ac, f303513d06)
            var rejPl = await plAppService.CreateAsync(new CreatePickListDto
            {
                CompanyId = company.Id,
                Purpose = "Delivery",
                CustomerId = customer.Id,
                Items = new CreatePickListItemDto[]
                {
                    new() { ItemId = item.Id, WarehouseId = rejWh.Id, Qty = 5m, Uom = "Unit", ConversionFactor = 1m }
                }
            });
            var rejAllocation = await plAppService.AllocateStockAsync(rejPl.Id);
            rejAllocation.HasShortage.ShouldBeTrue();
            rejAllocation.Allocations[0].AllocatedQty.ShouldBe(0m);

            // 2. Pick List without Sales Order creates Delivery Note using Customer's DefaultCurrency ("USD") (8fbc02f81b, e4e0d9c707)
            var stdPl = await plAppService.CreateAsync(new CreatePickListDto
            {
                CompanyId = company.Id,
                Purpose = "Delivery",
                CustomerId = customer.Id,
                Items = new CreatePickListItemDto[]
                {
                    new() { ItemId = item.Id, WarehouseId = stdWh.Id, Qty = 4m, Uom = "Unit", ConversionFactor = 1m }
                }
            });
            await plAppService.SubmitAsync(stdPl.Id);

            var dnId = await plAppService.CreateDeliveryNoteFromPickListAsync(stdPl.Id);
            var dn = await dnRepo.GetAsync(dnId);
            dn.CurrencyCode.ShouldBe("USD");
            dn.Items.Count.ShouldBe(1);
            dn.Items[0].Quantity.ShouldBe(4m);
        });
    }

    [Fact]
    public async Task PurchaseReceipt_ReturnQtyFromRejectedWarehouse_DeductsRejectedBinAndValidatesAgainstOriginalRejectedQty_3837f7b46a_097411fb47_b11c0c4076()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var suppRepo = GetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Rejected Return Co"), autoSave: true);
            await fyRepo.InsertAsync(
                new MyERP.Accounting.Entities.FiscalYear(Guid.NewGuid(), company.Id, "FY-PR-REJ", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PurchaseReceipt", "PurchaseReceipt", "PR-REJ-"), autoSave: true);

            var accWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Accepted WH"), autoSave: true);
            var rejWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Rejected WH") { WarehouseType = WarehouseType.Rejected }, autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PR-REJ-ITM", "Inspectable Item", ItemType.Goods) { Uom = "Unit", MaintainStock = true }, autoSave: true);
            var supplier = await suppRepo.InsertAsync(new MyERP.Purchasing.Entities.Supplier(Guid.NewGuid(), company.Id, "Rejected Return Supplier"), autoSave: true);

            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();

            // 1. Original Purchase Receipt receives 10 accepted into accWh and 4 rejected into rejWh
            var origPr = await prAppService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = accWh.Id,
                PostingDate = DateTime.Today.AddDays(-2),
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = item.ItemName,
                        Quantity = 10m,
                        RejectedQty = 4m,
                        ReceivedQty = 14m,
                        WarehouseId = accWh.Id,
                        RejectedWarehouseId = rejWh.Id,
                        UnitPrice = 0m,
                        Uom = "Unit"
                    }
                }
            });
            await prAppService.SubmitAsync(origPr.Id);

            var rejBinAfterOrig = (await binRepo.GetQueryableAsync()).First(b => b.ItemId == item.Id && b.WarehouseId == rejWh.Id);
            rejBinAfterOrig.ActualQty.ShouldBe(4m);

            // 2. Returning -3 with ReturnQtyFromRejectedWarehouse = true deducts from rejWh (leaving 1 in rejWh and 10 untouched in accWh) (4cc2ffb2a8, b11c0c4076, 3837f7b46a, 097411fb47)
            var retPr = await prAppService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = accWh.Id,
                PostingDate = DateTime.Today.AddDays(-1),
                IsReturn = true,
                ReturnAgainstId = origPr.Id,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = item.ItemName,
                        Quantity = -3m,
                        RejectedQty = 0m,
                        ReceivedQty = -3m,
                        RejectedWarehouseId = rejWh.Id,
                        ReturnQtyFromRejectedWarehouse = true,
                        UnitPrice = 0m,
                        Uom = "Unit"
                    }
                }
            });
            await prAppService.SubmitAsync(retPr.Id);

            var rejBinAfterRet = (await binRepo.GetQueryableAsync()).First(b => b.ItemId == item.Id && b.WarehouseId == rejWh.Id);
            rejBinAfterRet.ActualQty.ShouldBe(1m);
            var accBinAfterRet = (await binRepo.GetQueryableAsync()).First(b => b.ItemId == item.Id && b.WarehouseId == accWh.Id);
            accBinAfterRet.ActualQty.ShouldBe(10m);

            // 3. Attempting to return -2 more from rejected warehouse exceeds remaining rejected qty (4 - 3 = 1) -> throws ReturnQtyExceedsOriginal!
            var excessEx = await Should.ThrowAsync<BusinessException>(() =>
                prAppService.CreateAsync(new CreatePurchaseReceiptDto
                {
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    WarehouseId = accWh.Id,
                    PostingDate = DateTime.Today,
                    IsReturn = true,
                    ReturnAgainstId = origPr.Id,
                    Items = new List<CreatePurchaseReceiptItemDto>
                    {
                        new()
                        {
                            ItemId = item.Id,
                            Description = item.ItemName,
                            Quantity = -2m,
                            RejectedQty = 0m,
                            ReceivedQty = -2m,
                            RejectedWarehouseId = rejWh.Id,
                            ReturnQtyFromRejectedWarehouse = true,
                            UnitPrice = 0m,
                            Uom = "Unit"
                        }
                    }
                }));
            excessEx.Code.ShouldBe(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal);
        });
    }
}
