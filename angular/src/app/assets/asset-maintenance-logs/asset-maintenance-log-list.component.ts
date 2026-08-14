import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { AssetMaintenanceLogService } from '../../proxy/assets/asset-maintenance-log.service';
import { AssetMaintenanceStatus } from '../../proxy/assets/asset-maintenance-status.enum';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AssetMaintenanceLogDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-maintenance-log-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'AssetMaintenanceLogs' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'AssetMaintenanceLogs' | abpLocalization }}</h5>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-calendar-check-o fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoAssetMaintenanceLogsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'Asset' | abpLocalization }}</th>
                  <th>{{ 'Task' | abpLocalization }}</th>
                  <th>{{ 'DueDate' | abpLocalization }}</th>
                  <th>{{ 'Assignee' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th>{{ 'CompletionDate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Cost' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (log of items(); track log.id) {
                  <tr>
                    <td class="fw-semibold">{{ log.assetName || log.assetId }}</td>
                    <td>{{ log.maintenanceTask }}</td>
                    <td>{{ log.dueDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ log.assignToName || '—' }}</td>
                    <td>
                      @switch (log.maintenanceStatus) {
                        @case (AssetMaintenanceStatus.Planned) {
                          <span class="badge bg-primary">{{ 'Planned' | abpLocalization }}</span>
                        }
                        @case (AssetMaintenanceStatus.Completed) {
                          <span class="badge bg-success">{{ 'Completed' | abpLocalization }}</span>
                        }
                        @case (AssetMaintenanceStatus.Cancelled) {
                          <span class="badge bg-secondary">{{ 'Cancelled' | abpLocalization }}</span>
                        }
                        @case (AssetMaintenanceStatus.Overdue) {
                          <span class="badge bg-danger">{{ 'Overdue' | abpLocalization }}</span>
                        }
                      }
                    </td>
                    <td>{{ log.completionDate ? (log.completionDate | date:'dd/MM/yyyy') : '—' }}</td>
                    <td class="text-end fw-semibold">
                      {{ log.cost ? (log.cost | number:'1.2-2') : '—' }}
                    </td>
                    <td class="text-end">
                      <a class="btn btn-sm btn-outline-secondary me-1" [routerLink]="['/assets/maintenance-logs', log.id]">
                        <i class="fa fa-eye"></i>
                      </a>
                      @if (log.maintenanceStatus === AssetMaintenanceStatus.Planned || log.maintenanceStatus === AssetMaintenanceStatus.Overdue) {
                        <button class="btn btn-sm btn-outline-success me-1" (click)="completeLog(log.id!)" title="Complete">
                          <i class="fa fa-check"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-danger" (click)="cancelLog(log.id!)" title="Cancel">
                          <i class="fa fa-ban"></i>
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            <app-pagination
              [total]="totalCount()"
              [page]="page()"
              [pageSize]="pageSize"
              (pageChange)="onPageChange($event)">
            </app-pagination>
          }
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetMaintenanceLogListComponent implements OnInit {
  private service = inject(AssetMaintenanceLogService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  AssetMaintenanceStatus = AssetMaintenanceStatus;
  items = signal<AssetMaintenanceLogDto[]>([]);
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
    this.page.set(event.pageIndex);
    this.loadData();
  }

  completeLog(id: string) {
    this.service
      .complete(id, {
        completionDate: new Date().toISOString(),
        actionsPerformed: 'Maintenance task completed',
      })
      .subscribe({
        next: () => {
          this.toaster.success('::MaintenanceCompleted');
          this.loadData();
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Complete failed'),
      });
  }

  cancelLog(id: string) {
    this.confirmation.warn('::AreYouSure', '::ConfirmCancel').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.cancel(id).subscribe({
          next: () => {
            this.toaster.success('::MaintenanceCancelled');
            this.loadData();
          },
          error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Cancel failed'),
        });
      }
    });
  }
}
