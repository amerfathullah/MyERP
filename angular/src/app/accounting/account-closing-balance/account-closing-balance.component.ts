import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

interface ClosingBalanceItem {
  accountName: string;
  accountCode: string;
  debit: number;
  credit: number;
  balance: number;
  costCenterName?: string;
  financeBook?: string;
}

interface ClosingBalanceStatus {
  latestPeriod: string | null;
  latestClosingDate: string | null;
  totalBalances: number;
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
}

@Component({
  selector: 'app-account-closing-balance',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid mt-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4>{{ 'AccountClosingBalances' | abpLocalization }}</h4>
        <div class="d-flex gap-2">
          <button class="btn btn-outline-primary btn-sm" (click)="exportCsv()" [disabled]="balances().length === 0">
            <i class="fas fa-download me-1"></i> {{ 'ExportCSV' | abpLocalization }}
          </button>
          <button class="btn btn-primary btn-sm" (click)="rebuild()" [disabled]="isRebuilding()">
            <i class="fas fa-rotate me-1"></i> {{ 'Rebuild' | abpLocalization }}
          </button>
        </div>
      </div>

      <!-- Status Card -->
      <div class="card mb-3">
        <div class="card-body">
          <div class="row text-center">
            <div class="col-md-2">
              <div class="text-muted small">{{ 'Period' | abpLocalization }}</div>
              <div class="fw-bold">{{ status()?.latestPeriod || '—' }}</div>
            </div>
            <div class="col-md-2">
              <div class="text-muted small">{{ 'ClosingDate' | abpLocalization }}</div>
              <div class="fw-bold">{{ status()?.latestClosingDate | date:'dd/MM/yyyy' }}</div>
            </div>
            <div class="col-md-2">
              <div class="text-muted small">{{ 'TotalAccounts' | abpLocalization }}</div>
              <div class="fw-bold">{{ status()?.totalBalances || 0 }}</div>
            </div>
            <div class="col-md-3">
              <div class="text-muted small">{{ 'TotalDebit' | abpLocalization }}</div>
              <div class="fw-bold text-success">{{ status()?.totalDebit | number:'1.2-2' }}</div>
            </div>
            <div class="col-md-3">
              <div class="text-muted small">{{ 'TotalCredit' | abpLocalization }}</div>
              <div class="fw-bold text-danger">{{ status()?.totalCredit | number:'1.2-2' }}</div>
            </div>
          </div>
          @if (status()?.isBalanced) {
            <div class="text-center mt-2">
              <span class="badge bg-success"><i class="fas fa-check me-1"></i> Balanced</span>
            </div>
          }
        </div>
      </div>

      <!-- Period Selector + Rebuild -->
      <div class="card mb-3">
        <div class="card-body">
          <div class="row g-2 align-items-end">
            <div class="col-md-4">
              <label class="form-label">{{ 'Period' | abpLocalization }}</label>
              <input type="text" class="form-control form-control-sm" [(ngModel)]="period"
                     [placeholder]="'YYYY-MM'" (keyup.enter)="loadBalances()">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'ClosingDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="closingDate">
            </div>
            <div class="col-md-2">
              <button class="btn btn-outline-secondary btn-sm w-100" (click)="loadBalances()">
                <i class="fas fa-search me-1"></i> {{ 'Load' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Balances Table -->
      @if (balances().length > 0) {
        <div class="card">
          <div class="card-body p-0">
            <table class="table table-hover table-sm mb-0">
              <thead>
                <tr>
                  <th>{{ 'AccountCode' | abpLocalization }}</th>
                  <th>{{ 'AccountName' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Debit' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Credit' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Balance' | abpLocalization }}</th>
                  <th>{{ 'CostCenter' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (b of balances(); track b.accountCode) {
                  <tr>
                    <td class="text-muted">{{ b.accountCode }}</td>
                    <td>{{ b.accountName }}</td>
                    <td class="text-end">{{ b.debit | number:'1.2-2' }}</td>
                    <td class="text-end">{{ b.credit | number:'1.2-2' }}</td>
                    <td class="text-end" [class.text-success]="b.balance > 0" [class.text-danger]="b.balance < 0">
                      {{ b.balance | number:'1.2-2' }}
                    </td>
                    <td class="text-muted">{{ b.costCenterName || '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      } @else if (!isLoading()) {
        <div class="text-center text-muted py-4">
          <i class="fas fa-database fa-2x mb-2 d-block"></i>
          {{ 'NoClosingBalancesForPeriod' | abpLocalization }}
        </div>
      }
    </div>
  `
})
export class AccountClosingBalanceComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  balances = signal<ClosingBalanceItem[]>([]);
  status = signal<ClosingBalanceStatus | null>(null);
  isLoading = signal(false);
  isRebuilding = signal(false);
  period = '';
  closingDate = '';

  ngOnInit(): void {
    // Default to current month
    const now = new Date();
    this.period = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
    this.closingDate = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString().split('T')[0];
    this.loadStatus();
  }

  loadStatus(): void {
    const cid = this.companyContext.currentCompanyId();
    if (!cid) return;
    this.http.get<ClosingBalanceStatus>(`/api/app/account-closing-balance/status?companyId=${cid}`)
      .subscribe({
        next: s => {
          this.status.set(s);
          if (s.latestPeriod) this.period = s.latestPeriod;
        },
        error: () => {}
      });
  }

  loadBalances(): void {
    const cid = this.companyContext.currentCompanyId();
    if (!cid || !this.period) return;
    this.isLoading.set(true);
    this.http.get<ClosingBalanceItem[]>(`/api/app/account-closing-balance?companyId=${cid}&period=${this.period}`)
      .subscribe({
        next: items => { this.balances.set(items); this.isLoading.set(false); },
        error: () => this.isLoading.set(false)
      });
  }

  rebuild(): void {
    const cid = this.companyContext.currentCompanyId();
    if (!cid || !this.period || !this.closingDate) return;
    this.isRebuilding.set(true);
    this.http.post<number>('/api/app/account-closing-balance/rebuild', {
      companyId: cid, closingDate: this.closingDate, period: this.period
    }).subscribe({
      next: count => {
        this.toaster.success(`Rebuilt ${count} closing balances`, 'Success');
        this.isRebuilding.set(false);
        this.loadBalances();
        this.loadStatus();
      },
      error: () => this.isRebuilding.set(false)
    });
  }

  exportCsv(): void {
    const rows = this.balances().map(b => ({
      'Account Code': b.accountCode,
      'Account Name': b.accountName,
      'Debit': b.debit,
      'Credit': b.credit,
      'Balance': b.balance,
      'Cost Center': b.costCenterName || ''
    }));
    exportToCsv(`closing-balances-${this.period}.csv`, rows,
      ['Account Code', 'Account Name', 'Debit', 'Credit', 'Balance', 'Cost Center']);
  }
}
