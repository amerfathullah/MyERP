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
using MyERP.Purchasing.Entities;
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
}
