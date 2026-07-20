import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { BatchService } from '../../proxy/inventory/batch.service';
import type { BatchDto } from '../../proxy/inventory/models';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-batch-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'Batches' | abpLocalization">
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-boxes-stacked fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoBatchesYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'BatchNo' | abpLocalization }}</th>
              <th>{{ 'Item' | abpLocalization }}</th>
              <th>{{ 'ExpiryDate' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (b of items; track b.id) {
                <tr>
                  <td class="font-monospace">{{ b.batchNo }}</td>
                  <td>{{ b.itemId | slice:0:8 }}…</td>
                  <td>{{ b.expiryDate ? (b.expiryDate | date:'dd/MM/yyyy') : '—' }}</td>
                  <td><span class="badge" [class]="b.isDisabled ? 'bg-danger' : 'bg-success'">{{ b.isDisabled ? 'Disabled' : 'Active' }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
      <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
  </abp-page>
  `,
})
export class BatchListComponent implements OnInit {
  private service = inject(BatchService);
  items: BatchDto[] = [];
  isLoading = false;
  totalCount = 0;

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize })
      .subscribe({ next: (r) => { this.items = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}