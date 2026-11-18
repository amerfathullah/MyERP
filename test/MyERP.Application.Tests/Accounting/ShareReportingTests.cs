using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class ShareReportingTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IShareReportingAppService _shareReportingAppService;
    private readonly IShareTransferAppService _shareTransferAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<ShareType, Guid> _shareTypeRepository;
    private readonly IRepository<Shareholder, Guid> _shareholderRepository;

    protected ShareReportingTests()
    {
        _shareReportingAppService = GetRequiredService<IShareReportingAppService>();
        _shareTransferAppService = GetRequiredService<IShareTransferAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        _shareTypeRepository = GetRequiredService<IRepository<ShareType, Guid>>();
        _shareholderRepository = GetRequiredService<IRepository<Shareholder, Guid>>();
    }

    [Fact]
    public async Task ShareBalance_Should_Validate_Mandatory_Date_And_Handle_Empty_Shareholder()
    {
        await Should.ThrowAsync<BusinessException>(async () =>
            await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto()));

        var emptyResult = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 6, 1),
        });
        emptyResult.Rows.ShouldBeEmpty();

        var missingResult = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 6, 1),
            ShareholderId = Guid.NewGuid(),
        });
        missingResult.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task ShareBalance_Should_Compute_Weighted_Average_AsOnDate_Transfers_And_BuyBacks()
    {
        var (company, assetAcc, equityAcc) = await SeedCompanyAndAccountsAsync("Share Co 1");
        var (otherCompany, _, _) = await SeedCompanyAndAccountsAsync("Share Co 2");

        var equityType = await _shareTypeRepository.InsertAsync(
            new ShareType(Guid.NewGuid(), "Ordinary Share"), autoSave: true);
        var prefType = await _shareTypeRepository.InsertAsync(
            new ShareType(Guid.NewGuid(), "Preference Share"), autoSave: true);

        var ironMan = await _shareholderRepository.InsertAsync(
            new Shareholder(Guid.NewGuid(), company.Id, "Iron Man"), autoSave: true);
        var thor = await _shareholderRepository.InsertAsync(
            new Shareholder(Guid.NewGuid(), company.Id, "Thor"), autoSave: true);

        // Issue 100 @ 10 on 2026-06-01
        await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Issue, new DateTime(2026, 6, 1),
            null, ironMan.Id, equityType.Id, 1, 100, 10m, equityAcc.Id, assetAcc.Id);

        // Before issue -> empty
        var beforeIssue = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 5, 1),
            CompanyId = company.Id,
            ShareholderId = ironMan.Id,
        });
        beforeIssue.Rows.ShouldBeEmpty();

        // Issue second tranche 100 @ 20 on 2026-06-10 + 10 Preference @ 50
        await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Issue, new DateTime(2026, 6, 10),
            null, ironMan.Id, equityType.Id, 101, 200, 20m, equityAcc.Id, assetAcc.Id);
        await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Issue, new DateTime(2026, 6, 10),
            null, ironMan.Id, prefType.Id, 1, 10, 50m, equityAcc.Id, assetAcc.Id);

        // As-on 2026-06-05 -> only first issue visible
        var mid = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 6, 5),
            CompanyId = company.Id,
            ShareholderId = ironMan.Id,
        });
        mid.Rows.Count.ShouldBe(1);
        mid.Rows[0].NoOfShares.ShouldBe(100);
        mid.Rows[0].AverageRate.ShouldBe(10m);
        mid.Rows[0].Amount.ShouldBe(1000m);

        // Other company filter returns no rows
        var otherCoReport = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 6, 15),
            CompanyId = otherCompany.Id,
            ShareholderId = ironMan.Id,
        });
        otherCoReport.Rows.ShouldBeEmpty();

        // As-on 2026-06-11 -> Ordinary 200 @ avg 15 = 3000; Preference 10 @ 50 = 500
        var bothIssues = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 6, 11),
            CompanyId = company.Id,
            ShareholderId = ironMan.Id,
        });
        var ordBeforeTransfer = bothIssues.Rows.Single(r => r.ShareTypeId == equityType.Id);
        ordBeforeTransfer.NoOfShares.ShouldBe(200);
        ordBeforeTransfer.AverageRate.ShouldBe(15m);
        ordBeforeTransfer.Amount.ShouldBe(3000m);

        // Transfer 40 shares (1..40) to Thor on 2026-06-12 at rate 15 -> Iron Man keeps 1..40 gone, remaining at original receipt rates (60@10 + 100@20 = 2600)
        await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Transfer, new DateTime(2026, 6, 12),
            ironMan.Id, thor.Id, equityType.Id, 1, 40, 15m, equityAcc.Id, assetAcc.Id);

        var ironManAfterTransfer = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 6, 13),
            CompanyId = company.Id,
            ShareholderId = ironMan.Id,
        });
        var ironManOrd = ironManAfterTransfer.Rows.Single(r => r.ShareTypeId == equityType.Id);
        ironManOrd.NoOfShares.ShouldBe(160);
        ironManOrd.Amount.ShouldBe(2600m);

        var thorAfterTransfer = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 6, 13),
            CompanyId = company.Id,
            ShareholderId = thor.Id,
        });
        thorAfterTransfer.Rows.Single().NoOfShares.ShouldBe(40);
        thorAfterTransfer.Rows.Single().AverageRate.ShouldBe(15m);
        thorAfterTransfer.Rows.Single().Amount.ShouldBe(600m);

        // Buy back 20 shares (41..60) on 2026-06-15 -> Company treasury shareholder holds issued minus buy-backs
        await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Purchase, new DateTime(2026, 6, 15),
            ironMan.Id, null, equityType.Id, 41, 60, 25m, equityAcc.Id, assetAcc.Id);

        var companyHolder = (await _shareholderRepository.GetListAsync(s => s.CompanyId == company.Id && s.IsCompany)).Single();
        var companyBal = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 6, 16),
            CompanyId = company.Id,
            ShareholderId = companyHolder.Id,
        });
        var compOrd = companyBal.Rows.Single(r => r.ShareTypeId == equityType.Id);
        compOrd.NoOfShares.ShouldBe(180); // 200 issued - 20 bought back
        compOrd.Amount.ShouldBe(2800m);  // 80 remaining from first issue @10 (800) + 100 @20 (2000)
        var compPref = companyBal.Rows.Single(r => r.ShareTypeId == prefType.Id);
        compPref.NoOfShares.ShouldBe(10);
        compPref.Amount.ShouldBe(500m);
    }

    [Fact]
    public async Task ShareBalance_Should_Handle_Backdated_Transfer_Independent_Of_Date()
    {
        var (company, assetAcc, equityAcc) = await SeedCompanyAndAccountsAsync("Backdate Share Co");
        var equityType = await _shareTypeRepository.InsertAsync(
            new ShareType(Guid.NewGuid(), "Backdate Eq"), autoSave: true);
        var holder = await _shareholderRepository.InsertAsync(
            new Shareholder(Guid.NewGuid(), company.Id, "Holder One"), autoSave: true);
        var counterparty = await _shareholderRepository.InsertAsync(
            new Shareholder(Guid.NewGuid(), company.Id, "Holder Two"), autoSave: true);

        await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Issue, new DateTime(2026, 6, 10),
            null, holder.Id, equityType.Id, 1, 100, 10m, equityAcc.Id, assetAcc.Id);

        // Submit backdated transfer out dated 2026-06-01
        await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Transfer, new DateTime(2026, 6, 1),
            holder.Id, counterparty.Id, equityType.Id, 1, 40, 15m, equityAcc.Id, assetAcc.Id);

        var report = await _shareReportingAppService.GetShareBalanceAsync(new ShareBalanceRequestDto
        {
            Date = new DateTime(2026, 6, 15),
            CompanyId = company.Id,
            ShareholderId = holder.Id,
        });
        report.Rows.Count.ShouldBe(1);
        report.Rows[0].NoOfShares.ShouldBe(60);
        report.Rows[0].AverageRate.ShouldBe(10m);
        report.Rows[0].Amount.ShouldBe(600m);
    }

    [Fact]
    public async Task ShareLedger_Should_List_Submitted_Transfers_With_Directional_Labels()
    {
        await Should.ThrowAsync<BusinessException>(async () =>
            await _shareReportingAppService.GetShareLedgerAsync(new ShareLedgerRequestDto()));

        var (company, assetAcc, equityAcc) = await SeedCompanyAndAccountsAsync("Ledger Share Co");
        var equityType = await _shareTypeRepository.InsertAsync(
            new ShareType(Guid.NewGuid(), "Ledger Equity"), autoSave: true);
        var alice = await _shareholderRepository.InsertAsync(
            new Shareholder(Guid.NewGuid(), company.Id, "Alice"), autoSave: true);
        var bob = await _shareholderRepository.InsertAsync(
            new Shareholder(Guid.NewGuid(), company.Id, "Bob"), autoSave: true);

        var issue1 = await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Issue, new DateTime(2026, 6, 1),
            null, alice.Id, equityType.Id, 1, 100, 10m, equityAcc.Id, assetAcc.Id);
        var issue2 = await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Issue, new DateTime(2026, 6, 10),
            null, alice.Id, equityType.Id, 101, 150, 12m, equityAcc.Id, assetAcc.Id);
        var transfer = await CreateAndSubmitTransferAsync(company.Id, ShareTransferType.Transfer, new DateTime(2026, 6, 15),
            alice.Id, bob.Id, equityType.Id, 1, 40, 10m, equityAcc.Id, assetAcc.Id);

        // Draft transfer must be ignored
        await _shareTransferAppService.CreateAsync(new CreateUpdateShareTransferDto
        {
            CompanyId = company.Id,
            TransferType = ShareTransferType.Issue,
            Date = new DateTime(2026, 6, 16),
            ToShareholderId = alice.Id,
            ShareTypeId = equityType.Id,
            FromNo = 151,
            ToNo = 200,
            Rate = 10m,
            EquityOrLiabilityAccountId = equityAcc.Id,
            AssetAccountId = assetAcc.Id,
        });

        // Mid-period snapshot: only issue1
        var midLedger = await _shareReportingAppService.GetShareLedgerAsync(new ShareLedgerRequestDto
        {
            Date = new DateTime(2026, 6, 5),
            CompanyId = company.Id,
            ShareholderId = alice.Id,
        });
        midLedger.Rows.Count.ShouldBe(1);
        midLedger.Rows[0].ShareTransferId.ShouldBe(issue1.Id);
        midLedger.Rows[0].TransferType.ShouldBe("Issue");

        // Full snapshot for Alice (seller side of transfer)
        var aliceLedger = await _shareReportingAppService.GetShareLedgerAsync(new ShareLedgerRequestDto
        {
            Date = new DateTime(2026, 6, 30),
            CompanyId = company.Id,
            ShareholderId = alice.Id,
        });
        aliceLedger.Rows.Count.ShouldBe(3);
        aliceLedger.Rows[0].ShareTransferId.ShouldBe(issue1.Id);
        aliceLedger.Rows[0].NoOfShares.ShouldBe(100);
        aliceLedger.Rows[1].ShareTransferId.ShouldBe(issue2.Id);
        aliceLedger.Rows[1].NoOfShares.ShouldBe(50);
        aliceLedger.Rows[2].ShareTransferId.ShouldBe(transfer.Id);
        aliceLedger.Rows[2].TransferType.ShouldBe("Transfer to Bob");
        aliceLedger.Rows[2].NoOfShares.ShouldBe(40);
        aliceLedger.Rows[2].CompanyName.ShouldBe("Ledger Share Co");

        // Buyer perspective for Bob
        var bobLedger = await _shareReportingAppService.GetShareLedgerAsync(new ShareLedgerRequestDto
        {
            Date = new DateTime(2026, 6, 30),
            CompanyId = company.Id,
            ShareholderId = bob.Id,
        });
        bobLedger.Rows.Count.ShouldBe(1);
        bobLedger.Rows[0].TransferType.ShouldBe("Transfer from Alice");
        bobLedger.Rows[0].NoOfShares.ShouldBe(40);
        bobLedger.Rows[0].Amount.ShouldBe(400m);
    }

    private async Task<(Company Company, Account AssetAccount, Account EquityAccount)> SeedCompanyAndAccountsAsync(string name)
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), name), autoSave: true);
        var asset = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1100", "Cash", AccountType.Asset), autoSave: true);
        var equity = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "3100", "Share Capital", AccountType.Equity), autoSave: true);
        return (company, asset, equity);
    }

    private async Task<ShareTransferDto> CreateAndSubmitTransferAsync(
        Guid companyId,
        ShareTransferType type,
        DateTime date,
        Guid? fromShareholderId,
        Guid? toShareholderId,
        Guid shareTypeId,
        int fromNo,
        int toNo,
        decimal rate,
        Guid equityAccountId,
        Guid assetAccountId)
    {
        var created = await _shareTransferAppService.CreateAsync(new CreateUpdateShareTransferDto
        {
            CompanyId = companyId,
            TransferType = type,
            Date = date,
            FromShareholderId = fromShareholderId,
            ToShareholderId = toShareholderId,
            ShareTypeId = shareTypeId,
            FromNo = fromNo,
            ToNo = toNo,
            Rate = rate,
            EquityOrLiabilityAccountId = equityAccountId,
            AssetAccountId = assetAccountId,
        });
        return await _shareTransferAppService.SubmitAsync(created.Id);
    }
}
