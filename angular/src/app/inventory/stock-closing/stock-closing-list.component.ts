import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockClosingService } from '../../proxy/inventory/stock-closing.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

const STATUS = ['Draft', 'Submitted', 'Cancelled'] as const;

@Component({
  selector: 'app-stock-closing-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent, StatusBadgeComponent, PaginationComponent],
  template: `
    <abp-page [title]="'StockClosing' | abpLocalization">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div class="d-flex gap-2">
          <select class="form-select form-select-sm" style="width: 150px" [(ngModel)]="statusFilter" (change)="loadData()">
            <option value="">{{ 'AllStatuses' | abpLocalization }}</option>
            <option value="Draft">Draft</option>
            <option value="Submitted">Submitted</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
        <button class="btn btn-primary btn-sm" (click)="createNew()">
          <i class="fa fa-plus me-1"></i>{{ 'NewStockClosing' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-box-archive fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoStockClosingEntriesYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body p-0">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'PostingDate' | abpLocalization }}</th>
              <th class="text-end">{{ 'TotalEntries' | abpLocalization }}</th>
              <th class="text-end">{{ 'TotalStockValue' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (item of items; track item.id) {
                <tr>
                  <td>{{ item.postingDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end">{{ item.totalEntries }}</td>
                  <td class="text-end fw-bold">{{ item.totalStockValue | number:'1.2-2' }}</td>
                  <td><app-status-badge [status]="STATUS[item.status ?? 0]" /></td>
                  <td>
                    <div class="btn-group btn-group-sm">
                      @if (item.status === 0) {
                        <button class="btn btn-outline-primary" (click)="generate(item.id)" title="Generate">
                          <i class="fa fa-cogs"></i>
                        </button>
                        <button class="btn btn-outline-success" (click)="submit(item.id)" title="Submit">
                          <i class="fa fa-check"></i>
                        </button>
                      }
                      @if (item.status === 1) {
                        <button class="btn btn-outline-danger" (click)="cancelEntry(item.id)" title="Cancel">
                          <i class="fa fa-times"></i>
                        </button>
                      }
                    </div>
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
export class StockClosingListComponent implements OnInit {
  private service = inject(StockClosingService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  items: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;
  statusFilter = '';
  STATUS = STATUS;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    const cid = this.companyContext.currentCompanyId();
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize, companyId: cid ?? undefined, status: this.statusFilter || undefined } as any).subscribe({
      next: res => { this.items = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  createNew() {
    const cid = this.companyContext.currentCompanyId();
    if (!cid) { this.toaster.warn('Select a company first'); return; }
    this.service.generate({ companyId: cid, postingDate: new Date().toISOString().slice(0, 10) } as any).subscribe({
      next: () => { this.toaster.success('Stock Closing entry created'); this.loadData(); },
      error: () => {}
    });
  }

  generate(id: string) {
    this.service.generate({ id } as any).subscribe({
      next: () => { this.toaster.success('Balances generated'); this.loadData(); },
      error: () => {}
    });
  }

  submit(id: string) {
    this.service.submit(id).subscribe({
      next: () => { this.toaster.success('Stock Closing submitted'); this.loadData(); },
      error: () => {}
    });
  }

  cancelEntry(id: string) {
    if (!confirm('Cancel this stock closing entry?')) return;
    this.service.cancel(id).subscribe({
      next: () => { this.toaster.success('Cancelled'); this.loadData(); },
      error: () => {}
    });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.pageSize = e.pageSize; this.loadData(); }
}
