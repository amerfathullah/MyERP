import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { AccountService } from '../../proxy/accounting/account.service';
import type { AccountDto } from '../../proxy/accounting/models';

export interface AccountNode {
  id: string;
  accountCode: string;
  accountName: string;
  accountType: string;
  isGroup: boolean;
  balance?: number;
  children?: AccountNode[];
}

@Component({
  selector: 'app-chart-of-accounts',
  standalone: true,
  imports: [
    CommonModule,
    PageModule],
  templateUrl: './chart-of-accounts.component.html',
  styleUrls: ['./chart-of-accounts.component.scss'],
})
export class ChartOfAccountsComponent implements OnInit {
  private accountService = inject(AccountService);
  accounts = signal<AccountNode[]>([]);
  expandedIds = new Set<string>();

  toggleNode(id: string): void {
    if (this.expandedIds.has(id)) {
      this.expandedIds.delete(id);
    } else {
      this.expandedIds.add(id);
    }
  }

  isExpanded(id: string): boolean {
    return this.expandedIds.has(id);
  }

  hasChild = (node: AccountNode) => node.isGroup && !!node.children?.length;

  ngOnInit(): void {
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe((result) => {
        const accs = result.items ?? [];
        this.accounts.set(this.buildTree(accs));
      });
  }

  private buildTree(accounts: AccountDto[]): AccountNode[] {
    const map = new Map<string, AccountNode>();
    const roots: AccountNode[] = [];

    // Create nodes
    for (const acc of accounts) {
      map.set(acc.id!, {
        id: acc.id!,
        accountCode: acc.accountCode ?? '',
        accountName: acc.accountName ?? '',
        accountType: acc.accountType as any ?? '',
        isGroup: acc.isGroup ?? false,
        children: [],
      });
    }

    // Build hierarchy
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

  getTypeIcon(type: string): string {
    switch (type) {
      case 'Asset': return 'account_balance';
      case 'Liability': return 'credit_card';
      case 'Equity': return 'savings';
      case 'Revenue': return 'trending_up';
      case 'Expense': return 'trending_down';
      default: return 'folder';
    }
  }

  addAccount(): void {
    // TODO: Open create account dialog
  }
}
