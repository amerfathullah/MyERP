import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface ProformaInvoice {
  id: string;
  proformaNumber: string;
  proformaDate: string;
  salesOrderId: string;
  salesOrderNumber?: string;
  customerId: string;
  customerName?: string;
  basedOn: number;
  grandTotal: number;
  totalQty: number;
  status: number;
  sentOn?: string;
}

@Component({
  selector: 'app-proforma-invoice-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent, StatusBadgeComponent],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'ProformaInvoices' | abpLocalization }}</h5>
        </div>
        <div class="card-body">
          <div class="row mb-3">
            <div class="col-md-4">
              <input class="form-control form-control-sm" [placeholder]="'::Placeholder:Search' | abpLocalization"
                [(ngModel)]="searchTerm" (keyup.enter)="loadData()">
            </div>
            <div class="col-md-3">
              <select class="form-select form-select-sm" [(ngModel)]="statusFilter" (change)="loadData()">
                <option value="">{{ 'AllStatuses' | abpLocalization }}</option>
                <option value="1">{{ 'Issued' | abpLocalization }}</option>
                <option value="2">{{ 'Cancelled' | abpLocalization }}</option>
              </select>
            </div>
          </div>

          @if (loading()) {
            <div class="text-center py-4"><i class="fas fa-spinner fa-spin"></i></div>
          } @else if (proformas().length === 0) {
            <div class="text-center text-muted py-5">
              <i class="fas fa-file-invoice fa-3x mb-3 opacity-25"></i>
              <p>{{ 'NoProformaInvoicesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-sm align-middle">
                <thead>
                  <tr>
                    <th>{{ 'ProformaNumber' | abpLocalization }}</th>
                    <th>{{ 'Date' | abpLocalization }}</th>
                    <th>{{ 'Customer' | abpLocalization }}</th>
                    <th>{{ 'SalesOrder' | abpLocalization }}</th>
                    <th>{{ 'Basis' | abpLocalization }}</th>
                    <th class="text-end">{{ 'GrandTotal' | abpLocalization }}</th>
                    <th>{{ 'Status' | abpLocalization }}</th>
                    <th>{{ 'Actions' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (p of proformas(); track p.id) {
                    <tr>
                      <td><a [routerLink]="['/sales/proforma-invoices', p.id]">{{ p.proformaNumber }}</a></td>
                      <td>{{ p.proformaDate | date:'dd/MM/yyyy' }}</td>
                      <td>{{ p.customerName || '—' }}</td>
                      <td><a [routerLink]="['/sales/orders', p.salesOrderId]">{{ p.salesOrderNumber || '—' }}</a></td>
                      <td><span class="badge" [class.bg-primary]="p.basedOn === 0" [class.bg-info]="p.basedOn === 1">
                        {{ p.basedOn === 0 ? 'Quantity' : 'Amount' }}
                      </span></td>
                      <td class="text-end fw-semibold">{{ p.grandTotal | number:'1.2-2' }}</td>
                      <td><app-status-badge [status]="getStatusLabel(p.status)" /></td>
                      <td>
                        @if (p.status === 1) {
                          <button class="btn btn-sm btn-outline-danger" (click)="cancel(p.id)"
                            title="Cancel"><i class="fas fa-times"></i></button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize" [currentPage]="currentPage"
              (pageChange)="onPageChange($event)"></app-pagination>
          }
        </div>
      </div>
    </div>
  `
})
export class ProformaInvoiceListComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  proformas = signal<ProformaInvoice[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  searchTerm = '';
  statusFilter = '';
  currentPage = 0;
  pageSize = 20;

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    const params: any = {
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize
    };
    if (this.searchTerm) params.filter = this.searchTerm;
    if (this.statusFilter) params.status = this.statusFilter;
    const cid = this.companyContext.currentCompanyId();
    if (cid) params.companyId = cid;

    this.http.get<any>('/api/app/proforma-invoice', { params }).subscribe({
      next: (res) => {
        this.proformas.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  cancel(id: string) {
    if (!confirm('Cancel this proforma invoice?')) return;
    this.http.post(`/api/app/proforma-invoice/${id}/cancel`, {}).subscribe({
      next: () => {
        this.toaster.success('Proforma Invoice cancelled');
        this.loadData();
      }
    });
  }

  onPageChange(event: any) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  getStatusLabel(status: number): string {
    return ['Draft', 'Issued', 'Cancelled'][status] ?? 'Unknown';
  }
}
