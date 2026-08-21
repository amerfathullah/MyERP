import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { RepostAccountingLedgerService } from '../../proxy/accounting/repost-accounting-ledger.service';
import type { RepostAccountingLedgerDto, RepostAccountingLedgerVoucherInputDto } from '../../proxy/accounting/models';

const STATUS = ['Draft', 'Queued', 'InProgress', 'PartiallyReposted', 'Completed', 'Failed', 'Cancelled'] as const;
const VOUCHER_STATUS = ['Pending', 'Reposted', 'Failed', 'Skipped'] as const;

interface PendingVoucherRow extends RepostAccountingLedgerVoucherInputDto {
  voucherNumber: string;
}

@Component({
  selector: 'app-repost-accounting-ledger',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent, LoadingOverlayComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card mb-3">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fa fa-sync me-2"></i>{{ 'RepostAccountingLedger' | abpLocalization }}</h5>
          @if (!showCreateForm) {
            <button class="btn btn-primary btn-sm" (click)="openCreateForm()">
              <i class="fa fa-plus me-1"></i>{{ 'New' | abpLocalization }}
            </button>
          }
        </div>
        <div class="card-body py-2">
          <small class="text-muted">{{ 'RepostAccountingLedgerHint' | abpLocalization }}</small>
        </div>
      </div>

      @if (showCreateForm) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">{{ 'NewRepostAccountingLedger' | abpLocalization }}</h6>
            <button class="btn-close" (click)="showCreateForm = false"></button>
          </div>
          <div class="card-body">
            <div class="row g-2 align-items-end mb-3">
              <div class="col-auto">
                <label class="form-label">{{ 'VoucherType' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="pickType">
                  @for (t of allowedTypes; track t) { <option [value]="t">{{ t }}</option> }
                </select>
              </div>
              <div class="col-auto">
                <label class="form-label">{{ 'VoucherNumber' | abpLocalization }}</label>
                <input class="form-control form-control-sm" [(ngModel)]="pickNumber"
                  (keyup.enter)="addVoucher()" placeholder="e.g. SI-00001" />
              </div>
              <div class="col-auto">
                <button class="btn btn-sm btn-outline-primary" [disabled]="!pickNumber || resolving" (click)="addVoucher()">
                  @if (resolving) { <span class="spinner-border spinner-border-sm me-1"></span> }
                  <i class="fa fa-plus"></i> {{ 'Add' | abpLocalization }}
                </button>
              </div>
            </div>

            @if (pendingVouchers.length === 0) {
              <p class="text-muted">{{ 'NoVouchersAddedYet' | abpLocalization }}</p>
            } @else {
              <table class="table table-sm table-hover mb-3">
                <thead><tr><th>{{ 'VoucherType' | abpLocalization }}</th><th>{{ 'VoucherNumber' | abpLocalization }}</th><th></th></tr></thead>
                <tbody>
                  @for (v of pendingVouchers; track v.voucherId) {
                    <tr>
                      <td>{{ v.voucherType }}</td>
                      <td>{{ v.voucherNumber }}</td>
                      <td class="text-end">
                        <button class="btn btn-sm btn-outline-danger" (click)="removeVoucher(v)"><i class="fa fa-times"></i></button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }

            <div class="d-flex gap-2">
              <button class="btn btn-primary" [disabled]="creating || pendingVouchers.length === 0" (click)="createDraft()">
                @if (creating) { <span class="spinner-border spinner-border-sm me-1"></span> }
                <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
              </button>
              <button class="btn btn-secondary" (click)="showCreateForm = false">{{ 'Cancel' | abpLocalization }}</button>
            </div>
          </div>
        </div>
      }

      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0 && !showCreateForm) {
        <div class="text-center py-5">
          <i class="fa fa-sync fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoRepostAccountingLedgerYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading && items.length > 0) {
        <div class="card"><div class="card-body p-0">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th></th>
              <th>{{ 'Date' | abpLocalization }}</th>
              <th>{{ 'Vouchers' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (item of items; track item.id) {
                <tr style="cursor:pointer" (click)="toggleExpand(item)">
                  <td><i class="fa" [ngClass]="expandedId === item.id ? 'fa-chevron-down' : 'fa-chevron-right'"></i></td>
                  <td>{{ item.creationTime | date:'dd/MM/yyyy HH:mm' }}</td>
                  <td>{{ item.vouchers.length }}</td>
                  <td><span class="badge" [ngClass]="statusClass(item.status)">{{ STATUS[item.status] }}</span></td>
                  <td (click)="$event.stopPropagation()">
                    <div class="btn-group btn-group-sm">
                      @if (item.status === 0) {
                        <button class="btn btn-outline-success" (click)="submitDoc(item)" title="Submit"><i class="fa fa-check"></i></button>
                      }
                      @if (item.status === 0 || item.status === 1) {
                        <button class="btn btn-outline-danger" (click)="cancelDoc(item)" title="Cancel"><i class="fa fa-ban"></i></button>
                      }
                      @if (item.status === 2) {
                        <button class="btn btn-outline-secondary" (click)="refresh(item)" title="Refresh"><i class="fa fa-rotate"></i></button>
                      }
                    </div>
                  </td>
                </tr>
                @if (expandedId === item.id) {
                  <tr>
                    <td colspan="5" class="bg-light">
                      <div class="p-3">
                        @if (item.errorLog) {
                          <p class="text-danger"><i class="fa fa-exclamation-triangle me-1"></i>{{ item.errorLog }}</p>
                        }
                        <table class="table table-sm mb-0">
                          <thead><tr><th>{{ 'VoucherType' | abpLocalization }}</th><th>{{ 'VoucherNumber' | abpLocalization }}</th><th>{{ 'Status' | abpLocalization }}</th><th>{{ 'Error' | abpLocalization }}</th></tr></thead>
                          <tbody>
                            @for (v of item.vouchers; track v.id) {
                              <tr>
                                <td>{{ v.voucherType }}</td>
                                <td>{{ v.voucherNumber }}</td>
                                <td><span class="badge" [ngClass]="voucherStatusClass(v.status)">{{ VOUCHER_STATUS[v.status] }}</span></td>
                                <td class="text-danger small">{{ v.errorMessage }}</td>
                              </tr>
                            }
                          </tbody>
                        </table>
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
export class RepostAccountingLedgerComponent implements OnInit {
  private service = inject(RepostAccountingLedgerService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  items: RepostAccountingLedgerDto[] = [];
  allowedTypes: string[] = [];
  pendingVouchers: PendingVoucherRow[] = [];
  isLoading = false;
  creating = false;
  resolving = false;
  showCreateForm = false;
  expandedId: string | null = null;
  pickType = '';
  pickNumber = '';
  STATUS = STATUS;
  VOUCHER_STATUS = VOUCHER_STATUS;

  ngOnInit() {
    this.loadData();
    this.service.getAllowedVoucherTypes().subscribe({
      next: types => { this.allowedTypes = types ?? []; this.pickType = this.allowedTypes[0] ?? ''; },
      error: () => {},
    });
  }

  loadData() {
    this.isLoading = true;
    const cid = this.companyContext.currentCompanyId();
    this.service.getList({ companyId: cid ?? undefined, maxResultCount: 50 } as any).subscribe({
      next: res => { this.items = res.items ?? []; this.isLoading = false; },
      error: () => { this.isLoading = false; },
    });
  }

  openCreateForm() {
    this.showCreateForm = true;
    this.expandedId = null;
    this.pendingVouchers = [];
  }

  addVoucher() {
    if (!this.pickNumber || !this.pickType) return;
    this.resolving = true;
    this.service.resolveVoucher(this.pickType, this.pickNumber).subscribe({
      next: v => {
        this.resolving = false;
        if (this.pendingVouchers.some(x => x.voucherType === v.voucherType && x.voucherId === v.voucherId)) {
          this.toaster.warn('AlreadyAdded');
          return;
        }
        this.pendingVouchers.push({ voucherType: v.voucherType, voucherId: v.voucherId, voucherNumber: v.voucherNumber });
        this.pickNumber = '';
      },
      error: (err: any) => {
        this.resolving = false;
        this.toaster.error(err?.error?.error?.message || 'OperationFailed');
      },
    });
  }

  removeVoucher(v: PendingVoucherRow) {
    this.pendingVouchers = this.pendingVouchers.filter(x => x !== v);
  }

  createDraft() {
    const cid = this.companyContext.currentCompanyId();
    if (!cid) {
      this.toaster.warn('SelectCompanyFirst');
      return;
    }
    this.creating = true;
    this.service.create({
      companyId: cid,
      vouchers: this.pendingVouchers.map(v => ({ voucherType: v.voucherType, voucherId: v.voucherId })),
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

  toggleExpand(item: RepostAccountingLedgerDto) {
    this.expandedId = this.expandedId === item.id ? null : (item.id ?? null);
  }

  submitDoc(item: RepostAccountingLedgerDto) {
    this.service.submit(item.id!).subscribe({
      next: () => { this.toaster.success('SuccessfullySubmitted'); this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || 'OperationFailed'),
    });
  }

  cancelDoc(item: RepostAccountingLedgerDto) {
    this.confirmation.warn('DeleteConfirmation', 'AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(item.id!).subscribe({
        next: () => { this.toaster.success('SuccessfullyCancelled'); this.loadData(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || 'OperationFailed'),
      });
    });
  }

  refresh(item: RepostAccountingLedgerDto) {
    this.service.get(item.id!).subscribe({
      next: dto => { this.items = this.items.map(i => i.id === dto.id ? dto : i); },
      error: () => {},
    });
  }

  statusClass(status: number): string {
    return ['bg-secondary', 'bg-info', 'bg-warning text-dark', 'bg-warning text-dark', 'bg-success', 'bg-danger', 'bg-dark'][status] ?? 'bg-secondary';
  }

  voucherStatusClass(status: number): string {
    return ['bg-secondary', 'bg-success', 'bg-danger', 'bg-dark'][status] ?? 'bg-secondary';
  }
}
