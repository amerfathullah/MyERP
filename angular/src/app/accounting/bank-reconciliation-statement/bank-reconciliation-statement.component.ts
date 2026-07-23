import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AccountService } from '../../proxy/accounting/account.service';
import { BankReconciliationService } from '../../proxy/accounting/bank-reconciliation.service';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

interface BankStatementEntry {
  postingDate: string;
  documentType: string;
  documentNumber: string;
  documentId: string;
  debit: number;
  credit: number;
  referenceNumber?: string;
  clearanceDate?: string;
  partyName?: string;
}

interface ReconciliationStatement {
  glBalance?: number;
  outstandingDeposits?: number;
  outstandingPayments?: number;
  netOutstanding?: number;
  calculatedBankBalance?: number;
  unclearedEntries?: BankStatementEntry[];
  currencyCode?: string;
  reportDate?: string;
  bankAccountName?: string;
}

interface AccountOption {
  id: string;
  accountCode: string;
  accountName: string;
}

@Component({
  selector: 'app-bank-reconciliation-statement',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">{{ '::BankReconciliationStatement' | abpLocalization }}</h5>
        @if (statement()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-3 mb-4">
          <div class="col-md-4">
            <label class="form-label">{{ '::BankAccount' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="selectedAccountId" (change)="onFilterChange()">
              <option value="">{{ '::SelectAccount' | abpLocalization }}</option>
              @for (acc of bankAccounts(); track acc.id) {
                <option [value]="acc.id">{{ acc.accountCode }} — {{ acc.accountName }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ '::ReportDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="reportDate" (change)="onFilterChange()">
          </div>
          <div class="col-md-3 d-flex align-items-end">
            <button class="btn btn-primary" (click)="generateStatement()" [disabled]="!selectedAccountId || !reportDate || isLoading()">
              @if (isLoading()) {
                <span class="spinner-border spinner-border-sm me-1"></span>
              }
              {{ '::GenerateReport' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (statement(); as stmt) {
          <!-- Summary KPIs -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="card bg-light border-0">
                <div class="card-body text-center py-3">
                  <small class="text-muted d-block mb-1">{{ '::GLBalance' | abpLocalization }}</small>
                  <h5 class="mb-0 fw-bold" [class.text-success]="stmt.glBalance >= 0" [class.text-danger]="stmt.glBalance < 0">
                    {{ stmt.currencyCode }} {{ stmt.glBalance | number:'1.2-2' }}
                  </h5>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card bg-light border-0">
                <div class="card-body text-center py-3">
                  <small class="text-muted d-block mb-1">{{ '::OutstandingDeposits' | abpLocalization }}</small>
                  <h5 class="mb-0 text-info">
                    {{ stmt.currencyCode }} {{ stmt.outstandingDeposits | number:'1.2-2' }}
                  </h5>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card bg-light border-0">
                <div class="card-body text-center py-3">
                  <small class="text-muted d-block mb-1">{{ '::OutstandingPayments' | abpLocalization }}</small>
                  <h5 class="mb-0 text-warning">
                    {{ stmt.currencyCode }} {{ stmt.outstandingPayments | number:'1.2-2' }}
                  </h5>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-primary border-0" style="background-color: #e8f4fd;">
                <div class="card-body text-center py-3">
                  <small class="text-muted d-block mb-1">{{ '::CalculatedBankBalance' | abpLocalization }}</small>
                  <h5 class="mb-0 fw-bold text-primary">
                    {{ stmt.currencyCode }} {{ stmt.calculatedBankBalance | number:'1.2-2' }}
                  </h5>
                </div>
              </div>
            </div>
          </div>

          <!-- Formula display -->
          <div class="alert alert-info py-2 mb-4">
            <small>
              <strong>{{ '::GLBalance' | abpLocalization }}</strong> ({{ stmt.glBalance | number:'1.2-2' }})
              − <strong>{{ '::NetOutstanding' | abpLocalization }}</strong> ({{ stmt.netOutstanding | number:'1.2-2' }})
              = <strong>{{ '::CalculatedBankBalance' | abpLocalization }}</strong> ({{ stmt.calculatedBankBalance | number:'1.2-2' }})
            </small>
          </div>

          <!-- Uncleared entries table -->
          @if (stmt.unclearedEntries.length > 0) {
            <h6 class="text-muted mb-3">
              <i class="fa fa-clock me-1"></i>
              {{ '::UnclearedEntries' | abpLocalization }} ({{ stmt.unclearedEntries.length }})
            </h6>
            <div class="table-responsive">
              <table class="table table-hover table-sm">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::PostingDate' | abpLocalization }}</th>
                    <th>{{ '::VoucherType' | abpLocalization }}</th>
                    <th>{{ '::VoucherNumber' | abpLocalization }}</th>
                    <th>{{ '::ReferenceNumber' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Debit' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Credit' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (entry of stmt.unclearedEntries; track entry.documentId) {
                    <tr>
                      <td>{{ entry.postingDate | date:'dd/MM/yyyy' }}</td>
                      <td><span class="badge bg-secondary">{{ entry.documentType }}</span></td>
                      <td class="fw-semibold">{{ entry.documentNumber }}</td>
                      <td class="text-muted">{{ entry.referenceNumber || '—' }}</td>
                      <td class="text-end" [class.fw-bold]="entry.debit > 0">
                        {{ entry.debit > 0 ? (entry.debit | number:'1.2-2') : '' }}
                      </td>
                      <td class="text-end" [class.fw-bold]="entry.credit > 0">
                        {{ entry.credit > 0 ? (entry.credit | number:'1.2-2') : '' }}
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light">
                  <tr class="fw-bold">
                    <td colspan="4">{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ stmt.outstandingDeposits | number:'1.2-2' }}</td>
                    <td class="text-end">{{ stmt.outstandingPayments | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          } @else {
            <div class="text-center text-muted py-4">
              <i class="fa fa-check-circle fa-2x text-success mb-2"></i>
              <p class="mb-0">{{ '::AllEntriesCleared' | abpLocalization }}</p>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    @media print {
      .btn, app-breadcrumb { display: none !important; }
      .card { border: none !important; box-shadow: none !important; }
    }
  `]
})
export class BankReconciliationStatementComponent implements OnInit {
  bankAccounts = signal<AccountOption[]>([]);
  statement = signal<ReconciliationStatement | null>(null);
  isLoading = signal(false);

  selectedAccountId = '';
  reportDate = new Date().toISOString().split('T')[0];

  private accountService = inject(AccountService);
  private bankReconciliationService = inject(BankReconciliationService);
  private companyContext = inject(CompanyContextService);

  ngOnInit(): void {
    this.loadBankAccounts();
  }

  private loadBankAccounts(): void {
    // Load accounts of type Bank for the current company
    this.accountService.getList({ maxResultCount: 200, skipCount: 0, sorting: '', accountType: 'Bank' } as any).subscribe({
      next: (res) => {
        const items = (res.items || []).map((a: any) => ({
          id: a.id,
          accountCode: a.accountCode,
          accountName: a.accountName
        }));
        this.bankAccounts.set(items);
      }
    });
  }

  onFilterChange(): void {
    // Clear statement when filters change
    this.statement.set(null);
  }

  generateStatement(): void {
    if (!this.selectedAccountId || !this.reportDate) return;

    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.bankReconciliationService.getReconciliationStatement({ bankAccountId: this.selectedAccountId, companyId: companyId, reportDate: this.reportDate } as any).subscribe({
      next: (data) => {
        this.statement.set(data as ReconciliationStatement);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  exportCsv(): void {
    const stmt = this.statement();
    if (!stmt) return;

    const rows = stmt.unclearedEntries.map(e => ({
      'Posting Date': e.postingDate,
      'Document Type': e.documentType,
      'Document Number': e.documentNumber,
      'Reference #': e.referenceNumber || '',
      'Debit': e.debit || '',
      'Credit': e.credit || ''
    }));

    // Add summary rows
    rows.push({ 'Posting Date': '', 'Document Type': '', 'Document Number': 'GL Balance', 'Reference #': '', 'Debit': stmt.glBalance as any, 'Credit': '' });
    rows.push({ 'Posting Date': '', 'Document Type': '', 'Document Number': 'Outstanding Deposits', 'Reference #': '', 'Debit': stmt.outstandingDeposits as any, 'Credit': '' });
    rows.push({ 'Posting Date': '', 'Document Type': '', 'Document Number': 'Outstanding Payments', 'Reference #': '', 'Debit': '', 'Credit': stmt.outstandingPayments as any });
    rows.push({ 'Posting Date': '', 'Document Type': '', 'Document Number': 'Calculated Bank Balance', 'Reference #': '', 'Debit': stmt.calculatedBankBalance as any, 'Credit': '' });

    exportToCsv(`bank-reconciliation-statement-${this.reportDate}.csv`, rows,
      ['Posting Date', 'Document Type', 'Document Number', 'Reference #', 'Debit', 'Credit']);
  }
}
