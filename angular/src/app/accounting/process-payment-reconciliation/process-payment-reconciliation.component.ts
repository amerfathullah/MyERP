import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ProcessPaymentReconciliationService } from '../../proxy/accounting/process-payment-reconciliation.service';
import type { ProcessPaymentReconciliationDto } from '../../proxy/accounting/models';

const STATUS = ['Draft', 'Queued', 'Running', 'Completed', 'PartiallyReconciled', 'Failed', 'Cancelled'] as const;

@Component({
  selector: 'app-process-payment-reconciliation',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent, LoadingOverlayComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card mb-3">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fa fa-robot me-2"></i>{{ 'ProcessPaymentReconciliation' | abpLocalization }}</h5>
          @if (!showCreateForm) {
            <button class="btn btn-primary btn-sm" (click)="showCreateForm = true">
              <i class="fa fa-plus me-1"></i>{{ 'New' | abpLocalization }}
            </button>
          }
        </div>
        <div class="card-body py-2">
          <small class="text-muted">{{ 'ProcessPaymentReconciliationHint' | abpLocalization }}</small>
        </div>
      </div>

      @if (showCreateForm) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">{{ 'NewProcessPaymentReconciliation' | abpLocalization }}</h6>
            <button class="btn-close" (click)="showCreateForm = false"></button>
          </div>
          <div class="card-body">
            <div class="row g-2 mb-3">
              <div class="col-md-3">
                <label class="form-label">{{ 'PartyType' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="draft.partyType">
                  <option value="Customer">{{ 'Customer' | abpLocalization }}</option>
                  <option value="Supplier">{{ 'Supplier' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'Party' | abpLocalization }}</label>
                <input class="form-control form-control-sm" [(ngModel)]="draft.partyId" [placeholder]="'::Placeholder:EnterPartyId' | abpLocalization" />
              </div>
              <div class="col-md-5">
                <label class="form-label">{{ 'ReceivablePayableAccount' | abpLocalization }}</label>
                <input class="form-control form-control-sm" [(ngModel)]="draft.receivablePayableAccountId" placeholder="Account Id" />
              </div>
            </div>
            <div class="d-flex gap-2">
              <button class="btn btn-primary" [disabled]="creating || !draft.partyId || !draft.receivablePayableAccountId" (click)="createDraft()">
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
          <i class="fa fa-robot fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoProcessPaymentReconciliationYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading && items.length > 0) {
        <div class="card"><div class="card-body p-0">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Date' | abpLocalization }}</th>
              <th>{{ 'PartyType' | abpLocalization }}</th>
              <th>{{ 'Party' | abpLocalization }}</th>
              <th class="text-end">{{ 'Reconciled' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (item of items; track item.id) {
                <tr>
                  <td>{{ item.creationTime | date:'dd/MM/yyyy HH:mm' }}</td>
                  <td><span class="badge bg-info">{{ item.partyType }}</span></td>
                  <td class="small text-muted">{{ item.partyId }}</td>
                  <td class="text-end">{{ item.reconciledCount }}</td>
                  <td>
                    <span class="badge" [ngClass]="statusClass(item.status)">{{ STATUS[item.status] }}</span>
                    @if (item.errorLog) {
                      <i class="fa fa-exclamation-triangle text-danger ms-1" [title]="item.errorLog"></i>
                    }
                  </td>
                  <td class="text-end">
                    <div class="btn-group btn-group-sm">
                      @if (item.status === 0) {
                        <button class="btn btn-outline-success" (click)="submitItem(item)" title="Submit"><i class="fa fa-check"></i></button>
                      }
                      @if (item.status === 0 || item.status === 1 || item.status === 2) {
                        <button class="btn btn-outline-danger" (click)="cancelItem(item)" title="Cancel"><i class="fa fa-ban"></i></button>
                      }
                      @if (item.status === 1 || item.status === 2) {
                        <button class="btn btn-outline-secondary" (click)="refresh(item)" title="Refresh"><i class="fa fa-rotate"></i></button>
                      }
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </div>
  `
})
export class ProcessPaymentReconciliationComponent implements OnInit {
  private service = inject(ProcessPaymentReconciliationService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  items: ProcessPaymentReconciliationDto[] = [];
  isLoading = false;
  creating = false;
  showCreateForm = false;
  STATUS = STATUS;

  draft = {
    partyType: 'Customer',
    partyId: '',
    receivablePayableAccountId: '',
  };

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.isLoading = true;
    const cid = this.companyContext.currentCompanyId();
    this.service.getList({ companyId: cid ?? undefined, maxResultCount: 50 } as any).subscribe({
      next: res => { this.items = res.items ?? []; this.isLoading = false; },
      error: () => { this.isLoading = false; },
    });
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
      partyType: this.draft.partyType,
      partyId: this.draft.partyId,
      receivablePayableAccountId: this.draft.receivablePayableAccountId,
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

  submitItem(item: ProcessPaymentReconciliationDto) {
    this.service.submit(item.id!).subscribe({
      next: () => { this.toaster.success('SuccessfullySubmitted'); this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || 'OperationFailed'),
    });
  }

  cancelItem(item: ProcessPaymentReconciliationDto) {
    this.confirmation.warn('DeleteConfirmation', 'AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(item.id!).subscribe({
        next: () => { this.toaster.success('SuccessfullyCancelled'); this.loadData(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || 'OperationFailed'),
      });
    });
  }

  refresh(item: ProcessPaymentReconciliationDto) {
    this.service.get(item.id!).subscribe({
      next: dto => { this.items = this.items.map(i => i.id === dto.id ? dto : i); },
      error: () => {},
    });
  }

  statusClass(status: number): string {
    return ['bg-secondary', 'bg-info', 'bg-warning text-dark', 'bg-success', 'bg-primary', 'bg-danger', 'bg-dark'][status] ?? 'bg-secondary';
  }
}
