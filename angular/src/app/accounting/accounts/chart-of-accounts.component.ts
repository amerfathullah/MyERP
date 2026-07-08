import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTreeModule, MatTreeNestedDataSource } from '@angular/material/tree';
import { NestedTreeControl } from '@angular/cdk/tree';
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
    PageModule,
    MatCardModule,
    MatTreeModule,
  ],
  templateUrl: './chart-of-accounts.component.html',
  styleUrls: ['./chart-of-accounts.component.scss'],
})
export class ChartOfAccountsComponent implements OnInit {
  private accountService = inject(AccountService);
  treeControl = new NestedTreeControl<AccountNode>(node => node.children);
  dataSource = new MatTreeNestedDataSource<AccountNode>();

  hasChild = (_: number, node: AccountNode) => node.isGroup && !!node.children?.length;

  ngOnInit(): void {
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe((result) => {
        const accounts = result.items ?? [];
        this.dataSource.data = this.buildTree(accounts);
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
