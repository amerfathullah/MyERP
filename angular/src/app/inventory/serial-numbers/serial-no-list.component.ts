import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { SerialNoService } from '../../proxy/inventory/serial-no.service';
import type { SerialNoDto } from '../../proxy/inventory/models';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-serial-no-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'SerialNumbers' | abpLocalization">
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-barcode fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoSerialNumbersYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'SerialNumber' | abpLocalization }}</th>
              <th>{{ 'Item' | abpLocalization }}</th>
              <th>{{ 'Warehouse' | abpLocalization }}</th>
              <th>{{ 'MaintenanceStatus' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (sn of items; track sn.id) {
                <tr>
                  <td class="font-monospace">{{ sn.serialNumber }}</td>
                  <td>{{ sn.itemId | slice:0:8 }}…</td>
                  <td>{{ sn.warehouseId ? (sn.warehouseId | slice:0:8) + '…' : '—' }}</td>
                  <td>{{ sn.maintenanceStatus }}</td>
                  <td><span class="badge" [ngClass]="{'bg-success':sn.status===0, 'bg-info':sn.status===1, 'bg-secondary':sn.status===2}">
                    {{ ['Active','Delivered','Inactive'][sn.status ?? 0] ?? 'Active' }}
                  </span></td>
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
export class SerialNoListComponent implements OnInit {
  private service = inject(SerialNoService);
  items: SerialNoDto[] = [];
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