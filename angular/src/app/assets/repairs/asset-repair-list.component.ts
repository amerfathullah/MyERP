import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface AssetRepairDto {
  id: string;
  assetId: string;
  repairDescription: string | null;
  failureDate: string | null;
  completionDate: string | null;
  repairCost: number;
  capitalizeRepairCost: boolean;
  increaseInAssetLife: number;
  status: number;
  creationTime: string;
}

@Component({
  selector: 'app-asset-repair-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'AssetRepairs' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'AssetRepairs' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/repairs/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewRepair' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-wrench fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoAssetRepairsYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/assets/repairs/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewRepair' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'Asset' | abpLocalization }}</th>
                <th>{{ 'Description' | abpLocalization }}</th>
                <th>{{ 'FailureDate' | abpLocalization }}</th>
                <th class="text-end">{{ 'RepairCost' | abpLocalization }}</th>
                <th>{{ 'Capitalize' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (r of items(); track r.id) {
                  <tr>
                    <td class="font-monospace">{{ r.assetId | slice:0:8 }}…</td>
                    <td class="text-truncate" style="max-width:200px">{{ r.repairDescription ?? '—' }}</td>
                    <td>{{ r.failureDate ? (r.failureDate | date:'dd/MM/yyyy') : '—' }}</td>
                    <td class="text-end fw-semibold">{{ r.repairCost | number:'1.2-2' }}</td>
                    <td>
                      @if (r.capitalizeRepairCost) {
                        <span class="badge bg-info">Yes (+{{ r.increaseInAssetLife }}mo)</span>
                      } @else {
                        <span class="badge bg-secondary">No</span>
                      }
                    </td>
                    <td>
                      <span class="badge"
                        [class.bg-warning]="r.status === 0"
                        [class.bg-success]="r.status === 1"
                        [class.bg-danger]="r.status === 2">
                        {{ r.status === 0 ? 'Pending' : r.status === 1 ? 'Completed' : 'Cancelled' }}
                      </span>
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (r.status === 0) {
                          <button class="btn btn-outline-success" (click)="completeRepair(r)">
                            <i class="fa fa-check"></i>
                          </button>
                          <button class="btn btn-outline-danger" (click)="cancelRepair(r)">
                            <i class="fa fa-times"></i>
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
export class AssetRepairListComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);

  items = signal<AssetRepairDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.isLoading.set(true);
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    const cid = this.companyContext.currentCompanyId();
    if (cid) params.companyId = cid;

    this.http.get<{ items: AssetRepairDto[]; totalCount: number }>(
      '/api/app/asset-repair', { params }
    ).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  completeRepair(repair: AssetRepairDto): void {
    if (!confirm('Complete this repair?')) return;
    this.http.post(`/api/app/asset-repair/${repair.id}/complete`, {}).subscribe({
      next: () => this.loadData(),
    });
  }

  cancelRepair(repair: AssetRepairDto): void {
    if (!confirm('Cancel this repair?')) return;
    this.http.post(`/api/app/asset-repair/${repair.id}/cancel`, {}).subscribe({
      next: () => this.loadData(),
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
