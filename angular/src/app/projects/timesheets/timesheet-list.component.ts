import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { TimesheetService, TimesheetDto } from '../../proxy/projects/timesheet.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-timesheet-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'Timesheets' | abpLocalization">
      <div class="d-flex justify-content-end mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/projects/timesheets/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewTimesheet' | abpLocalization }}
        </button>
      </div>
      @if (isLoading()) { <app-loading-overlay /> }
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
                  <td>{{ ts.employeeName ?? ts.employeeId }}</td>
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
      }
    </abp-page>
  `,
})
export class TimesheetListComponent implements OnInit {
  private service = inject(TimesheetService);
  items = signal<TimesheetDto[]>([]);
  isLoading = signal(false);

  ngOnInit() {
    this.isLoading.set(true);
    this.service.getList({ skipCount: 0, maxResultCount: 20 }).subscribe({
      next: r => { this.items.set(r.items ?? []); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  getStatus(s: number | undefined): string {
    return ['Draft', 'Submitted', 'Billed', 'Cancelled'][s ?? 0] ?? 'Draft';
  }
}
