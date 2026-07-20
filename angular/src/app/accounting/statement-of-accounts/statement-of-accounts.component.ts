import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StatementOfAccountsService } from '../../proxy/accounting/statement-of-accounts.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { exportToCsv } from '../../shared/utils/csv-export';
import type { StatementOfAccountsDto } from '../../proxy/accounting/models';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-statement-of-accounts',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">{{ 'StatementOfAccounts' | abpLocalization }}</h5>
        @if (result()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Customer' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="customerId">
              <option value="">-- Select --</option>
              @for (c of customers(); track c.id) {
                <option [value]="c.id">{{ c.customerName }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate">
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate">
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <button class="btn btn-primary btn-sm w-100" (click)="generate()" [disabled]="!customerId">
              {{ 'GenerateReport' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (result(); as r) {
          <!-- KPI Cards -->
          <div class="row g-2 mb-3">
            <div class="col-md-3">
              <div class="border rounded p-2 text-center">
                <small class="text-muted">{{ 'OpeningBalance' | abpLocalization }}</small>
                <div class="fw-bold">{{ r.openingBalance | number:'1.2-2' }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-2 text-center">
                <small class="text-muted">{{ 'TotalDebit' | abpLocalization }}</small>
                <div class="fw-bold text-primary">{{ r.totalDebit | number:'1.2-2' }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-2 text-center">
                <small class="text-muted">{{ 'TotalCredit' | abpLocalization }}</small>
                <div class="fw-bold text-success">{{ r.totalCredit | number:'1.2-2' }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-2 text-center bg-light">
                <small class="text-muted">{{ 'ClosingBalance' | abpLocalization }}</small>
                <div class="fw-bold" [class.text-danger]="(r.closingBalance ?? 0) > 0">
                  {{ r.closingBalance | number:'1.2-2' }}
                </div>
              </div>
            </div>
          </div>

          <!-- Statement Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover">
              <thead class="table-light">
                <tr>
                  <th>{{ 'Date' | abpLocalization }}</th>
                  <th>{{ 'VoucherType' | abpLocalization }}</th>
                  <th>{{ 'VoucherNumber' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Debit' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Credit' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Balance' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                <!-- Opening row -->
                <tr class="table-secondary">
                  <td colspan="3"><strong>{{ 'OpeningBalance' | abpLocalization }}</strong></td>
                  <td></td><td></td>
                  <td class="text-end"><strong>{{ r.openingBalance | number:'1.2-2' }}</strong></td>
                </tr>
                @for (entry of r.entries; track entry.documentId) {
                  <tr>
                    <td>{{ entry.date | date:'dd/MM/yyyy' }}</td>
                    <td>
                      <span class="badge"
                        [class.bg-primary]="entry.documentType === 'Sales Invoice'"
                        [class.bg-success]="entry.documentType === 'Payment'"
                        [class.bg-warning]="entry.documentType === 'Credit Note'">
                        {{ entry.documentType }}
                      </span>
                    </td>
                    <td>{{ entry.documentNumber }}</td>
                    <td class="text-end">{{ entry.debitAmount ? (entry.debitAmount | number:'1.2-2') : '' }}</td>
                    <td class="text-end">{{ entry.creditAmount ? (entry.creditAmount | number:'1.2-2') : '' }}</td>
                    <td class="text-end fw-bold">{{ entry.runningBalance | number:'1.2-2' }}</td>
                  </tr>
                }
                <!-- Closing row -->
                <tr class="table-dark">
                  <td colspan="3"><strong>{{ 'ClosingBalance' | abpLocalization }}</strong></td>
                  <td class="text-end"><strong>{{ r.totalDebit | number:'1.2-2' }}</strong></td>
                  <td class="text-end"><strong>{{ r.totalCredit | number:'1.2-2' }}</strong></td>
                  <td class="text-end"><strong>{{ r.closingBalance | number:'1.2-2' }}</strong></td>
                </tr>
              </tbody>
            </table>
          </div>
        } @else {
          <p class="text-muted text-center py-4">
            {{ 'SelectCustomerToGenerateStatement' | abpLocalization }}
          </p>
        }
      </div>
    </div>
  `
})
export class StatementOfAccountsComponent implements OnInit {
  private statementService = inject(StatementOfAccountsService);
  private companyContext = inject(CompanyContextService);
  private customerService = inject(CustomerService);

  customers = signal<any[]>([]);
  result = signal<StatementOfAccountsDto | null>(null);

  customerId = '';
  fromDate = new Date(new Date().getFullYear(), 0, 1).toISOString().substring(0, 10); // Jan 1 current year
  toDate = new Date().toISOString().substring(0, 10); // today

  ngOnInit() {
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(r => {
      this.customers.set(r.items ?? []);
    });
  }

  generate() {
    if (!this.customerId) return;
    const companyId = this.companyContext.currentCompanyId();
    this.statementService.getCustomerStatement(
      this.customerId,
      companyId || '',
      this.fromDate,
      this.toDate
    ).subscribe(data => this.result.set(data));
  }

  exportCsv() {
    const r = this.result();
    if (!r) return;
    const rows = r.entries.map(e => ({
      Date: e.date,
      Type: e.documentType,
      Number: e.documentNumber,
      Debit: e.debitAmount,
      Credit: e.creditAmount,
      Balance: e.runningBalance
    }));
    exportToCsv(`statement-of-accounts-${this.fromDate}-to-${this.toDate}.csv`,
      rows, ['Date', 'Type', 'Number', 'Debit', 'Credit', 'Balance']);
  }
}
