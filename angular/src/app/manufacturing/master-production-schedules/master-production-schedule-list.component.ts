import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { MasterProductionScheduleService } from '../../proxy/manufacturing/master-production-schedule.service';
import type { MasterProductionScheduleDto } from '../../proxy/manufacturing/models';

@Component({
  selector: 'app-master-production-schedule-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent, StatusBadgeComponent],
  template: `
    <abp-page [title]="'MasterProductionSchedules' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/manufacturing/master-production-schedules/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewMasterProductionSchedule' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) {
        <app-loading-overlay />
      }

      @if (!isLoading && schedules.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-calendar fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoMasterProductionSchedulesYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'ScheduleNumber' | abpLocalization }}</th>
                  <th>{{ 'PostingDate' | abpLocalization }}</th>
                  <th>{{ 'FromDate' | abpLocalization }}</th>
                  <th>{{ 'ToDate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'PlannedItems' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (s of schedules; track s.id) {
                  <tr>
                    <td>{{ s.scheduleNumber }}</td>
                    <td>{{ s.postingDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ s.fromDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ s.toDate ? (s.toDate | date:'dd/MM/yyyy') : '—' }}</td>
                    <td class="text-end">{{ s.items?.length ?? 0 }}</td>
                    <td><app-status-badge [status]="statusLabel(s.status)"></app-status-badge></td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/manufacturing/master-production-schedules', s.id]">
                          <i class="fa fa-eye"></i>
                        </a>
                        @if ((s.status ?? 0) === 0) {
                          <button class="btn btn-outline-danger" (click)="remove(s)"><i class="fa fa-trash"></i></button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <app-pagination
        [totalCount]="totalCount"
        [pageSize]="pageSize"
        [currentPage]="currentPage"
        (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class MasterProductionScheduleListComponent implements OnInit {
  private service = inject(MasterProductionScheduleService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  schedules: MasterProductionScheduleDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: (result) => {
        this.schedules = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  statusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', '', '', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  remove(s: MasterProductionScheduleDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(s.id!).subscribe(() => {
        this.toaster.success('::SuccessfullyDeleted');
        this.load();
      });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}
