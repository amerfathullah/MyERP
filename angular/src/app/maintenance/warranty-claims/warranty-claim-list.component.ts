import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { WarrantyClaimService } from '../../proxy/maintenance/warranty-claim.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { Confirmation, ToasterService , ConfirmationService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-warranty-claim-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'WarrantyClaims' | abpLocalization">
      <div class="d-flex justify-content-between mb-3">
        <div class="d-flex gap-2">
          <input type="text" class="form-control form-control-sm" style="max-width: 250px"
            [placeholder]="'::Placeholder:Search' | abpLocalization"
            [(ngModel)]="searchTerm" (keyup.enter)="onSearch()">
          <select class="form-select form-select-sm" style="max-width: 160px"
            [(ngModel)]="statusFilter" (ngModelChange)="onSearch()">
            <option value="">{{ 'AllStatuses' | abpLocalization }}</option>
            <option value="0">Open</option>
            <option value="1">Work In Progress</option>
            <option value="2">Closed</option>
            <option value="3">Cancelled</option>
          </select>
        </div>
        <button class="btn btn-primary btn-sm" (click)="showCreateForm = !showCreateForm">
          <i class="fa fa-plus me-1"></i>{{ 'NewWarrantyClaim' | abpLocalization }}
        </button>
      </div>

      @if (showCreateForm) {
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'NewWarrantyClaim' | abpLocalization }}</h6>
          <div class="row g-2">
            <div class="col-md-4">
              <label class="form-label">{{ 'Customer' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="newClaim.customerId">
                <option value="">{{ '::Select' | abpLocalization }}</option>
                @for (c of customers(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'Item' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="newClaim.itemId">
                <option value="">{{ '::Select' | abpLocalization }}</option>
                @for (i of items(); track i.id) {
                  <option [value]="i.id">{{ i.itemCode }} - {{ i.itemName }}</option>
                }
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'ComplaintDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="newClaim.complaintDate">
            </div>
          </div>
          <div class="row g-2 mt-1">
            <div class="col-md-12">
              <label class="form-label">{{ 'Complaint' | abpLocalization }}</label>
              <textarea class="form-control form-control-sm" rows="2" [(ngModel)]="newClaim.complaint"></textarea>
            </div>
          </div>
          <div class="mt-2">
            <button class="btn btn-success btn-sm me-2" (click)="create()">
              <i class="fa fa-check me-1"></i>{{ 'Save' | abpLocalization }}
            </button>
            <button class="btn btn-outline-secondary btn-sm" (click)="showCreateForm = false">
              {{ 'Cancel' | abpLocalization }}
            </button>
          </div>
        </div></div>
      }

      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && claims.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-shield-halved fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoWarrantyClaimsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'ClaimNumber' | abpLocalization }}</th>
              <th>{{ 'Customer' | abpLocalization }}</th>
              <th>{{ 'Item' | abpLocalization }}</th>
              <th>{{ 'ComplaintDate' | abpLocalization }}</th>
              <th>{{ 'Warranty' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (c of claims; track c.id) {
                <tr>
                  <td><a [routerLink]="['/maintenance/warranty-claims', c.id]"><strong>{{ c.claimNumber }}</strong></a></td>
                  <td>{{ c.customerName || '—' }}</td>
                  <td>{{ c.itemName || '—' }}</td>
                  <td>{{ c.complaintDate | date:'dd/MM/yyyy' }}</td>
                  <td>
                    <span class="badge" [class]="c.isUnderWarranty ? 'bg-success' : 'bg-warning text-dark'">
                      {{ c.isUnderWarranty ? 'Under Warranty' : 'Expired' }}
                    </span>
                  </td>
                  <td>
                    <span class="badge" [class]="getStatusClass(c.status)">{{ getStatusName(c.status) }}</span>
                  </td>
                  <td>
                    @if (c.status === 0) {
                      <button class="btn btn-outline-primary btn-sm me-1" (click)="startWork(c)"
                        title="Start Work"><i class="fa fa-wrench"></i></button>
                    }
                    @if (c.status === 0 || c.status === 1) {
                      <button class="btn btn-outline-success btn-sm me-1" (click)="close(c)"
                        title="Close"><i class="fa fa-check"></i></button>
                      <button class="btn btn-outline-danger btn-sm" (click)="cancel(c)"
                        title="Cancel"><i class="fa fa-times"></i></button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `
})
export class WarrantyClaimListComponent implements OnInit {
  private claimService = inject(WarrantyClaimService);
  private customerService = inject(CustomerService);
  private itemServiceProxy = inject(ItemService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  claims: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 50;
  currentPage = 0;
  searchTerm = '';
  statusFilter = '';
  showCreateForm = false;
  customers = signal<any[]>([]);
  items = signal<any[]>([]);

  newClaim: any = { customerId: '', itemId: '', complaintDate: new Date().toISOString().split('T')[0], complaint: '' };

  private statusNames = ['Open', 'Work In Progress', 'Closed', 'Cancelled'];
  private statusClasses = ['bg-primary', 'bg-info', 'bg-success', 'bg-secondary'];

  ngOnInit() {
    this.loadData();
    this.customerService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe(
      (res: any) => this.customers.set(res.items ?? []));
    this.itemServiceProxy.getList({ skipCount: 0, maxResultCount: 500 } as any).subscribe(
      (res: any) => this.items.set(res.items ?? []));
  }

  loadData() {
    this.isLoading = true;
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    const cid = this.companyContext.currentCompanyId();
    if (cid) params.companyId = cid;
    if (this.searchTerm) params.filter = this.searchTerm;
    if (this.statusFilter) params.status = this.statusFilter;
    this.claimService.getList(params as any).subscribe({
      next: (res: any) => { this.claims = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  onSearch() { this.currentPage = 0; this.loadData(); }
  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
  getStatusName(s: number) { return this.statusNames[s] ?? 'Unknown'; }
  getStatusClass(s: number) { return this.statusClasses[s] ?? 'bg-secondary'; }

  create() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.newClaim.customerId || !this.newClaim.itemId) {
      this.toaster.warn('::PleaseFillAllRequiredFields');
      return;
    }
    this.claimService.create({
      ...this.newClaim, companyId
    } as any).subscribe({
      next: () => { this.toaster.success('::SuccessfullyCreated'); this.showCreateForm = false; this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
    });
  }

  startWork(c: any) {
    this.claimService.startWork(c.id).subscribe({
      next: () => { this.toaster.success('::SuccessfullyStarted'); this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
    });
  }

  showResolutionInput = signal(false);
  resolutionText = '';
  closingClaimId: string | null = null;

  close(c: any) {
    this.closingClaimId = c.id;
    this.resolutionText = '';
    this.showResolutionInput.set(true);
  }

  confirmClose(): void {
    if (!this.closingClaimId) return;
    this.claimService.close(this.closingClaimId!, this.resolutionText).subscribe({
      next: () => { this.toaster.success('::SuccessfullyClosed'); this.showResolutionInput.set(false); this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
    });
  }

  cancel(c: any) {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.claimService.cancel(c.id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyCancelled'); this.loadData(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
      });
    });
  }
}
