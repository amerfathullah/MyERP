import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountService } from '../../proxy/accounting/account.service';
import { BankClearanceService } from '../../proxy/accounting/bank-clearance.service';
import type { BankClearanceEntryDto } from '../../proxy/accounting/models';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface AccountOption {
  id: string;
  accountCode: string;
  accountName: string;
}

/**
 * Bank Clearance — marks Payment/Journal Entries as cleared against a bank statement
 * by setting ClearanceDate. Per ERPNext accounts/doctype/bank_clearance.
 * Drives the outstanding/uncleared calculation on the Bank Reconciliation Statement.
 */
@Component({
  selector: 'app-bank-clearance',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">{{ '::BankClearance' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ '::BankAccount' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="selectedAccountId" (change)="onFilterChange()">
              <option value="">{{ '::SelectAccount' | abpLocalization }}</option>
              @for (acc of bankAccounts(); track acc.id) {
                <option [value]="acc.id">{{ acc.accountCode }} — {{ acc.accountName }}</option>
              }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ '::FromDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="fromDate" (change)="onFilterChange()">
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ '::ToDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="toDate" (change)="onFilterChange()">
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <div class="form-check">
              <input class="form-check-input" type="checkbox" id="includeCleared" [(ngModel)]="includeCleared" (change)="loadEntries()">
              <label class="form-check-label" for="includeCleared">{{ '::IncludeCleared' | abpLocalization }}</label>
            </div>
          </div>
          <div class="col-md-3 d-flex align-items-end">
            <button class="btn btn-primary" (click)="loadEntries()" [disabled]="!selectedAccountId || !fromDate || !toDate || isLoading()">
              @if (isLoading()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              {{ '::Search' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (entries().length > 0) {
          <!-- Bulk clearance action bar -->
          <div class="d-flex align-items-center gap-2 mb-3 p-2 bg-light rounded">
            <div class="form-check mb-0">
              <input class="form-check-input" type="checkbox" id="selectAll" [checked]="allSelected()" (change)="toggleSelectAll()">
              <label class="form-check-label" for="selectAll">{{ '::SelectAll' | abpLocalization }}</label>
            </div>
            <span class="text-muted small">{{ selectedIds().size }} {{ '::Selected' | abpLocalization }}</span>
            <input type="date" class="form-control form-control-sm" style="width:160px" [(ngModel)]="bulkClearanceDate">
            <button class="btn btn-sm btn-success" [disabled]="selectedIds().size === 0 || !bulkClearanceDate || isSaving()"
                    (click)="applyClearance(bulkClearanceDate)">
              <i class="fa fa-check me-1"></i>{{ '::MarkAsCleared' | abpLocalization }}
            </button>
            <button class="btn btn-sm btn-outline-secondary" [disabled]="selectedIds().size === 0 || isSaving()"
                    (click)="applyClearance(null)">
              <i class="fa fa-rotate-left me-1"></i>{{ '::UnClear' | abpLocalization }}
            </button>
          </div>

          <div class="table-responsive">
            <table class="table table-hover table-sm">
              <thead class="table-light">
                <tr>
                  <th style="width:36px"></th>
                  <th>{{ '::PostingDate' | abpLocalization }}</th>
                  <th>{{ '::VoucherType' | abpLocalization }}</th>
                  <th>{{ '::VoucherNumber' | abpLocalization }}</th>
                  <th>{{ '::ReferenceNumber' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Debit' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Credit' | abpLocalization }}</th>
                  <th>{{ '::ClearanceDate' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (e of entries(); track entryKey(e)) {
                  <tr [class.table-success]="!!e.clearanceDate">
                    <td>
                      <input class="form-check-input" type="checkbox"
                             [checked]="selectedIds().has(entryKey(e))"
                             (change)="toggleSelect(entryKey(e))">
                    </td>
                    <td>{{ e.postingDate | date:'dd/MM/yyyy' }}</td>
                    <td><span class="badge bg-secondary">{{ e.documentType }}</span></td>
                    <td class="fw-semibold">{{ e.documentNumber }}</td>
                    <td class="text-muted">{{ e.referenceNumber || '—' }}</td>
                    <td class="text-end">{{ e.debit ? (e.debit | number:'1.2-2') : '' }}</td>
                    <td class="text-end">{{ e.credit ? (e.credit | number:'1.2-2') : '' }}</td>
                    <td>
                      @if (e.clearanceDate) {
                        <span class="text-success"><i class="fa fa-check-circle me-1"></i>{{ e.clearanceDate | date:'dd/MM/yyyy' }}</span>
                      } @else {
                        <span class="text-muted">{{ '::Uncleared' | abpLocalization }}</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else if (searched()) {
          <div class="text-center text-muted py-4">
            <i class="fa fa-check-circle fa-2x text-success mb-2"></i>
            <p class="mb-0">{{ '::NoEntriesFound' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>
  `,
})
export class BankClearanceComponent implements OnInit {
  bankAccounts = signal<AccountOption[]>([]);
  entries = signal<BankClearanceEntryDto[]>([]);
  selectedIds = signal<Set<string>>(new Set());
  isLoading = signal(false);
  isSaving = signal(false);
  searched = signal(false);

  selectedAccountId = '';
  fromDate = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];
  includeCleared = false;
  bulkClearanceDate = new Date().toISOString().split('T')[0];

  allSelected = computed(() => this.entries().length > 0 && this.selectedIds().size === this.entries().length);

  private accountService = inject(AccountService);
  private clearanceService = inject(BankClearanceService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  ngOnInit(): void {
    this.loadBankAccounts();
  }

  private loadBankAccounts(): void {
    this.accountService.getList({ maxResultCount: 200, skipCount: 0, sorting: '', accountType: 'Bank' } as any).subscribe({
      next: (res) => {
        this.bankAccounts.set((res.items || []).map((a: any) => ({
          id: a.id, accountCode: a.accountCode, accountName: a.accountName,
        })));
      },
    });
  }

  entryKey(e: BankClearanceEntryDto): string {
    return `${e.documentType}:${e.documentId}`;
  }

  onFilterChange(): void {
    this.entries.set([]);
    this.selectedIds.set(new Set());
    this.searched.set(false);
  }

  loadEntries(): void {
    if (!this.selectedAccountId || !this.fromDate || !this.toDate) return;
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.clearanceService.getEntries({
      bankAccountId: this.selectedAccountId,
      companyId,
      fromDate: this.fromDate,
      toDate: this.toDate,
      includeCleared: this.includeCleared,
    }).subscribe({
      next: (data) => {
        this.entries.set(data || []);
        this.selectedIds.set(new Set());
        this.isLoading.set(false);
        this.searched.set(true);
      },
      error: () => {
        this.isLoading.set(false);
        this.toaster.error('::FailedToLoadEntries');
      },
    });
  }

  toggleSelect(key: string): void {
    this.selectedIds.update(set => {
      const next = new Set(set);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  }

  toggleSelectAll(): void {
    if (this.allSelected()) {
      this.selectedIds.set(new Set());
    } else {
      this.selectedIds.set(new Set(this.entries().map(e => this.entryKey(e))));
    }
  }

  applyClearance(clearanceDate: string | null): void {
    const keys = this.selectedIds();
    if (keys.size === 0) return;

    const entries = this.entries()
      .filter(e => keys.has(this.entryKey(e)))
      .map(e => ({ documentType: e.documentType, documentId: e.documentId }));

    this.isSaving.set(true);
    this.clearanceService.setClearanceDate({ entries, clearanceDate }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.toaster.success(clearanceDate ? '::SuccessfullyCleared' : '::SuccessfullyUncleared');
        this.loadEntries();
      },
      error: (err: any) => {
        this.isSaving.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }
}
