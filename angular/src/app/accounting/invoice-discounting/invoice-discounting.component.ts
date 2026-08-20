import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { InvoiceDiscountingService } from '../../proxy/accounting/invoice-discounting.service';
import { AccountService } from '../../proxy/accounting/account.service';
import type { AccountDto } from '../../proxy/accounting/models';
import type { InvoiceDiscountingDto, InvoiceForDiscountingDto } from '../../proxy/accounting/models';

const STATUS = ['Draft', 'Sanctioned', 'Disbursed', 'Settled', 'Cancelled'] as const;

interface PledgeRow extends InvoiceForDiscountingDto {
  selected: boolean;
  pledgeAmount: number;
}

@Component({
  selector: 'app-invoice-discounting',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent, LoadingOverlayComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card mb-3">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fa fa-percent me-2"></i>{{ 'InvoiceDiscounting' | abpLocalization }}</h5>
          @if (!showCreateForm) {
            <button class="btn btn-primary btn-sm" (click)="openCreateForm()">
              <i class="fa fa-plus me-1"></i>{{ 'New' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      @if (showCreateForm) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">{{ 'NewInvoiceDiscounting' | abpLocalization }}</h6>
            <button class="btn-close" (click)="showCreateForm = false"></button>
          </div>
          <div class="card-body">
            <div class="row g-3 mb-3">
              <div class="col-md-3">
                <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
                <input type="date" class="form-control" [(ngModel)]="draft.postingDate" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'ShortTermLoanAccount' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="draft.shortTermLoanAccountId">
                  <option value="">-</option>
                  @for (a of accounts; track a.id) { <option [value]="a.id">{{ a.accountName }}</option> }
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'BankAccount' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="draft.bankAccountId">
                  <option value="">-</option>
                  @for (a of accounts; track a.id) { <option [value]="a.id">{{ a.accountName }}</option> }
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'BankChargesAccount' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="draft.bankChargesAccountId">
                  <option value="">-</option>
                  @for (a of accounts; track a.id) { <option [value]="a.id">{{ a.accountName }}</option> }
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'AccountsReceivableCreditAccount' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="draft.accountsReceivableCreditAccountId">
                  <option value="">-</option>
                  @for (a of accounts; track a.id) { <option [value]="a.id">{{ a.accountName }}</option> }
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'AccountsReceivableDiscountedAccount' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="draft.accountsReceivableDiscountedAccountId">
                  <option value="">-</option>
                  @for (a of accounts; track a.id) { <option [value]="a.id">{{ a.accountName }}</option> }
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'AccountsReceivableUnpaidAccount' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="draft.accountsReceivableUnpaidAccountId">
                  <option value="">-</option>
                  @for (a of accounts; track a.id) { <option [value]="a.id">{{ a.accountName }}</option> }
                </select>
              </div>
            </div>

            <h6 class="mb-2">{{ 'EligibleInvoices' | abpLocalization }}</h6>
            @if (loadingEligible) {
              <app-loading-overlay />
            } @else if (eligibleInvoices.length === 0) {
              <p class="text-muted">{{ 'NoEligibleInvoices' | abpLocalization }}</p>
            } @else {
              <table class="table table-sm table-hover mb-2">
                <thead>
                  <tr>
                    <th style="width:40px"></th>
                    <th>{{ 'InvoiceNumber' | abpLocalization }}</th>
                    <th>{{ 'Customer' | abpLocalization }}</th>
                    <th class="text-end">{{ 'OutstandingAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ 'PledgeAmount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of eligibleInvoices; track row.invoiceId) {
                    <tr>
                      <td><input type="checkbox" class="form-check-input" [(ngModel)]="row.selected" /></td>
                      <td>{{ row.invoiceNumber }}</td>
                      <td>{{ row.customerName }}</td>
                      <td class="text-end">{{ row.outstandingAmount | number:'1.2-2' }}</td>
                      <td class="text-end" style="width:140px">
                        <input type="number" class="form-control form-control-sm text-end"
                          [(ngModel)]="row.pledgeAmount" [disabled]="!row.selected" [max]="row.outstandingAmount" min="0" />
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <p class="text-end fw-bold mb-3">{{ 'Total' | abpLocalization }}: {{ selectedTotal() | number:'1.2-2' }}</p>
            }

            <div class="d-flex gap-2">
              <button class="btn btn-primary" [disabled]="creating || selectedTotal() <= 0" (click)="createDraft()">
                @if (creating) { <span class="spinner-border spinner-border-sm me-1"></span> }
                <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
              </button>
              <button class="btn btn-secondary" (click)="showCreateForm = false">{{ 'Cancel' | abpLocalization }}</button>
            </div>
          </div>
        </div>
      }

      <!-- History -->
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0 && !showCreateForm) {
        <div class="text-center py-5">
          <i class="fa fa-percent fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoInvoiceDiscountingYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading && items.length > 0) {
        <div class="card"><div class="card-body p-0">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th></th>
              <th>{{ 'PostingDate' | abpLocalization }}</th>
              <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
              <th>{{ 'LoanPeriod' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (item of items; track item.id) {
                <tr style="cursor:pointer" (click)="toggleExpand(item)">
                  <td><i class="fa" [ngClass]="expandedId === item.id ? 'fa-chevron-down' : 'fa-chevron-right'"></i></td>
                  <td>{{ item.postingDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end">{{ item.totalAmount | number:'1.2-2' }}</td>
                  <td>{{ item.loanStartDate ? (item.loanStartDate | date:'dd/MM/yyyy') + ' - ' + (item.loanEndDate | date:'dd/MM/yyyy') : '-' }}</td>
                  <td><span class="badge" [ngClass]="statusClass(item.status)">{{ STATUS[item.status ?? 0] }}</span></td>
                  <td (click)="$event.stopPropagation()">
                    <div class="btn-group btn-group-sm">
                      @if (item.status === 0) {
                        <button class="btn btn-outline-success" (click)="openSubmit(item)" title="Submit"><i class="fa fa-check"></i></button>
                      }
                      @if (item.status === 1) {
                        <button class="btn btn-outline-primary" (click)="openDisburse(item)" title="Disburse"><i class="fa fa-money-bill-wave"></i></button>
                      }
                      @if (item.status === 2) {
                        <button class="btn btn-outline-info" (click)="settle(item)" title="Settle"><i class="fa fa-handshake"></i></button>
                      }
                      @if (item.status === 0 || item.status === 1 || item.status === 2) {
                        <button class="btn btn-outline-danger" (click)="cancelDoc(item)" title="Cancel"><i class="fa fa-ban"></i></button>
                      }
                    </div>
                  </td>
                </tr>
                @if (expandedId === item.id) {
                  <tr>
                    <td colspan="6" class="bg-light">
                      <div class="p-3">
                        @if (item.invoices?.length) {
                          <table class="table table-sm mb-3">
                            <thead><tr><th>{{ 'InvoiceNumber' | abpLocalization }}</th><th>{{ 'Customer' | abpLocalization }}</th><th class="text-end">{{ 'PledgedAmount' | abpLocalization }}</th></tr></thead>
                            <tbody>
                              @for (inv of item.invoices; track inv.salesInvoiceId) {
                                <tr><td>{{ inv.invoiceNumber }}</td><td>{{ inv.customerName }}</td><td class="text-end">{{ inv.outstandingAmount | number:'1.2-2' }}</td></tr>
                              }
                            </tbody>
                          </table>
                        }

                        @if (submitTargetId === item.id) {
                          <div class="row g-2 align-items-end">
                            <div class="col-auto">
                              <label class="form-label">{{ 'LoanStartDate' | abpLocalization }}</label>
                              <input type="date" class="form-control form-control-sm" [(ngModel)]="submitForm.loanStartDate" />
                            </div>
                            <div class="col-auto">
                              <label class="form-label">{{ 'LoanPeriodDays' | abpLocalization }}</label>
                              <input type="number" class="form-control form-control-sm" [(ngModel)]="submitForm.loanPeriodDays" />
                            </div>
                            <div class="col-auto">
                              <button class="btn btn-sm btn-success" (click)="submit(item)">{{ 'Confirm' | abpLocalization }}</button>
                              <button class="btn btn-sm btn-secondary" (click)="submitTargetId = null">{{ 'Cancel' | abpLocalization }}</button>
                            </div>
                          </div>
                        }

                        @if (disburseTargetId === item.id) {
                          <div class="row g-2 align-items-end">
                            <div class="col-auto">
                              <label class="form-label">{{ 'BankCharges' | abpLocalization }}</label>
                              <input type="number" class="form-control form-control-sm" [(ngModel)]="disburseForm.bankCharges" />
                            </div>
                            <div class="col-auto">
                              <button class="btn btn-sm btn-success" (click)="disburse(item)">{{ 'Confirm' | abpLocalization }}</button>
                              <button class="btn btn-sm btn-secondary" (click)="disburseTargetId = null">{{ 'Cancel' | abpLocalization }}</button>
                            </div>
                          </div>
                        }
                      </div>
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div></div>
      }
    </div>
  `
})
export class InvoiceDiscountingComponent implements OnInit {
  private invoiceDiscountingService = inject(InvoiceDiscountingService);
  private accountService = inject(AccountService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  items: InvoiceDiscountingDto[] = [];
  accounts: AccountDto[] = [];
  eligibleInvoices: PledgeRow[] = [];
  isLoading = false;
  loadingEligible = false;
  creating = false;
  showCreateForm = false;
  expandedId: string | null = null;
  submitTargetId: string | null = null;
  disburseTargetId: string | null = null;
  STATUS = STATUS;

  draft = {
    postingDate: new Date().toISOString().substring(0, 10),
    shortTermLoanAccountId: '',
    bankAccountId: '',
    bankChargesAccountId: '',
    accountsReceivableCreditAccountId: '',
    accountsReceivableDiscountedAccountId: '',
    accountsReceivableUnpaidAccountId: '',
  };

  submitForm = { loanStartDate: new Date().toISOString().substring(0, 10), loanPeriodDays: 90 };
  disburseForm = { bankCharges: 0 };

  ngOnInit() {
    this.loadData();
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' } as any).subscribe({
      next: res => { this.accounts = res.items ?? []; },
      error: () => {},
    });
  }

  loadData() {
    this.isLoading = true;
    const cid = this.companyContext.currentCompanyId();
    this.invoiceDiscountingService.getList({ maxResultCount: 50 } as any, cid || undefined).subscribe({
      next: res => { this.items = res.items ?? []; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  openCreateForm() {
    this.showCreateForm = true;
    this.expandedId = null;
    const cid = this.companyContext.currentCompanyId();
    if (!cid) {
      this.toaster.warn('SelectCompanyFirst');
      return;
    }
    this.loadingEligible = true;
    this.invoiceDiscountingService.getEligibleInvoices(cid).subscribe({
      next: res => {
        this.eligibleInvoices = (res ?? []).map(r => ({ ...r, selected: false, pledgeAmount: r.outstandingAmount }));
        this.loadingEligible = false;
      },
      error: () => { this.loadingEligible = false; },
    });
  }

  selectedTotal(): number {
    return this.eligibleInvoices.filter(r => r.selected).reduce((sum, r) => sum + (r.pledgeAmount || 0), 0);
  }

  createDraft() {
    const cid = this.companyContext.currentCompanyId();
    const selected = this.eligibleInvoices.filter(r => r.selected && r.pledgeAmount > 0);
    if (selected.length === 0) return;

    this.creating = true;
    this.invoiceDiscountingService.create({
      companyId: cid,
      postingDate: this.draft.postingDate,
      shortTermLoanAccountId: this.draft.shortTermLoanAccountId,
      bankAccountId: this.draft.bankAccountId,
      bankChargesAccountId: this.draft.bankChargesAccountId,
      accountsReceivableCreditAccountId: this.draft.accountsReceivableCreditAccountId,
      accountsReceivableDiscountedAccountId: this.draft.accountsReceivableDiscountedAccountId,
      accountsReceivableUnpaidAccountId: this.draft.accountsReceivableUnpaidAccountId,
      invoices: selected.map(r => ({ salesInvoiceId: r.invoiceId, outstandingAmount: r.pledgeAmount })),
    }).subscribe({
      next: () => {
        this.toaster.success('SuccessfullyCreated');
        this.creating = false;
        this.showCreateForm = false;
        this.loadData();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || 'OperationFailed');
        this.creating = false;
      },
    });
  }

  toggleExpand(item: InvoiceDiscountingDto) {
    this.expandedId = this.expandedId === item.id ? null : (item.id ?? null);
    this.submitTargetId = null;
    this.disburseTargetId = null;
  }

  openSubmit(item: InvoiceDiscountingDto) {
    this.expandedId = item.id ?? null;
    this.submitTargetId = item.id ?? null;
    this.disburseTargetId = null;
  }

  submit(item: InvoiceDiscountingDto) {
    this.invoiceDiscountingService.submit(item.id!, this.submitForm).subscribe({
      next: () => { this.toaster.success('SuccessfullySubmitted'); this.submitTargetId = null; this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || 'OperationFailed'),
    });
  }

  openDisburse(item: InvoiceDiscountingDto) {
    this.expandedId = item.id ?? null;
    this.disburseTargetId = item.id ?? null;
    this.submitTargetId = null;
  }

  disburse(item: InvoiceDiscountingDto) {
    this.invoiceDiscountingService.disburse(item.id!, this.disburseForm).subscribe({
      next: () => { this.toaster.success('SuccessfullyDisbursed'); this.disburseTargetId = null; this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || 'OperationFailed'),
    });
  }

  settle(item: InvoiceDiscountingDto) {
    this.invoiceDiscountingService.settle(item.id!).subscribe({
      next: () => { this.toaster.success('SuccessfullyClosed'); this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || 'OperationFailed'),
    });
  }

  cancelDoc(item: InvoiceDiscountingDto) {
    this.confirmation.warn('DeleteConfirmation', 'AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.invoiceDiscountingService.cancel(item.id!).subscribe({
        next: () => { this.toaster.success('SuccessfullyCancelled'); this.loadData(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || 'OperationFailed'),
      });
    });
  }

  statusClass(status?: number): string {
    return ['bg-warning text-dark', 'bg-info', 'bg-primary', 'bg-success', 'bg-secondary'][status ?? 0];
  }
}
