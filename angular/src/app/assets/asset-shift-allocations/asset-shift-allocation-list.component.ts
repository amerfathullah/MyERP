import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { AssetShiftAllocationService } from '../../proxy/assets/asset-shift-allocation.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import type { AssetShiftAllocationDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-shift-allocation-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'AssetShiftAllocations' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'AssetShiftAllocations' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/shift-allocations/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewAssetShiftAllocation' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-layer-group fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoAssetShiftAllocationsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'AllocationNumber' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
                <th>{{ 'Periods' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr>
                    <td class="fw-semibold">{{ item.allocationNumber }}</td>
                    <td>
                      @switch (item.status) {
                        @case (0) { <span class="badge bg-secondary">{{ 'Draft' | abpLocalization }}</span> }
                        @case (1) { <span class="badge bg-success">{{ 'Submitted' | abpLocalization }}</span> }
                        @case (4) { <span class="badge bg-danger">{{ 'Cancelled' | abpLocalization }}</span> }
                      }
                    </td>
                    <td>{{ item.lines?.length ?? 0 }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (item.status === 0) {
                          <button class="btn btn-outline-success" (click)="submit(item)">
                            <i class="fa fa-check me-1"></i>{{ 'Submit' | abpLocalization }}
                          </button>
                        }
                        @if (item.status === 1) {
                          <button class="btn btn-outline-danger" (click)="cancel(item)">
                            <i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
      <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize"
        [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class AssetShiftAllocationListComponent implements OnInit {
  private service = inject(AssetShiftAllocationService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items = signal<AssetShiftAllocationDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize } as any).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  submit(item: AssetShiftAllocationDto): void {
    this.confirmation.warn('::AreYouSureToSubmit', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.submit(item.id!).subscribe({
        next: () => { this.toaster.success('::SuccessfullySubmitted'); this.loadData(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Submit failed'),
      });
    });
  }

  cancel(item: AssetShiftAllocationDto): void {
    this.confirmation.warn('::AreYouSureToCancel', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(item.id!).subscribe({
        next: () => { this.toaster.success('::SuccessfullyCancelled'); this.loadData(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Cancel failed'),
      });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
