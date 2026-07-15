import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { TimesheetService } from '../../proxy/projects/timesheet.service';
import type { TimesheetDto } from '../../proxy/projects/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-timesheet-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, StatusBadgeComponent],
  template: `
    <abp-page [title]="'Timesheets' | abpLocalization">
      <div class="d-flex justify-content-between mb-3">
        <input type="text" class="form-control form-control-sm" style="width:200px"
          [(ngModel)]="searchTerm" (keyup.enter)="loadData()" placeholder="Search...">
        <button class="btn btn-primary btn-sm" routerLink="/projects/timesheets/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewTimesheet' | abpLocalization }}
        </button>
      </div>
      @if (isLoading()) { <div class="text-center py-3"><i class="fa fa-spinner fa-spin fa-2x"></i></div> }
      @if (!isLoading() && items().length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-clock fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoTimesheetsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading()) {
        <div class="table-responsive">
          <table class="table table-hover align-middle">
            <thead>
              <tr>
                <th>{{ 'Employee' | abpLocalization }}</th>
                <th>{{ 'StartDate' | abpLocalization }}</th>
                <th>{{ 'EndDate' | abpLocalization }}</th>
                <th class="text-end">{{ 'TotalHours' | abpLocalization }}</th>
                <th class="text-end">{{ 'BillingAmount' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @for (ts of items(); track ts.id) {
                <tr>
                  <td><a [routerLink]="['/projects/timesheets', ts.id]" class="text-decoration-none">{{ ts.employeeName ?? ts.employeeId }}</a></td>
                  <td>{{ ts.startDate | date:'dd/MM/yyyy' }}</td>
                  <td>{{ ts.endDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end">{{ ts.totalHours | number:'1.1-1' }}</td>
                  <td class="text-end">{{ ts.totalBillingAmount | number:'1.2-2' }}</td>
                  <td><app-status-badge [status]="getStatus(ts.status)" /></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `,
})
export class TimesheetListComponent implements OnInit {
  private service = inject(TimesheetService);
  items = signal<TimesheetDto[]>([]);
  isLoading = signal(false);
  searchTerm = '';
  totalCount = 0;
  currentPage = 0;
  pageSize = 20;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading.set(true);
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    if (this.searchTerm) params.filter = this.searchTerm;
    this.service.getList(params).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount = r.totalCount ?? 0; this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  getStatus(s: number | undefined): string {
    return ['Draft', 'Submitted', 'Billed', 'Cancelled'][s ?? 0] ?? 'Draft';
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
