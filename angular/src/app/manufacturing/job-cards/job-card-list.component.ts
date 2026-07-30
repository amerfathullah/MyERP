import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { JobCardService } from '../../proxy/manufacturing/job-card.service';
import type { JobCardDto } from '../../proxy/manufacturing/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';

@Component({
  selector: 'app-job-card-list',
  standalone: true,
  imports: [PaginationComponent, SortableHeaderComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'JobCards' | abpLocalization">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <span class="text-muted small">{{ totalCount }} {{ '::Results' | abpLocalization }}</span>
      </div>
      <div class="card">
        <div class="card-body">
          <div class="row mb-3 align-items-center">
            <div class="col-auto">
              <select class="form-select form-select-sm" style="width: 170px;" [(ngModel)]="statusFilter" (change)="onStatusChange()">
                <option value="">{{ '::AllStatuses' | abpLocalization }}</option>
                <option value="0">{{ '::Open' | abpLocalization }}</option>
                <option value="1">{{ '::WorkInProgress' | abpLocalization }}</option>
                <option value="3">{{ '::Completed' | abpLocalization }}</option>
                <option value="4">{{ '::OnHold' | abpLocalization }}</option>
                <option value="5">{{ '::Cancelled' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-auto">
              <div class="input-group input-group-sm" style="width: 250px;">
                <span class="input-group-text"><i class="fa fa-search"></i></span>
                <input type="text" class="form-control"
                  [placeholder]="'::Search' | abpLocalization"
                  [ngModel]="searchTerm" (ngModelChange)="onSearch($event)" />
              </div>
            </div>
          </div>

          @if (isLoading) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (jobCards.length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-id-card fa-3x text-muted mb-3 d-block"></i>
              <h6 class="text-muted">{{ '::NoRecordsFound' | abpLocalization }}</h6>
              <p class="text-muted small">{{ 'NoJobCardsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover w-100">
                <thead>
                  <tr>
                    <th style="min-width: 80px;">
                      <app-sortable-header field="sequenceId" [currentField]="sortField" [currentDirection]="sortDirection" (sort)="onSort($event)">
                        #
                      </app-sortable-header>
                    </th>
                    <th>{{ '::Operation' | abpLocalization }}</th>
                    <th style="width: 140px;">{{ '::Manufacturing:Progress' | abpLocalization }}</th>
                    <th class="text-end" style="width: 100px;">
                      <app-sortable-header field="totalTimeInMins" [currentField]="sortField" [currentDirection]="sortDirection" (sort)="onSort($event)">
                        {{ '::TimeSpent' | abpLocalization }}
                      </app-sortable-header>
                    </th>
                    <th style="width: 130px;">{{ '::Status' | abpLocalization }}</th>
                    <th class="text-end" style="width: 80px;"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (jc of jobCards; track jc.id) {
                    <tr>
                      <td class="fw-semibold">
                        <a [routerLink]="['/manufacturing/job-cards', jc.id]" class="text-primary text-decoration-none">#{{ jc.sequenceId }}</a>
                      </td>
                      <td>
                        {{ jc.operationId }}
                        @if (jc.isCorrective) {
                          <span class="badge bg-warning text-dark ms-1">{{ '::Corrective' | abpLocalization }}</span>
                        }
                      </td>
                      <td>
                        <div class="d-flex align-items-center gap-1">
                          <div class="progress flex-grow-1" style="height: 5px;">
                            <div class="progress-bar" [style.width.%]="getProgressPct(jc)"
                              [class.bg-success]="getProgressPct(jc) >= 100"
                              [class.bg-primary]="getProgressPct(jc) < 100"></div>
                          </div>
                          <small class="font-monospace">{{ jc.completedQty ?? 0 | number:'1.0-0' }}/{{ jc.forQuantity | number:'1.0-0' }}</small>
                        </div>
                      </td>
                      <td class="text-end font-monospace">{{ jc.totalTimeInMins | number:'1.0-0' }} min</td>
                      <td><span class="badge" [ngClass]="getStatusClass(jc.status)">{{ getStatusLabel(jc.status) }}</span></td>
                      <td class="text-end">
                        <a [routerLink]="['/manufacturing/job-cards', jc.id]" class="btn btn-sm btn-outline-primary" title="View">
                          <i class="fa fa-eye"></i>
                        </a>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
      <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class JobCardListComponent implements OnInit {
  private service = inject(JobCardService);
  jobCards: JobCardDto[] = [];
  isLoading = false;

  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'sequenceId';
  sortDirection: 'asc' | 'desc' = 'asc';
  totalCount = 0;

  ngOnInit(): void { this.loadData(); }

  loadData() {
    this.isLoading = true;
    const params: any = {
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : '',
    };
    if (this.searchTerm) params.filter = this.searchTerm;
    if (this.statusFilter) params.status = this.statusFilter;
    this.service.getList(params)
      .subscribe({ next: (r) => { this.jobCards = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  onSearch(term: string): void { this.searchTerm = term; this.currentPage = 0; this.loadData(); }

  onStatusChange(): void { this.currentPage = 0; this.loadData(); }

  onSort(event: SortEvent): void {
    this.sortField = event.field;
    this.sortDirection = event.direction;
    this.currentPage = 0;
    this.loadData();
  }

  getStatusLabel(s: number | undefined): string {
    return ['Open', 'Work In Progress', 'Material Transferred', 'Completed', 'On Hold', 'Cancelled'][s ?? 0] ?? 'Open';
  }

  getStatusClass(s: number | undefined): string {
    return ['bg-secondary', 'bg-primary', 'bg-info', 'bg-success', 'bg-warning', 'bg-danger'][s ?? 0] ?? 'bg-secondary';
  }

  getProgressPct(jc: JobCardDto): number {
    return jc.forQuantity ? Math.min(100, ((jc.completedQty ?? 0) / jc.forQuantity) * 100) : 0;
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
