import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DunningService, type DunningDto } from '../../proxy/sales/sales-advanced.service';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-dunning-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'Dunnings' | abpLocalization">
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-bell fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoDunningsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Customer' | abpLocalization }}</th>
              <th>{{ 'Level' | abpLocalization }}</th>
              <th>{{ 'Date' | abpLocalization }}</th>
              <th class="text-end">{{ 'Outstanding' | abpLocalization }}</th>
              <th class="text-end">{{ 'GrandTotal' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (d of items; track d.id) {
                <tr>
                  <td>{{ d.customerName ?? '—' }}</td>
                  <td><span class="badge bg-warning">{{ 'Level' | abpLocalization }} {{ d.dunningLevel }}</span></td>
                  <td>{{ d.postingDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end">{{ d.totalOutstanding | number:'1.2-2' }}</td>
                  <td class="text-end fw-bold">{{ d.grandTotal | number:'1.2-2' }}</td>
                  <td><span class="badge" [ngClass]="{'bg-secondary':d.status===0, 'bg-primary':d.status===1, 'bg-success':d.status===3, 'bg-danger':d.status===4}">
                    {{ ['Draft','Submitted','','Resolved','Cancelled'][d.status ?? 0] }}
                  </span></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
      <app-pagination [totalCount]="0" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
  </abp-page>
  `,
})
export class DunningListComponent implements OnInit {
  private service = inject(DunningService);
  items: DunningDto[] = [];
  isLoading = false;

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 50 })
      .subscribe({ next: (r) => { this.items = r.items ?? []; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; /* reload handled by store */; }
}