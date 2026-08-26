import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { BankAccountBalanceService } from '../../proxy/accounting/bank-account-balance.service';
import { BankAccountService } from '../../proxy/accounting/bank-account.service';
import { BankAccountBalanceDto, BankAccountDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-bank-account-balance-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Bank Account Balances</h5>
        <a routerLink="/accounting/bank-account-balances/new" class="btn btn-primary btn-sm">New Balance Snapshot</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <select class="form-select form-select-sm" [(ngModel)]="bankAccountId" (ngModelChange)="load()">
              <option [ngValue]="undefined">All Bank Accounts</option>
              @for (acc of bankAccounts; track acc.id) {
                <option [ngValue]="acc.id">{{ acc.accountName }}</option>
              }
            </select>
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Bank Account</th>
              <th>Company</th>
              <th>Date</th>
              <th class="text-end">Balance</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/accounting/bank-account-balances', item.id, 'edit']" class="fw-semibold">
                    {{ item.bankAccountName }}
                  </a>
                </td>
                <td>{{ item.companyName || '—' }}</td>
                <td>{{ item.date | date:'mediumDate' }}</td>
                <td class="text-end">{{ item.balance | number:'1.2-2' }}</td>
                <td class="text-end">
                  <a [routerLink]="['/accounting/bank-account-balances', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="text-center text-muted py-4">No balance snapshots recorded yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class BankAccountBalanceListComponent implements OnInit {
  private service = inject(BankAccountBalanceService);
  private bankAccountService = inject(BankAccountService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: BankAccountBalanceDto[] = [];
  bankAccounts: BankAccountDto[] = [];
  bankAccountId?: string;

  ngOnInit() {
    this.bankAccountService.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.bankAccounts = res.items ?? [];
    });
    this.load();
  }

  load(): void {
    this.service.getList({ bankAccountId: this.bankAccountId, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: BankAccountBalanceDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(item.id!).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyDeleted');
          this.load();
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
      });
    });
  }
}
