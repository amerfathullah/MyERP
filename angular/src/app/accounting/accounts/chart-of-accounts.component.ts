import { Component, computed, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountService } from '../../proxy/accounting/account.service';
import { ChartOfAccountsImportService } from '../../proxy/accounting/chart-of-accounts-import.service';
import { CompanyService } from '../../proxy/core/company.service';
import type { AccountDto, CoaTemplateRowDto } from '../../proxy/accounting/models';
import type { CompanyDto } from '../../proxy/core/models';
import { AccountType } from '../../proxy/accounting/account-type.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';

export interface AccountNode {
  id: string;
  accountCode: string;
  accountName: string;
  accountType: AccountType | string;
  isGroup: boolean;
  currency?: string | null;
  isFrozen?: boolean;
  isActive?: boolean;
  balance?: number;
  children?: AccountNode[];
}

const ACCOUNT_TYPE_NAMES: Record<number, string> = {
  [AccountType.Asset]: 'Asset',
  [AccountType.Liability]: 'Liability',
  [AccountType.Equity]: 'Equity',
  [AccountType.Revenue]: 'Revenue',
  [AccountType.Expense]: 'Expense',
};

export function flattenAccountNodes(nodes: AccountNode[]): AccountNode[] {
  const result: AccountNode[] = [];
  const walk = (list: AccountNode[]) => {
    for (const n of list) {
      result.push(n);
      if (n.children?.length) {
        walk(n.children);
      }
    }
  };
  walk(nodes);
  return result;
}

export function parseCoaCsv(csvText: string): CoaTemplateRowDto[] {
  const lines = csvText
    .split(/\r?\n/)
    .map(l => l.trim())
    .filter(l => l.length > 0);
  if (lines.length === 0) return [];

  const rows: CoaTemplateRowDto[] = [];
  for (let i = 0; i < lines.length; i++) {
    const cols = lines[i].split(',').map(c => c.trim());
    if (i === 0 && cols[0].toLowerCase() === 'accountcode') continue;
    if (cols.length < 3 || !cols[0] || !cols[1]) continue;

    const rawType = cols[2].toLowerCase();
    const accountType =
      rawType === 'liability' || rawType === '1' ? AccountType.Liability
      : rawType === 'equity' || rawType === '2' ? AccountType.Equity
      : rawType === 'revenue' || rawType === 'income' || rawType === '3' ? AccountType.Revenue
      : rawType === 'expense' || rawType === '4' ? AccountType.Expense
      : AccountType.Asset;

    const rawIsGroup = (cols[3] ?? '').toLowerCase();
    const isGroup = rawIsGroup === 'true' || rawIsGroup === '1' || rawIsGroup === 'yes';

    rows.push({
      accountCode: cols[0],
      accountName: cols[1],
      accountType,
      isGroup,
      parentCode: cols[4] || null,
    });
  }
  return rows;
}

@Component({
  selector: 'app-chart-of-accounts',
  standalone: true,
  imports: [
    CommonModule,
    LocalizationPipe,
    PageModule],
  templateUrl: './chart-of-accounts.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./chart-of-accounts.component.scss'],
})
export class ChartOfAccountsComponent implements OnInit {
  private accountService = inject(AccountService);
  private coaImportService = inject(ChartOfAccountsImportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  private router = inject(Router);

  companies = signal<CompanyDto[]>([]);
  selectedCompanyId = signal<string>('');
  accounts = signal<AccountNode[]>([]);
  searchQuery = signal<string>('');
  loading = signal<boolean>(false);
  expandedIds = new Set<string>();

  // ERPNext COA Importer state
  showImportPanel = signal<boolean>(false);
  importTemplate = signal<'MY' | 'US' | 'CSV'>('MY');
  csvInput = signal<string>('');
  previewRows = signal<CoaTemplateRowDto[]>([]);
  loadingTemplate = signal<boolean>(false);
  importing = signal<boolean>(false);

  // Merge Accounts state
  showMergePanel = signal<boolean>(false);
  mergeOldAccountId = signal<string>('');
  mergeNewAccountId = signal<string>('');
  merging = signal<boolean>(false);
  flatAccounts = computed(() => flattenAccountNodes(this.accounts()));

  filteredAccounts = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const nodes = this.accounts();
    if (!query) return nodes;
    return this.filterNodes(nodes, query);
  });

  ngOnInit(): void {
    const ctxCompany = this.companyContext.currentCompanyId() ?? '';
    if (ctxCompany) {
      this.selectedCompanyId.set(ctxCompany);
    }

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe({
        next: (res) => {
          const items = res.items ?? [];
          this.companies.set(items);
          if (!this.selectedCompanyId() && items.length > 0) {
            this.selectedCompanyId.set(items[0].id!);
          }
          this.loadAccounts();
        },
        error: () => this.loadAccounts(),
      });
  }

  onCompanyChanged(companyId: string): void {
    this.selectedCompanyId.set(companyId);
    this.expandedIds.clear();
    this.loadAccounts();
  }

  loadAccounts(): void {
    this.loading.set(true);
    this.accountService.getList({
      companyId: this.selectedCompanyId() || undefined,
      skipCount: 0,
      maxResultCount: 1000,
      sorting: 'accountCode asc',
    }).subscribe({
      next: (result) => {
        const accs = result.items ?? [];
        const tree = this.buildTree(accs);
        this.accounts.set(tree);
        if (this.expandedIds.size === 0) {
          for (const root of tree) {
            this.expandedIds.add(root.id);
          }
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  addAccount(parentAccountId?: string): void {
    const queryParams: Record<string, string> = {};
    if (this.selectedCompanyId()) queryParams['companyId'] = this.selectedCompanyId();
    if (parentAccountId) queryParams['parentAccountId'] = parentAccountId;
    this.router.navigate(['/accounting/accounts/new'], { queryParams });
  }

  editAccount(accountId: string): void {
    this.router.navigate(['/accounting/accounts', accountId, 'edit']);
  }

  toggleNode(id: string): void {
    if (this.expandedIds.has(id)) {
      this.expandedIds.delete(id);
    } else {
      this.expandedIds.add(id);
    }
  }

  expandAll(): void {
    const visit = (nodes: AccountNode[]) => {
      for (const n of nodes) {
        if (n.isGroup && n.children?.length) {
          this.expandedIds.add(n.id);
          visit(n.children);
        }
      }
    };
    visit(this.accounts());
  }

  collapseAll(): void {
    this.expandedIds.clear();
  }

  isExpanded(id: string): boolean {
    if (this.searchQuery().trim()) return true;
    return this.expandedIds.has(id);
  }

  hasChild = (node: AccountNode) => !!node.children?.length;

  toggleImportPanel(): void {
    const next = !this.showImportPanel();
    this.showImportPanel.set(next);
    if (next) {
      this.showMergePanel.set(false);
    }
    if (next && this.previewRows().length === 0 && this.importTemplate() !== 'CSV') {
      this.loadTemplatePreview();
    }
  }

  toggleMergePanel(): void {
    const next = !this.showMergePanel();
    this.showMergePanel.set(next);
    if (next) {
      this.showImportPanel.set(false);
    }
  }

  mergeAccounts(): void {
    const oldAccountId = this.mergeOldAccountId();
    const newAccountId = this.mergeNewAccountId();
    if (!oldAccountId || !newAccountId || oldAccountId === newAccountId) return;

    this.merging.set(true);
    this.accountService.mergeAccount({ oldAccountId, newAccountId }).subscribe({
      next: () => {
        this.merging.set(false);
        this.showMergePanel.set(false);
        this.mergeOldAccountId.set('');
        this.mergeNewAccountId.set('');
        this.toaster.success(this.l.instant('::SuccessfullySaved'));
        this.loadAccounts();
      },
      error: (err: any) => {
        this.merging.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::OperationFailed');
      },
    });
  }

  onTemplateChanged(mode: string): void {
    const next = (mode === 'US' || mode === 'CSV') ? mode : 'MY';
    this.importTemplate.set(next);
    this.previewRows.set([]);
    if (next !== 'CSV') {
      this.loadTemplatePreview();
    }
  }

  loadTemplatePreview(): void {
    const mode = this.importTemplate();
    if (mode === 'CSV') {
      this.previewRows.set(parseCoaCsv(this.csvInput()));
      return;
    }
    this.loadingTemplate.set(true);
    this.coaImportService.getTemplateByCountry(mode).subscribe({
      next: (rows) => {
        this.previewRows.set(rows ?? []);
        this.loadingTemplate.set(false);
      },
      error: () => this.loadingTemplate.set(false),
    });
  }

  importPreviewRows(): void {
    const companyId = this.selectedCompanyId();
    const rows = this.previewRows();
    if (!companyId || rows.length === 0) return;

    this.importing.set(true);
    this.coaImportService.import({
      companyId,
      rows: rows.map(r => ({
        accountCode: r.accountCode,
        accountName: r.accountName,
        accountType: r.accountType,
        isGroup: r.isGroup ?? false,
        parentCode: r.parentCode ?? null,
        subType: r.subType ?? null,
      })),
    }).subscribe({
      next: (res) => {
        this.importing.set(false);
        this.showImportPanel.set(false);
        this.toaster.success(this.l.instant('::CoaImportSuccess', String(res.accountsCreated ?? rows.length)));
        this.expandedIds.clear();
        this.loadAccounts();
      },
      error: () => this.importing.set(false),
    });
  }

  buildTree(accounts: AccountDto[]): AccountNode[] {
    const map = new Map<string, AccountNode>();
    const roots: AccountNode[] = [];

    for (const acc of accounts) {
      map.set(acc.id!, {
        id: acc.id!,
        accountCode: acc.accountCode ?? '',
        accountName: acc.accountName ?? '',
        accountType: acc.accountType ?? AccountType.Asset,
        isGroup: acc.isGroup ?? false,
        currency: acc.currency,
        isFrozen: acc.isFrozen ?? false,
        isActive: acc.isActive ?? true,
        children: [],
      });
    }

    for (const acc of accounts) {
      const node = map.get(acc.id!)!;
      if (acc.parentAccountId && map.has(acc.parentAccountId)) {
        map.get(acc.parentAccountId)!.children!.push(node);
      } else {
        roots.push(node);
      }
    }

    return roots;
  }

  private filterNodes(nodes: AccountNode[], query: string): AccountNode[] {
    const matched: AccountNode[] = [];
    for (const node of nodes) {
      const selfMatches =
        node.accountCode.toLowerCase().includes(query) ||
        node.accountName.toLowerCase().includes(query);
      const filteredChildren = node.children ? this.filterNodes(node.children, query) : [];
      if (selfMatches || filteredChildren.length > 0) {
        matched.push({
          ...node,
          children: selfMatches && filteredChildren.length === 0 ? node.children : filteredChildren,
        });
      }
    }
    return matched;
  }

  getAccountTypeName(type: AccountType | string | undefined): string {
    if (typeof type === 'number') return ACCOUNT_TYPE_NAMES[type] ?? '';
    return type ?? '';
  }

  getTypeIconClass(type: AccountType | string | undefined): string {
    switch (this.getAccountTypeName(type)) {
      case 'Asset': return 'fa-landmark text-primary';
      case 'Liability': return 'fa-credit-card text-warning';
      case 'Equity': return 'fa-piggy-bank text-info';
      case 'Revenue': return 'fa-arrow-trend-up text-success';
      case 'Expense': return 'fa-arrow-trend-down text-danger';
      default: return 'fa-folder text-secondary';
    }
  }
}
