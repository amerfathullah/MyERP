using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class AccountAppService :
    CrudAppService<
        Account,
        AccountDto,
        Guid,
        GetAccountListDto,
        CreateUpdateAccountDto>,
    IAccountAppService
{
    private readonly IRepository<StockLedgerEntry, Guid>? _stockLedgerEntryRepository;
    private readonly IRepository<JournalEntry, Guid>? _journalRepository;
    private readonly IRepository<JournalEntryLine, Guid>? _lineRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid>? _pleRepository;
    private readonly IRepository<AccountClosingBalance, Guid>? _closingBalanceRepository;
    private readonly IRepository<AccountingRule, Guid>? _ruleRepository;
    private readonly IRepository<BudgetAccount, Guid>? _budgetAccountRepository;
    private readonly IRepository<BankAccount, Guid>? _bankAccountRepository;
    private readonly IRepository<Company, Guid>? _companyRepository;
    private readonly IRepository<WarehouseAccount, Guid>? _warehouseAccountRepository;
    private readonly IRepository<AssetCategoryAccount, Guid>? _assetCategoryAccountRepository;
    private readonly AccountBalanceService? _accountBalanceService;

    private IRepository<StockLedgerEntry, Guid>? StockLedgerEntryRepository =>
        _stockLedgerEntryRepository ?? LazyServiceProvider?.LazyGetService<IRepository<StockLedgerEntry, Guid>>();

    private IRepository<JournalEntry, Guid>? JournalRepository =>
        _journalRepository ?? LazyServiceProvider?.LazyGetService<IRepository<JournalEntry, Guid>>();

    private IRepository<JournalEntryLine, Guid>? LineRepository =>
        _lineRepository ?? LazyServiceProvider?.LazyGetService<IRepository<JournalEntryLine, Guid>>();

    private IRepository<PaymentLedgerEntry, Guid>? PleRepository =>
        _pleRepository ?? LazyServiceProvider?.LazyGetService<IRepository<PaymentLedgerEntry, Guid>>();

    private IRepository<AccountClosingBalance, Guid>? ClosingBalanceRepository =>
        _closingBalanceRepository ?? LazyServiceProvider?.LazyGetService<IRepository<AccountClosingBalance, Guid>>();

    private IRepository<AccountingRule, Guid>? RuleRepository =>
        _ruleRepository ?? LazyServiceProvider?.LazyGetService<IRepository<AccountingRule, Guid>>();

    private IRepository<BudgetAccount, Guid>? BudgetAccountRepository =>
        _budgetAccountRepository ?? LazyServiceProvider?.LazyGetService<IRepository<BudgetAccount, Guid>>();

    private IRepository<BankAccount, Guid>? BankAccountRepository =>
        _bankAccountRepository ?? LazyServiceProvider?.LazyGetService<IRepository<BankAccount, Guid>>();

    private IRepository<Company, Guid>? CompanyRepository =>
        _companyRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Company, Guid>>();

    private IRepository<WarehouseAccount, Guid>? WarehouseAccountRepository =>
        _warehouseAccountRepository ?? LazyServiceProvider?.LazyGetService<IRepository<WarehouseAccount, Guid>>();

    private IRepository<AssetCategoryAccount, Guid>? AssetCategoryAccountRepository =>
        _assetCategoryAccountRepository ?? LazyServiceProvider?.LazyGetService<IRepository<AssetCategoryAccount, Guid>>();

    private AccountBalanceService? BalanceService =>
        _accountBalanceService ?? LazyServiceProvider?.LazyGetService<AccountBalanceService>();

    public AccountAppService(
        IRepository<Account, Guid> repository,
        IRepository<JournalEntry, Guid>? journalRepository = null,
        IRepository<JournalEntryLine, Guid>? lineRepository = null,
        IRepository<PaymentLedgerEntry, Guid>? pleRepository = null,
        IRepository<AccountClosingBalance, Guid>? closingBalanceRepository = null,
        IRepository<AccountingRule, Guid>? ruleRepository = null,
        IRepository<BudgetAccount, Guid>? budgetAccountRepository = null,
        IRepository<BankAccount, Guid>? bankAccountRepository = null,
        IRepository<Company, Guid>? companyRepository = null,
        IRepository<WarehouseAccount, Guid>? warehouseAccountRepository = null,
        IRepository<AssetCategoryAccount, Guid>? assetCategoryAccountRepository = null,
        IRepository<StockLedgerEntry, Guid>? stockLedgerEntryRepository = null,
        AccountBalanceService? accountBalanceService = null)
        : base(repository)
    {
        _journalRepository = journalRepository;
        _lineRepository = lineRepository;
        _pleRepository = pleRepository;
        _closingBalanceRepository = closingBalanceRepository;
        _ruleRepository = ruleRepository;
        _budgetAccountRepository = budgetAccountRepository;
        _bankAccountRepository = bankAccountRepository;
        _companyRepository = companyRepository;
        _warehouseAccountRepository = warehouseAccountRepository;
        _assetCategoryAccountRepository = assetCategoryAccountRepository;
        _stockLedgerEntryRepository = stockLedgerEntryRepository;
        _accountBalanceService = accountBalanceService;

        GetPolicyName = MyERPPermissions.Accounts.Default;
        GetListPolicyName = MyERPPermissions.Accounts.Default;
        CreatePolicyName = MyERPPermissions.Accounts.Create;
        UpdatePolicyName = MyERPPermissions.Accounts.Edit;
        DeletePolicyName = MyERPPermissions.Accounts.Delete;
    }

    public override async Task<PagedResultDto<AccountDto>> GetListAsync(GetAccountListDto input)
    {
        var queryable = await Repository.GetQueryableAsync();

        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            queryable = queryable.Where(a => a.CompanyId == input.CompanyId.Value);
        }

        if (input.AccountSubType.HasValue)
        {
            queryable = queryable.Where(a => a.AccountSubType == input.AccountSubType.Value);
        }

        if (input.IsGroup.HasValue)
        {
            queryable = queryable.Where(a => a.IsGroup == input.IsGroup.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            queryable = queryable.Where(a =>
                a.AccountName.Contains(input.Filter)
                || a.AccountCode.Contains(input.Filter));
        }

        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(a => a.AccountCode)
            .ThenBy(a => a.AccountName)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<AccountDto>(
            totalCount,
            items.Select(ObjectMapper.Map<Account, AccountDto>).ToList());
    }

    public override async Task<AccountDto> CreateAsync(CreateUpdateAccountDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Currency))
        {
            var companyRepo = CompanyRepository;
            if (companyRepo != null)
            {
                var company = await companyRepo.FindAsync(input.CompanyId);
                if (company != null)
                {
                    input.Currency = company.CurrencyCode;
                }
            }
        }
        return await base.CreateAsync(input);
    }

    protected override Account MapToEntity(CreateUpdateAccountDto input)
    {
        var entity = new Account(
            GuidGenerator.Create(),
            input.CompanyId,
            input.AccountCode,
            input.AccountName,
            input.AccountType,
            CurrentTenant.Id);
        MapUpdateFields(input, entity);
        return entity;
    }

    protected override void MapToEntity(CreateUpdateAccountDto input, Account entity)
    {
        entity.SetAccountCode(input.AccountCode);
        entity.SetAccountName(input.AccountName);
        MapUpdateFields(input, entity);
    }

    private static void MapUpdateFields(CreateUpdateAccountDto input, Account entity)
    {
        entity.AccountType = input.AccountType;
        entity.AccountSubType = input.AccountSubType;
        entity.ParentAccountId = input.ParentAccountId;
        entity.IsGroup = input.IsGroup;
        entity.Currency = input.Currency;
        entity.Description = input.Description;
        entity.IsFrozen = input.IsFrozen;
        entity.IsActive = input.IsActive;
    }

    /// <summary>
    /// Override UpdateAsync to validate stock account type change.
    /// Per ERPNext PR #57283: cannot change account type FROM 'Stock' when SLE exists
    /// for any warehouse using this account.
    /// </summary>
    public override async Task<AccountDto> UpdateAsync(Guid id, CreateUpdateAccountDto input)
    {
        var entity = await Repository.GetAsync(id);

        // Block changing account sub-type from Stock when stock transactions exist
        // Per ERPNext PR #57283
        if (entity.AccountSubType == AccountSubType.Stock && input.AccountSubType != AccountSubType.Stock)
        {
            var sleRepo = StockLedgerEntryRepository;
            if (sleRepo != null)
            {
                var sleQuery = await sleRepo.GetQueryableAsync();
                var hasSle = sleQuery.Any(sle => sle.CompanyId == entity.CompanyId);

                if (hasSle)
                {
                    throw new BusinessException("MyERP:02028")
                        .WithData("accountName", entity.AccountName);
                }
            }
        }

        return await base.UpdateAsync(id, input);
    }

    /// <summary>
    /// Prevent deletion of accounts with GL entries (Journal Entry lines).
    /// Per DO-NOT: cannot delete accounts that have been used in transactions.
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        var jeRepo = JournalRepository;
        if (jeRepo != null)
        {
            var jeQuery = await jeRepo.GetQueryableAsync();
            var hasGLEntries = jeQuery.Any(je =>
                je.Lines.Any(l => l.AccountId == id));

            if (hasGLEntries)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AccountCannotBeDeleted)
                    .WithData("reason", "Account has been used in Journal Entries. Deactivate instead of deleting.");
            }
        }

        // Check if account is a group (has children)
        var account = await Repository.GetAsync(id);
        if (account.IsGroup)
        {
            var childQuery = await Repository.GetQueryableAsync();
            var hasChildren = childQuery.Any(a => a.ParentAccountId == id);
            if (hasChildren)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AccountCannotBeDeleted)
                    .WithData("reason", "Group account has child accounts. Delete children first.");
            }
        }

        await base.DeleteAsync(id);
    }

    /// <summary>
    /// Returns the hierarchical Chart of Accounts tree for a company (ERPNext PR #58520).
    /// </summary>
    public async Task<System.Collections.Generic.List<AccountTreeNodeDto>> GetTreeAsync(Guid companyId, bool includeDisabled = false)
    {
        var queryable = await Repository.GetQueryableAsync();
        var query = queryable.Where(a => a.CompanyId == companyId);
        if (!includeDisabled)
        {
            query = query.Where(a => a.IsActive);
        }

        var accounts = query
            .OrderBy(a => a.AccountCode)
            .ToList();

        var nodes = accounts.Select(a => new AccountTreeNodeDto
        {
            Id = a.Id,
            CompanyId = a.CompanyId,
            AccountCode = a.AccountCode,
            AccountName = a.AccountName,
            AccountType = a.AccountType,
            AccountSubType = a.AccountSubType,
            ParentAccountId = a.ParentAccountId,
            IsGroup = a.IsGroup,
            Currency = a.Currency,
            IsFrozen = a.IsFrozen,
            IsActive = a.IsActive,
            Balance = 0
        }).ToList();

        var nodeMap = nodes.ToDictionary(n => n.Id);
        var rootNodes = new System.Collections.Generic.List<AccountTreeNodeDto>();

        foreach (var node in nodes)
        {
            if (node.ParentAccountId.HasValue && nodeMap.TryGetValue(node.ParentAccountId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                rootNodes.Add(node);
            }
        }

        return rootNodes;
    }

    /// <summary>
    /// Merges old account into new account per ERPNext accounts/doctype/account/account.py merge_account.
    /// Validates property compatibility (IsGroup, RootType, Company, Currency, AccountSubType),
    /// circular hierarchy prevention with automatic reparenting, and BalanceMustBe constraints.
    /// Reassigns GL/PLE/rule references and removes the merged account.
    /// </summary>
    public virtual async Task<AccountDto> MergeAccountAsync(MergeAccountDto input)
    {
        await CheckPolicyAsync(UpdatePolicyName);

        if (input.OldAccountId == input.NewAccountId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidAccountMerge)
                .WithData("reason", "Cannot merge account into itself.");
        }

        var oldAccount = await Repository.GetAsync(input.OldAccountId);
        var newAccount = await Repository.GetAsync(input.NewAccountId);

        // 1. Validate property compatibility
        ValidateAccountsCanBeMerged(oldAccount, newAccount);

        // 2. Load all accounts for company to resolve tree and reparenting
        var allCompanyAccounts = await Repository.GetListAsync(a => a.CompanyId == oldAccount.CompanyId);
        var parentLookup = allCompanyAccounts.ToDictionary(a => a.Id, a => a.ParentAccountId);

        // 3. Validate balance after merge (BalanceMustBe constraint)
        await ValidateBalanceAfterMergeAsync(oldAccount, newAccount, allCompanyAccounts, parentLookup);

        // 4. Reparent hierarchy
        // If oldAccount is a group and oldAccount is ancestor of newAccount:
        // newAccount is reparented to oldAccount's parent to avoid cycles or orphaning
        if (oldAccount.IsGroup && IsAncestor(oldAccount.Id, newAccount.Id, parentLookup))
        {
            newAccount.SetParent(oldAccount.ParentAccountId);
            await Repository.UpdateAsync(newAccount);
            parentLookup[newAccount.Id] = oldAccount.ParentAccountId;
        }

        // Reparent all remaining children of oldAccount to newAccount
        var childAccounts = allCompanyAccounts.Where(a => a.ParentAccountId == oldAccount.Id && a.Id != newAccount.Id).ToList();
        foreach (var child in childAccounts)
        {
            child.SetParent(newAccount.Id);
            await Repository.UpdateAsync(child);
            parentLookup[child.Id] = newAccount.Id;
        }

        // 5. Reassign references in other tables from oldAccount to newAccount
        await ReassignAccountReferencesAsync(oldAccount.Id, newAccount.Id, oldAccount.CompanyId);

        // 6. Delete oldAccount
        await Repository.DeleteAsync(oldAccount);

        return ObjectMapper.Map<Account, AccountDto>(newAccount);
    }

    private static void ValidateAccountsCanBeMerged(Account oldAccount, Account newAccount)
    {
        if (oldAccount.CompanyId != newAccount.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidAccountMerge)
                .WithData("reason", "Merging is only possible for accounts belonging to the same company.");
        }

        if (oldAccount.IsGroup != newAccount.IsGroup)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidAccountMerge)
                .WithData("reason", "Merging is only possible if Is Group is the same in both records.");
        }

        if (oldAccount.AccountType != newAccount.AccountType)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidAccountMerge)
                .WithData("reason", "Merging is only possible if Root Type (AccountType) is the same in both records.");
        }

        var oldCurrency = (oldAccount.Currency ?? string.Empty).Trim();
        var newCurrency = (newAccount.Currency ?? string.Empty).Trim();
        if (!string.Equals(oldCurrency, newCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidAccountMerge)
                .WithData("reason", "Merging is only possible if Account Currency is the same in both records.");
        }

        if (!oldAccount.IsGroup && oldAccount.AccountSubType != newAccount.AccountSubType)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidAccountMerge)
                .WithData("reason", "Merging is only possible if Account Type (AccountSubType) is the same in both ledger accounts.");
        }
    }

    private async Task ValidateBalanceAfterMergeAsync(
        Account oldAccount,
        Account newAccount,
        List<Account> allAccounts,
        Dictionary<Guid, Guid?> parentLookup)
    {
        if (!newAccount.BalanceMustBe.HasValue)
        {
            return;
        }

        decimal mergedBalance;
        if (IsAncestor(oldAccount.Id, newAccount.Id, parentLookup))
        {
            mergedBalance = await GetSubtreeBalanceAsync(oldAccount.Id, allAccounts);
        }
        else if (IsAncestor(newAccount.Id, oldAccount.Id, parentLookup))
        {
            mergedBalance = await GetSubtreeBalanceAsync(newAccount.Id, allAccounts);
        }
        else
        {
            var oldBalance = await GetSubtreeBalanceAsync(oldAccount.Id, allAccounts);
            var newBalance = await GetSubtreeBalanceAsync(newAccount.Id, allAccounts);
            mergedBalance = oldBalance + newBalance;
        }

        if (mergedBalance > 0 && newAccount.BalanceMustBe == BalanceDirection.Credit)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidAccountMerge)
                .WithData("reason", $"Cannot merge into {newAccount.AccountName}. Its balance after the merge would be in Debit, but its Balance must be is Credit.");
        }

        if (mergedBalance < 0 && newAccount.BalanceMustBe == BalanceDirection.Debit)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidAccountMerge)
                .WithData("reason", $"Cannot merge into {newAccount.AccountName}. Its balance after the merge would be in Credit, but its Balance must be is Debit.");
        }
    }

    private static bool IsAncestor(Guid potentialAncestorId, Guid descendantId, Dictionary<Guid, Guid?> parentLookup)
    {
        var current = descendantId;
        var visited = new HashSet<Guid>();
        while (parentLookup.TryGetValue(current, out var parentId) && parentId.HasValue)
        {
            if (parentId.Value == potentialAncestorId)
            {
                return true;
            }
            if (!visited.Add(current))
            {
                break;
            }
            current = parentId.Value;
        }
        return false;
    }

    private static HashSet<Guid> GetDescendantAndSelfIds(Guid rootId, List<Account> allAccounts)
    {
        var result = new HashSet<Guid> { rootId };
        bool added;
        do
        {
            added = false;
            foreach (var acc in allAccounts)
            {
                if (acc.ParentAccountId.HasValue && result.Contains(acc.ParentAccountId.Value) && !result.Contains(acc.Id))
                {
                    result.Add(acc.Id);
                    added = true;
                }
            }
        } while (added);
        return result;
    }

    private async Task<decimal> GetSubtreeBalanceAsync(Guid accountId, List<Account> allAccounts)
    {
        var acc = allAccounts.FirstOrDefault(a => a.Id == accountId);
        if (acc == null) return 0m;

        var descendantIds = GetDescendantAndSelfIds(accountId, allAccounts);

        if (BalanceService != null)
        {
            decimal total = 0m;
            foreach (var id in descendantIds)
            {
                var targetAcc = allAccounts.FirstOrDefault(a => a.Id == id);
                if (targetAcc != null && !targetAcc.IsGroup)
                {
                    var bal = await BalanceService.GetBalanceOnAsync(targetAcc.CompanyId, id, DateTime.UtcNow);
                    total += bal.Balance;
                }
            }
            return total;
        }

        var lineRepo = LineRepository;
        if (lineRepo != null)
        {
            var lines = await lineRepo.GetListAsync(l => descendantIds.Contains(l.AccountId));
            return lines.Sum(l => l.IsDebit ? l.Amount : -l.Amount);
        }

        return 0m;
    }

    private async Task ReassignAccountReferencesAsync(Guid oldAccountId, Guid newAccountId, Guid companyId)
    {
        // 1. JournalEntryLine
        var lineRepo = LineRepository;
        if (lineRepo != null)
        {
            var lines = await lineRepo.GetListAsync(l => l.AccountId == oldAccountId);
            foreach (var line in lines)
            {
                line.AccountId = newAccountId;
                await lineRepo.UpdateAsync(line);
            }
        }

        // 2. PaymentLedgerEntry
        var pleRepo = PleRepository;
        if (pleRepo != null)
        {
            var ples = await pleRepo.GetListAsync(p => p.AccountId == oldAccountId);
            foreach (var ple in ples)
            {
                ple.AccountId = newAccountId;
                await pleRepo.UpdateAsync(ple);
            }
        }

        // 3. AccountClosingBalance
        var closingBalanceRepo = ClosingBalanceRepository;
        if (closingBalanceRepo != null)
        {
            var oldClosingBalances = await closingBalanceRepo.GetListAsync(cb => cb.AccountId == oldAccountId);
            var newClosingBalances = await closingBalanceRepo.GetListAsync(cb => cb.AccountId == newAccountId);

            foreach (var oldCb in oldClosingBalances)
            {
                var existingNew = newClosingBalances.FirstOrDefault(nb =>
                    nb.CompanyId == oldCb.CompanyId &&
                    nb.CostCenterId == oldCb.CostCenterId &&
                    nb.FinanceBook == oldCb.FinanceBook &&
                    nb.ClosingDate == oldCb.ClosingDate &&
                    nb.Period == oldCb.Period);

                if (existingNew != null)
                {
                    existingNew.Debit += oldCb.Debit;
                    existingNew.Credit += oldCb.Credit;
                    await closingBalanceRepo.UpdateAsync(existingNew);
                    await closingBalanceRepo.DeleteAsync(oldCb);
                }
                else
                {
                    oldCb.AccountId = newAccountId;
                    await closingBalanceRepo.UpdateAsync(oldCb);
                }
            }
        }

        // 4. AccountingRule
        var ruleRepo = RuleRepository;
        if (ruleRepo != null)
        {
            var rules = await ruleRepo.GetListAsync(r => r.FixedAccountId == oldAccountId);
            foreach (var rule in rules)
            {
                rule.FixedAccountId = newAccountId;
                await ruleRepo.UpdateAsync(rule);
            }
        }

        // 5. BudgetAccount
        var budgetRepo = BudgetAccountRepository;
        if (budgetRepo != null)
        {
            var budgetAccounts = await budgetRepo.GetListAsync(ba => ba.AccountId == oldAccountId);
            foreach (var ba in budgetAccounts)
            {
                ba.AccountId = newAccountId;
                await budgetRepo.UpdateAsync(ba);
            }
        }

        // 6. BankAccount
        var bankAccountRepo = BankAccountRepository;
        if (bankAccountRepo != null)
        {
            var bankAccounts = await bankAccountRepo.GetListAsync(ba => ba.AccountId == oldAccountId);
            foreach (var ba in bankAccounts)
            {
                ba.AccountId = newAccountId;
                await bankAccountRepo.UpdateAsync(ba);
            }
        }

        // 7. Company defaults
        var companyRepo = CompanyRepository;
        if (companyRepo != null)
        {
            var company = await companyRepo.FindAsync(companyId);
            if (company != null)
            {
                bool modified = false;
                if (company.DefaultReceivableAccountId == oldAccountId) { company.DefaultReceivableAccountId = newAccountId; modified = true; }
                if (company.DefaultPayableAccountId == oldAccountId) { company.DefaultPayableAccountId = newAccountId; modified = true; }
                if (company.DefaultIncomeAccountId == oldAccountId) { company.DefaultIncomeAccountId = newAccountId; modified = true; }
                if (company.DefaultExpenseAccountId == oldAccountId) { company.DefaultExpenseAccountId = newAccountId; modified = true; }
                if (company.ServiceExpenseAccountId == oldAccountId) { company.ServiceExpenseAccountId = newAccountId; modified = true; }
                if (company.DefaultTaxPayableAccountId == oldAccountId) { company.DefaultTaxPayableAccountId = newAccountId; modified = true; }
                if (company.DefaultBankAccountId == oldAccountId) { company.DefaultBankAccountId = newAccountId; modified = true; }
                if (company.DefaultInventoryAccountId == oldAccountId) { company.DefaultInventoryAccountId = newAccountId; modified = true; }
                if (company.DefaultStockAdjustmentAccountId == oldAccountId) { company.DefaultStockAdjustmentAccountId = newAccountId; modified = true; }
                if (company.DefaultWipAccountId == oldAccountId) { company.DefaultWipAccountId = newAccountId; modified = true; }
                if (company.DepreciationExpenseAccountId == oldAccountId) { company.DepreciationExpenseAccountId = newAccountId; modified = true; }
                if (company.AccumulatedDepreciationAccountId == oldAccountId) { company.AccumulatedDepreciationAccountId = newAccountId; modified = true; }
                if (company.ExchangeGainLossAccountId == oldAccountId) { company.ExchangeGainLossAccountId = newAccountId; modified = true; }
                if (company.ExchangeGainAccountId == oldAccountId) { company.ExchangeGainAccountId = newAccountId; modified = true; }
                if (company.ExchangeLossAccountId == oldAccountId) { company.ExchangeLossAccountId = newAccountId; modified = true; }
                if (company.BankChargesAccountId == oldAccountId) { company.BankChargesAccountId = newAccountId; modified = true; }
                if (company.DisposalAccountId == oldAccountId) { company.DisposalAccountId = newAccountId; modified = true; }
                if (company.DefaultAdvanceReceivedAccountId == oldAccountId) { company.DefaultAdvanceReceivedAccountId = newAccountId; modified = true; }
                if (company.DefaultAdvancePaidAccountId == oldAccountId) { company.DefaultAdvancePaidAccountId = newAccountId; modified = true; }
                if (company.RoundOffForOpeningAccountId == oldAccountId) { company.RoundOffForOpeningAccountId = newAccountId; modified = true; }
                if (company.RoundOffAccountId == oldAccountId) { company.RoundOffAccountId = newAccountId; modified = true; }
                if (company.StockReceivedButNotBilledAccountId == oldAccountId) { company.StockReceivedButNotBilledAccountId = newAccountId; modified = true; }
                if (company.StockDeliveredButNotBilledAccountId == oldAccountId) { company.StockDeliveredButNotBilledAccountId = newAccountId; modified = true; }
                if (company.ExpensesAddedToStockAccountId == oldAccountId) { company.ExpensesAddedToStockAccountId = newAccountId; modified = true; }
                if (company.ExpensesAddedToStockContraAccountId == oldAccountId) { company.ExpensesAddedToStockContraAccountId = newAccountId; modified = true; }

                if (modified)
                {
                    await companyRepo.UpdateAsync(company);
                }
            }
        }

        // 8. WarehouseAccount
        var whRepo = WarehouseAccountRepository;
        if (whRepo != null)
        {
            var whAccounts = await whRepo.GetListAsync(wa =>
                wa.AccountId == oldAccountId ||
                wa.StockReceivedButNotBilledAccountId == oldAccountId ||
                wa.StockDeliveredButNotBilledAccountId == oldAccountId ||
                wa.StockAdjustmentAccountId == oldAccountId);

            foreach (var wa in whAccounts)
            {
                if (wa.AccountId == oldAccountId) wa.AccountId = newAccountId;
                if (wa.StockReceivedButNotBilledAccountId == oldAccountId) wa.StockReceivedButNotBilledAccountId = newAccountId;
                if (wa.StockDeliveredButNotBilledAccountId == oldAccountId) wa.StockDeliveredButNotBilledAccountId = newAccountId;
                if (wa.StockAdjustmentAccountId == oldAccountId) wa.StockAdjustmentAccountId = newAccountId;
                await whRepo.UpdateAsync(wa);
            }
        }

        // 9. AssetCategoryAccount
        var acaRepo = AssetCategoryAccountRepository;
        if (acaRepo != null)
        {
            var acAccounts = await acaRepo.GetListAsync(aca =>
                aca.FixedAssetAccountId == oldAccountId ||
                aca.AccumulatedDepreciationAccountId == oldAccountId ||
                aca.DepreciationExpenseAccountId == oldAccountId ||
                aca.CapitalWorkInProgressAccountId == oldAccountId);

            foreach (var aca in acAccounts)
            {
                if (aca.FixedAssetAccountId == oldAccountId) aca.FixedAssetAccountId = newAccountId;
                if (aca.AccumulatedDepreciationAccountId == oldAccountId) aca.AccumulatedDepreciationAccountId = newAccountId;
                if (aca.DepreciationExpenseAccountId == oldAccountId) aca.DepreciationExpenseAccountId = newAccountId;
                if (aca.CapitalWorkInProgressAccountId == oldAccountId) aca.CapitalWorkInProgressAccountId = newAccountId;
                await acaRepo.UpdateAsync(aca);
            }
        }
    }
}
