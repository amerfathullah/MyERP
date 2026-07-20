import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { OpeningBalanceService } from '../../proxy/accounting/opening-balance.service';
import type { OpeningStatusDto } from '../../proxy/accounting/models';

interface AccountDto {
  id: string;
  accountCode: string;
  accountName: string;
  accountType: number;
}

@Component({
  selector: 'app-opening-balance',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'OpeningBalance' | abpLocalization">
      <!-- Status Card -->
      <div class="card mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h6 class="card-title mb-0"><i class="fa fa-info-circle me-2"></i>{{ 'OpeningStatus' | abpLocalization }}</h6>
          <button class="btn btn-sm btn-outline-secondary" (click)="loadStatus()">
            <i class="fa fa-refresh"></i>
          </button>
        </div>
        @if (status()) {
        <div class="card-body">
          <div class="row">
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="text-muted small">{{ 'TemporaryOpeningBalance' | abpLocalization }}</div>
                <div class="fs-4 fw-bold" [class.text-success]="status()!.isBalanced" [class.text-danger]="!status()!.isBalanced">
                  {{ status()!.temporaryOpeningBalance | number:'1.2-2' }}
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="text-muted small">{{ 'OpeningJournalEntries' | abpLocalization }}</div>
                <div class="fs-4 fw-bold">{{ status()!.openingJournalEntryCount }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="text-muted small">{{ 'OpeningSalesInvoices' | abpLocalization }}</div>
                <div class="fs-4 fw-bold">{{ status()!.openingSalesInvoiceCount }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="text-muted small">{{ 'OpeningPurchaseInvoices' | abpLocalization }}</div>
                <div class="fs-4 fw-bold">{{ status()!.openingPurchaseInvoiceCount }}</div>
              </div>
            </div>
          </div>
          @if (status()!.isBalanced) {
          <div class="mt-3">
            <div class="alert alert-success mb-0">
              <i class="fa fa-check-circle me-2"></i>{{ 'OpeningEntriesBalanced' | abpLocalization }}
            </div>
          </div>
          } @else {
          <div class="mt-3">
            <div class="alert alert-warning mb-0">
              <i class="fa fa-exclamation-triangle me-2"></i>{{ status()!.message }}
            </div>
          </div>
          }
        </div>
        }
      </div>

      <!-- Opening Journal Entry Form -->
      <div class="card mb-4">
        <div class="card-header">
          <h6 class="card-title mb-0"><i class="fa fa-book me-2"></i>{{ 'OpeningJournalEntry' | abpLocalization }}</h6>
          <small class="text-muted">{{ 'OpeningJEDescription' | abpLocalization }}</small>
        </div>
        <div class="card-body">
          <form [formGroup]="jeForm" (ngSubmit)="createJournalEntry()">
            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
                <input type="date" class="form-control" formControlName="postingDate" />
              </div>
              <div class="col-md-8">
                <label class="form-label">{{ 'Remarks' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="remarks" placeholder="Opening Balance Entry" />
              </div>
            </div>

            <!-- Lines -->
            <table class="table table-sm table-bordered">
              <thead class="table-light">
                <tr>
                  <th>{{ 'Account' | abpLocalization }}</th>
                  <th class="text-end" style="width: 150px">{{ 'Debit' | abpLocalization }}</th>
                  <th class="text-end" style="width: 150px">{{ 'Credit' | abpLocalization }}</th>
                  <th style="width: 50px"></th>
                </tr>
              </thead>
              <tbody formArrayName="lines">
                @for (line of jeLines.controls; track $index) {
                  <tr [formGroupName]="$index">
                    <td>
                      <select class="form-select form-select-sm" formControlName="accountId">
                        <option value="">{{ 'SelectAccount' | abpLocalization }}...</option>
                        @for (account of bsAccounts(); track account.id) {
                          <option [value]="account.id">{{ account.accountCode }} - {{ account.accountName }}</option>
                        }
                      </select>
                    </td>
                    <td><input type="number" class="form-control form-control-sm text-end" formControlName="debit" step="0.01" /></td>
                    <td><input type="number" class="form-control form-control-sm text-end" formControlName="credit" step="0.01" /></td>
                    <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeLine($index)"><i class="fa fa-times"></i></button></td>
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr>
                  <td>
                    <button type="button" class="btn btn-sm btn-outline-primary" (click)="addLine()">
                      <i class="fa fa-plus me-1"></i>{{ 'AddLine' | abpLocalization }}
                    </button>
                  </td>
                  <td class="text-end fw-bold">{{ totalDebit() | number:'1.2-2' }}</td>
                  <td class="text-end fw-bold">{{ totalCredit() | number:'1.2-2' }}</td>
                  <td></td>
                </tr>
                @if (difference() !== 0) {
                <tr>
                  <td class="text-end text-muted">{{ 'TemporaryOpening' | abpLocalization }}</td>
                  <td class="text-end text-muted">{{ difference() < 0 ? (-difference() | number:'1.2-2') : '' }}</td>
                  <td class="text-end text-muted">{{ difference() > 0 ? (difference() | number:'1.2-2') : '' }}</td>
                  <td></td>
                </tr>
                }
              </tfoot>
            </table>

            <button type="submit" class="btn btn-primary" [disabled]="saving() || jeLines.length === 0">
              <i class="fa fa-save me-1"></i>{{ 'CreateOpeningEntry' | abpLocalization }}
            </button>
          </form>
        </div>
      </div>

      <!-- Opening Invoices Section -->
      <div class="card">
        <div class="card-header">
          <h6 class="card-title mb-0"><i class="fa fa-file-invoice me-2"></i>{{ 'OpeningInvoices' | abpLocalization }}</h6>
          <small class="text-muted">{{ 'OpeningInvoicesDescription' | abpLocalization }}</small>
        </div>
        <div class="card-body">
          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
              <input type="date" class="form-control" [(ngModel)]="invoicePostingDate" [ngModelOptions]="{standalone: true}" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'InvoiceType' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="invoiceType" [ngModelOptions]="{standalone: true}">
                <option value="sales">{{ 'SalesInvoices' | abpLocalization }} ({{ 'Receivables' | abpLocalization }})</option>
                <option value="purchase">{{ 'PurchaseInvoices' | abpLocalization }} ({{ 'Payables' | abpLocalization }})</option>
              </select>
            </div>
          </div>
          <div class="alert alert-info">
            <i class="fa fa-info-circle me-2"></i>
            {{ 'OpeningInvoiceHelp' | abpLocalization }}
          </div>
        </div>
      </div>
    </abp-page>
  `
})
export class OpeningBalanceComponent implements OnInit {
  private openingBalanceService = inject(OpeningBalanceService);
  private restService = inject(RestService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  status = signal<OpeningStatusDto | null>(null);
  bsAccounts = signal<AccountDto[]>([]);
  saving = signal(false);
  invoicePostingDate = '';
  invoiceType = 'sales';

  jeForm = this.fb.group({
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    remarks: [''],
    lines: this.fb.array([])
  });

  get jeLines(): FormArray {
    return this.jeForm.get('lines') as FormArray;
  }

  totalDebit = signal(0);
  totalCredit = signal(0);
  difference = signal(0);

  ngOnInit(): void {
    this.addLine();
    this.addLine();
    this.addLine();
    this.loadStatus();
    this.loadAccounts();
  }

  loadStatus(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.openingBalanceService.getOpeningStatus(companyId)
      .subscribe(status => this.status.set(status));
  }

  loadAccounts(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    // Only load Balance Sheet accounts (types 0-2: Asset, Liability, Equity)
    this.restService.request<any, any>({ method: 'GET', url: '/api/app/account', params: { companyId, maxResultCount: '500' } }, { apiName: 'Default' })
      .subscribe(result => {
        const accounts = (result.items || []).filter((a: AccountDto) =>
          a.accountType <= 2 && !a.accountCode?.startsWith('0')  // BS accounts only
        );
        this.bsAccounts.set(accounts);
      });
  }

  addLine(): void {
    this.jeLines.push(this.fb.group({
      accountId: ['', Validators.required],
      debit: [0],
      credit: [0]
    }));
  }

  removeLine(index: number): void {
    this.jeLines.removeAt(index);
    this.recalcTotals();
  }

  recalcTotals(): void {
    let debit = 0, credit = 0;
    for (const line of this.jeLines.controls) {
      debit += +(line.get('debit')?.value || 0);
      credit += +(line.get('credit')?.value || 0);
    }
    this.totalDebit.set(debit);
    this.totalCredit.set(credit);
    this.difference.set(debit - credit);
  }

  createJournalEntry(): void {
    this.recalcTotals();
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    const lines = this.jeLines.controls
      .map(c => ({
        accountId: c.get('accountId')?.value,
        debit: +(c.get('debit')?.value || 0),
        credit: +(c.get('credit')?.value || 0)
      }))
      .filter(l => l.accountId && (l.debit > 0 || l.credit > 0));

    if (lines.length === 0) {
      this.toaster.warn('Please add at least one line with a debit or credit amount.');
      return;
    }

    this.saving.set(true);
    this.openingBalanceService.createOpeningJournalEntry({
      companyId,
      postingDate: this.jeForm.get('postingDate')?.value,
      remarks: this.jeForm.get('remarks')?.value,
      lines
    } as any).subscribe({
      next: (result) => {
        this.toaster.success(result.message);
        this.saving.set(false);
        this.loadStatus();
        // Reset form
        this.jeLines.clear();
        this.addLine();
        this.addLine();
        this.addLine();
      },
      error: () => this.saving.set(false)
    });
  }
}
