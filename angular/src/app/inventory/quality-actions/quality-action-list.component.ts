import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { QualityActionDto } from '../../proxy/inventory/models';
import { QualityActionStatus } from '../../proxy/inventory/quality-action-status.enum';
import { QualityActionType } from '../../proxy/inventory/quality-action-type.enum';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-quality-action-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'QualityActions' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div class="d-flex gap-2">
            <select class="form-select form-select-sm" style="max-width: 160px" [(ngModel)]="statusFilter" (change)="onFilter()">
              <option value="">{{ '::All' | abpLocalization }}</option>
              <option [value]="QualityActionStatus.Open">Open</option>
              <option [value]="QualityActionStatus.Resolved">Resolved</option>
              <option [value]="QualityActionStatus.Closed">Closed</option>
            </select>
          </div>
          <a routerLink="/inventory/quality-actions/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ 'NewQualityAction' | abpLocalization }}
          </a>
        </div>
        <div class="card-body p-0">
          <div class="table-responsive">
            <table class="table table-hover table-striped mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Type' | abpLocalization }}</th>
                  <th>{{ '::ProblemDescription' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::Resolution' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @if (isLoading) {
                  <tr>
                    <td colspan="5" class="text-center py-4">
                      <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
                    </td>
                  </tr>
                } @else if (filteredActions().length === 0) {
                  <tr>
                    <td colspan="5" class="text-center py-4 text-muted">
                      {{ '::NoData' | abpLocalization }}
                    </td>
                  </tr>
                } @else {
                  @for (action of filteredActions(); track action.id) {
                    <tr>
                      <td>
                        <span class="badge" [ngClass]="action.actionType === QualityActionType.Corrective ? 'bg-danger' : 'bg-info'">
                          {{ action.actionType === QualityActionType.Corrective ? 'Corrective' : 'Preventive' }}
                        </span>
                      </td>
                      <td class="fw-semibold">
                        <a [routerLink]="['/inventory/quality-actions', action.id]">{{ action.problemDescription }}</a>
                      </td>
                      <td>
                        <span class="badge" [ngClass]="getStatusBadgeClass(action.status)">
                          {{ getStatusLabel(action.status) }}
                        </span>
                      </td>
                      <td class="text-truncate" style="max-width: 250px">{{ action.resolution ?? '-' }}</td>
                      <td class="text-end">
                        <a [routerLink]="['/inventory/quality-actions', action.id]" class="btn btn-outline-secondary btn-sm">
                          <i class="fa fa-pencil"></i>
                        </a>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        </div>
        <div class="card-footer d-flex justify-content-between align-items-center">
          <span class="text-muted small">Total: {{ totalCount }}</span>
          <app-pagination
            [totalCount]="totalCount"
            [currentPage]="currentPage"
            [pageSize]="pageSize"
            (pageChange)="onPageChange($event)"
          ></app-pagination>
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityActionListComponent implements OnInit {
  private readonly service = inject(QualityManagementService);

  readonly QualityActionStatus = QualityActionStatus;
  readonly QualityActionType = QualityActionType;

  actions: QualityActionDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 10;
  statusFilter = '';

  ngOnInit() {
    this.loadActions();
  }

  loadActions() {
    this.isLoading = true;
    this.service.getActionList({
      maxResultCount: this.pageSize,
      skipCount: this.currentPage * this.pageSize,
    }).subscribe({
      next: (res) => {
        this.actions = res.items ?? [];
        this.totalCount = res.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  onFilter() {
    this.currentPage = 0;
    this.loadActions();
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadActions();
  }

  filteredActions() {
    if (this.statusFilter === '') return this.actions;
    return this.actions.filter(a => a.status === Number(this.statusFilter));
  }

  getStatusBadgeClass(status?: QualityActionStatus): string {
    switch (Number(status)) {
      case QualityActionStatus.Resolved:
        return 'bg-success';
      case QualityActionStatus.Closed:
        return 'bg-secondary';
      default:
        return 'bg-warning text-dark';
    }
  }

  getStatusLabel(status?: QualityActionStatus): string {
    switch (Number(status)) {
      case QualityActionStatus.Resolved:
        return 'Resolved';
      case QualityActionStatus.Closed:
        return 'Closed';
      default:
        return 'Open';
    }
  }
}
