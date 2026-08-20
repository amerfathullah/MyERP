import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { AssetMaintenanceService } from '../../proxy/assets/asset-maintenance.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AssetMaintenanceDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-maintenance-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'AssetMaintenances' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'AssetMaintenances' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/maintenances/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewMaintenance' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-cogs fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoAssetMaintenancesYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/assets/maintenances/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewMaintenance' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'Asset' | abpLocalization }}</th>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  <th>{{ 'Manager' | abpLocalization }}</th>
                  <th>{{ 'Team' | abpLocalization }}</th>
                  <th class="text-center">{{ 'Tasks' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (m of items(); track m.id) {
                  <tr>
                    <td class="fw-semibold">
                      <a [routerLink]="['/assets/maintenances', m.id, 'edit']">{{ m.assetName || m.assetId }}</a>
                    </td>
                    <td>{{ m.itemName || m.itemCode || '—' }}</td>
                    <td>{{ m.maintenanceManagerName || '—' }}</td>
                    <td>{{ m.maintenanceTeamName || '—' }}</td>
                    <td class="text-center">
                      <span class="badge bg-secondary">{{ m.tasks?.length || 0 }}</span>
                    </td>
                    <td class="text-end">
                      <a class="btn btn-sm btn-outline-secondary me-1" [routerLink]="['/assets/maintenances', m.id, 'edit']">
                        <i class="fa fa-pencil"></i>
                      </a>
                      <button class="btn btn-sm btn-outline-danger" (click)="deleteMaintenance(m.id!)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            <app-pagination
              [totalCount]="totalCount()"
              [currentPage]="page() - 1"
              [pageSize]="pageSize"
              (pageChange)="onPageChange($event)">
            </app-pagination>
          }
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetMaintenanceListComponent implements OnInit {
  private service = inject(AssetMaintenanceService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  items = signal<AssetMaintenanceDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  page = signal(1);
  pageSize = 10;

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId();
    this.service
      .getList({
        companyId: companyId || undefined,
        skipCount: (this.page() - 1) * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (res) => {
          this.items.set(res.items || []);
          this.totalCount.set(res.totalCount || 0);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  onPageChange(event: PageEvent) {
    this.page.set(event.pageIndex + 1);
    this.loadData();
  }

  deleteMaintenance(id: string) {
    this.confirmation.warn('::AreYouSure', '::ConfirmDelete').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.delete(id).subscribe(() => this.loadData());
      }
    });
  }
}
