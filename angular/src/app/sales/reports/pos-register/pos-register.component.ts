import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PosRegisterService } from '../../../proxy/sales/pos-register.service';
import type {
  PosProfileDto,
  PosRegisterDto,
  PosRegisterRowDto,
} from '../../../proxy/sales/models';
import { PosProfileService } from '../../../proxy/sales/pos-profile.service';
import { CompanyService } from '../../../proxy/core/company.service';
import type { CompanyDto } from '../../../proxy/core/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function filterPosRegisterRows(
  rows: PosRegisterRowDto[] | undefined,
  searchTerm: string,
): PosRegisterRowDto[] {
  if (!rows || rows.length === 0) {
    return [];
  }
  const term = searchTerm.trim().toLowerCase();
  if (!term) {
    return rows;
  }
  return rows.filter(r =>
    [
      r.invoiceNumber,
      r.customerName,
      r.posProfileName,
      r.cashierName,
      r.modeOfPayment,
      r.groupKey,
    ].some(v => (v ?? '').toLowerCase().includes(term)),
  );
}

export function countPosTransactions(rows: PosRegisterRowDto[] | undefined): number {
  if (!rows || rows.length === 0) {
    return 0;
  }
  return rows.filter(r => !r.isSubtotal).length;
}

@Component({
  selector: 'app-pos-register',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-cash-register text-primary me-2"></i>
            {{ '::PosRegister' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::PosRegisterDescription' | abpLocalization }}
          </p>
        </div>
        @if (filteredRows().length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fas fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Company' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="companyId"
                (ngModelChange)="onCompanyChange()"
              >
                @for (company of companies(); track company.id) {
                  <option [value]="company.id">{{ company.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::PosProfile' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="posProfileId"
                (ngModelChange)="onPosProfileChange()"
              >
                <option value="">{{ '::AllPosProfiles' | abpLocalization }}</option>
                @for (profile of posProfiles(); track profile.id) {
                  <option [value]="profile.id">{{ profile.profileName }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::GroupBy' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="groupBy"
                (ngModelChange)="onGroupByChange()"
              >
                <option value="">{{ '::All' | abpLocalization }}</option>
                <option value="POS Profile">{{ '::PosProfile' | abpLocalization }}</option>
                <option value="Cashier">{{ '::Cashier' | abpLocalization }}</option>
                <option value="Customer">{{ '::Customer' | abpLocalization }}</option>
                <option value="Payment Method">{{ '::PaymentMethod' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadReport()"
                [disabled]="loading() || !companyId"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fas fa-sync me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
          <div class="row g-2 mt-1">
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::IsReturn' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="returnFilter"
                (ngModelChange)="loadReport()"
              >
                <option value="all">{{ '::All' | abpLocalization }}</option>
                <option value="sales">{{ '::SalesOnly' | abpLocalization }}</option>
                <option value="returns">{{ '::ReturnOnly' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::PaymentMethod' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [(ngModel)]="modeOfPayment"
                [disabled]="groupBy === 'Payment Method'"
                (keyup.enter)="loadReport()"
                (blur)="loadReport()"
              />
            </div>
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="'::InvoiceNumber' | abpLocalization"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- KPI Summary -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::Transactions' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ transactionCount() }}</div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::GrandTotal' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ (report()!.totalGrandTotal ?? 0) | number: '1.2-2' }}</div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::PaidAmount' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ (report()!.totalPaidAmount ?? 0) | number: '1.2-2' }}</div>
            </div>
          </div>
        </div>
      }

      <!-- Table -->
      <div class="card border-0 shadow-sm">
        <div class="table-responsive">
          <table class="table table-hover table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>{{ '::PostingDate' | abpLocalization }}</th>
                <th>{{ '::InvoiceType' | abpLocalization }}</th>
                <th>{{ '::InvoiceNumber' | abpLocalization }}</th>
                <th>{{ '::Customer' | abpLocalization }}</th>
                <th>{{ '::PosProfile' | abpLocalization }}</th>
                <th>{{ '::Cashier' | abpLocalization }}</th>
                <th>{{ '::PaymentMethod' | abpLocalization }}</th>
                <th class="text-end">{{ '::GrandTotal' | abpLocalization }}</th>
                <th class="text-end">{{ '::PaidAmount' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (loading()) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}
                  </td>
                </tr>
              } @else if (filteredRows().length === 0) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    {{ '::NoDataAvailable' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows(); track $index) {
                  <tr [class.table-secondary]="row.isSubtotal" [class.fw-bold]="row.isSubtotal">
                    <td>{{ row.postingDate ? (row.postingDate | date: 'yyyy-MM-dd') : '' }}</td>
                    <td>{{ row.invoiceType || '' }}</td>
                    <td class="fw-semibold">
                      {{ row.invoiceNumber }}
                      @if (row.isReturn) {
                        <span class="badge bg-warning text-dark ms-1">{{ '::IsReturn' | abpLocalization }}</span>
                      }
                    </td>
                    <td>{{ row.customerName || '' }}</td>
                    <td>{{ row.posProfileName || '' }}</td>
                    <td>{{ row.cashierName || '' }}</td>
                    <td>{{ row.modeOfPayment || '' }}</td>
                    <td class="text-end">{{ (row.grandTotal ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.paidAmount ?? 0) | number: '1.2-2' }}</td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
})
export class PosRegisterComponent implements OnInit {
  private posRegisterService = inject(PosRegisterService);
  private posProfileService = inject(PosProfileService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  companies = signal<CompanyDto[]>([]);
  posProfiles = signal<PosProfileDto[]>([]);
  report = signal<PosRegisterDto | null>(null);
  loading = signal(false);
  searchTerm = signal('');

  companyId = '';
  fromDate = '';
  toDate = '';
  posProfileId = '';
  groupBy = '';
  returnFilter = 'all';
  modeOfPayment = '';

  filteredRows = computed(() =>
    filterPosRegisterRows(this.report()?.rows, this.searchTerm()),
  );

  transactionCount = computed(() => countPosTransactions(this.filteredRows()));

  ngOnInit(): void {
    this.companyContext.load();
    const now = new Date();
    const monthStart = new Date(now.getFullYear(), now.getMonth(), 1);
    this.fromDate = monthStart.toISOString().slice(0, 10);
    this.toDate = now.toISOString().slice(0, 10);

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        const items = res.items ?? [];
        this.companies.set(items);
        this.companyId = this.companyContext.currentCompanyId() || items[0]?.id || '';
        if (this.companyId) {
          this.loadPosProfiles();
          this.loadReport();
        }
      },
    });
  }

  onCompanyChange(): void {
    this.posProfileId = '';
    this.loadPosProfiles();
    this.loadReport();
  }

  onPosProfileChange(): void {
    if (this.posProfileId && this.groupBy === 'POS Profile') {
      this.groupBy = '';
    }
    this.loadReport();
  }

  onGroupByChange(): void {
    if (this.groupBy === 'POS Profile') {
      this.posProfileId = '';
    }
    if (this.groupBy === 'Payment Method') {
      this.modeOfPayment = '';
    }
    this.loadReport();
  }

  loadPosProfiles(): void {
    if (!this.companyId) return;
    this.posProfileService.getList({ companyId: this.companyId, maxResultCount: 100 }).subscribe({
      next: res => this.posProfiles.set(res.items ?? []),
      error: () => this.posProfiles.set([]),
    });
  }

  loadReport(): void {
    if (!this.companyId || !this.fromDate || !this.toDate) return;
    this.loading.set(true);

    const isReturn =
      this.returnFilter === 'returns' ? true : this.returnFilter === 'sales' ? false : null;

    this.posRegisterService
      .getReport({
        companyId: this.companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
        posProfileId: this.posProfileId || null,
        isReturn,
        modeOfPayment: this.modeOfPayment.trim() || null,
        groupBy: this.groupBy || null,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  exportCsv(): void {
    exportToCsv('pos-register.csv', this.filteredRows(), [
      'postingDate',
      'invoiceType',
      'invoiceNumber',
      'customerName',
      'posProfileName',
      'cashierName',
      'modeOfPayment',
      'grandTotal',
      'paidAmount',
    ]);
  }
}
