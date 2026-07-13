import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { BatchProxyService, type BatchDto } from '../../proxy/inventory/inventory-additional.service';

@Component({
  selector: 'app-batch-list',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
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
    </abp-page>
  `,
})
export class BatchListComponent implements OnInit {
  private service = inject(BatchProxyService);
  items: BatchDto[] = [];
  isLoading = false;

  ngOnInit(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 50 })
      .subscribe({ next: (r) => { this.items = r.items ?? []; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }
}
