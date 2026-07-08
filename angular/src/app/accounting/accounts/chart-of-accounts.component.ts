import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTreeModule, MatTreeNestedDataSource } from '@angular/material/tree';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { NestedTreeControl } from '@angular/cdk/tree';

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
    MatIconModule,
    MatButtonModule,
  ],
  templateUrl: './chart-of-accounts.component.html',
  styleUrls: ['./chart-of-accounts.component.scss'],
})
export class ChartOfAccountsComponent implements OnInit {
  treeControl = new NestedTreeControl<AccountNode>(node => node.children);
  dataSource = new MatTreeNestedDataSource<AccountNode>();

  hasChild = (_: number, node: AccountNode) => node.isGroup && !!node.children?.length;

  ngOnInit(): void {
    // TODO: Load from AccountAppService proxy
    // Mock data for UI development
    this.dataSource.data = [
      {
        id: '1', accountCode: '1000', accountName: 'Assets', accountType: 'Asset', isGroup: true,
        children: [
          {
            id: '2', accountCode: '1100', accountName: 'Current Assets', accountType: 'Asset', isGroup: true,
            children: [
              { id: '3', accountCode: '1110', accountName: 'Cash and Bank', accountType: 'Asset', isGroup: false, balance: 50000 },
              { id: '4', accountCode: '1120', accountName: 'Accounts Receivable', accountType: 'Asset', isGroup: false, balance: 25000 },
              { id: '5', accountCode: '1130', accountName: 'Inventory', accountType: 'Asset', isGroup: false, balance: 15000 },
            ],
          },
          {
            id: '6', accountCode: '1200', accountName: 'Fixed Assets', accountType: 'Asset', isGroup: true,
            children: [
              { id: '7', accountCode: '1210', accountName: 'Equipment', accountType: 'Asset', isGroup: false, balance: 80000 },
            ],
          },
        ],
      },
      {
        id: '10', accountCode: '2000', accountName: 'Liabilities', accountType: 'Liability', isGroup: true,
        children: [
          { id: '11', accountCode: '2100', accountName: 'Accounts Payable', accountType: 'Liability', isGroup: false, balance: 12000 },
          { id: '12', accountCode: '2200', accountName: 'SST Payable', accountType: 'Liability', isGroup: false, balance: 3500 },
        ],
      },
      {
        id: '20', accountCode: '3000', accountName: 'Equity', accountType: 'Equity', isGroup: true,
        children: [
          { id: '21', accountCode: '3100', accountName: 'Share Capital', accountType: 'Equity', isGroup: false, balance: 100000 },
          { id: '22', accountCode: '3200', accountName: 'Retained Earnings', accountType: 'Equity', isGroup: false, balance: 54500 },
        ],
      },
      {
        id: '30', accountCode: '4000', accountName: 'Revenue', accountType: 'Revenue', isGroup: true,
        children: [
          { id: '31', accountCode: '4100', accountName: 'Sales Revenue', accountType: 'Revenue', isGroup: false, balance: 200000 },
          { id: '32', accountCode: '4200', accountName: 'Service Revenue', accountType: 'Revenue', isGroup: false, balance: 50000 },
        ],
      },
      {
        id: '40', accountCode: '5000', accountName: 'Expenses', accountType: 'Expense', isGroup: true,
        children: [
          { id: '41', accountCode: '5100', accountName: 'Cost of Goods Sold', accountType: 'Expense', isGroup: false, balance: 120000 },
          { id: '42', accountCode: '5200', accountName: 'Operating Expenses', accountType: 'Expense', isGroup: false, balance: 35000 },
        ],
      },
    ];
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
