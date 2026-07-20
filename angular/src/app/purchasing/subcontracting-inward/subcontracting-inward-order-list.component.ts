import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SubcontractingInwardOrderService } from '../../proxy/purchasing/subcontracting-inward-order.service';
import { FormsModule } from '@angular/forms';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { SubcontractingInwardOrderDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-subcontracting-inward-order-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, FormsModule,
    PaginationComponent, StatusBadgeComponent],
  template: `
    <abp-page [title]="'SubcontractingInwardOrders' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'SubcontractingInwardOrders' | abpLocalization }}</h5>
        </div>
        <div class="card-body">
          <div class="row mb-3">
            <div class="col-md-4">
              <input type="text" class="form-control form-control-sm"
                [placeholder]="'Search' | abpLocalization"
                [(ngModel)]="searchTerm" (keyup.enter)="loadData()">
            </div>
            <div class="col-md-3">
              <select class="form-select form-select-sm" [(ngModel)]="statusFilter" (change)="loadData()">
                <option value="">{{ 'AllStatuses' | abpLocalization }}</option>
                <option value="Open">Open</option>
                <option value="PartiallyReceived">Partially Received</option>
                <option value="Completed">Completed</option>
                <option value="Closed">Closed</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </div>
          </div>

          @if (loading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin"></i></div>
          } @else if (entries().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-truck-ramp-box fa-3x mb-3 d-block"></i>
              <p>{{ 'NoSubcontractingInwardOrdersYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead>
                <tr>
                  <th>{{ 'OrderNumber' | abpLocalization }}</th>
                  <th>{{ 'Date' | abpLocalization }}</th>
                  <th>{{ 'Total' | abpLocalization }}</th>
                  <th>{{ 'Received' | abpLocalization }}</th>
                  <th>{{ 'Billed' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th>{{ 'Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (entry of entries(); track entry.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/purchasing/subcontracting-inward', entry.id]">
                        {{ entry.orderNumber }}
                      </a>
                    </td>
                    <td>{{ entry.orderDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ entry.grandTotal | number:'1.2-2' }}</td>
                    <td>
                      <div class="progress" style="height: 16px; width: 80px;">
                        <div class="progress-bar bg-info" [style.width.%]="entry.perReceived">
                          {{ entry.perReceived | number:'1.0-0' }}%
                        </div>
                      </div>
                    </td>
                    <td>
                      <div class="progress" style="height: 16px; width: 80px;">
                        <div class="progress-bar bg-success" [style.width.%]="entry.perBilled">
                          {{ entry.perBilled | number:'1.0-0' }}%
                        </div>
                      </div>
                    </td>
                    <td><app-status-badge [status]="getStatusLabel(entry.status)" /></td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (entry.status === 0) {
                          <button class="btn btn-outline-success" (click)="submit(entry.id)">
                            <i class="fa fa-paper-plane"></i>
                          </button>
                        }
                        @if (entry.status === 1 || entry.status === 2) {
                          <button class="btn btn-outline-dark" (click)="close(entry.id)">
                            <i class="fa fa-lock"></i>
                          </button>
                        }
                        @if (entry.status !== 4 && entry.status !== 5) {
                          <button class="btn btn-outline-danger" (click)="cancelEntry(entry.id)">
                            <i class="fa fa-times"></i>
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            <app-pagination [totalCount]="totalCount()" [pageSize]="10" [currentPage]="currentPage"
              (pageChange)="onPageChange($event)" />
          }
        </div>
      </div>
    </abp-page>
  `
})
export class SubcontractingInwardOrderListComponent implements OnInit {
  private scioService = inject(SubcontractingInwardOrderService);
  private companyContext = inject(CompanyContextService);

  entries = signal<SubcontractingInwardOrderDto[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  currentPage = 0;
  searchTerm = '';
  statusFilter = '';

  ngOnInit() { this.loadData(); }

  loadData() {
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { skipCount: this.currentPage * 10, maxResultCount: 10 };
    if (companyId) params.companyId = companyId;
    if (this.searchTerm) params.filter = this.searchTerm;
    if (this.statusFilter) params.status = this.statusFilter;
    this.scioService.getList({ skipCount: this.currentPage * 10, maxResultCount: 10, companyId: companyId || undefined, filter: this.searchTerm || undefined, status: this.statusFilter || undefined, sorting: '' } as any).subscribe({
      next: res => {
        this.entries.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  submit(id: string) {
    this.scioService.submit(id).subscribe({ next: () => this.loadData() });
  }

  close(id: string) {
    this.scioService.close(id).subscribe({ next: () => this.loadData() });
  }

  cancelEntry(id: string) {
    this.scioService.cancel(id).subscribe({ next: () => this.loadData() });
  }

  getStatusLabel(status: number): string {
    return ['Draft', 'Open', 'PartiallyReceived', 'Completed', 'Closed', 'Cancelled'][status] ?? 'Draft';
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }
}
