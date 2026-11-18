import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { TimesheetService } from '../../../proxy/projects/timesheet.service';
import { TimesheetStatus } from '../../../proxy/projects/timesheet-status.enum';
import type {
  DailyTimesheetSummaryReportDto,
  DailyTimesheetSummaryRowDto,
} from '../../../proxy/projects/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { formatDayBookVoucherDate } from '../../../accounting/reports/day-book/day-book.component';

@Component({
  selector: 'app-daily-timesheet-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './daily-timesheet-summary.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class DailyTimesheetSummaryComponent implements OnInit {
  private router = inject(Router);
  private timesheetService = inject(TimesheetService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  report = signal<DailyTimesheetSummaryReportDto | null>(null);
  loading = signal(false);

  fromDate = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.timesheetService
      .getDailyTimesheetSummary({
        companyId,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: err => {
          this.loading.set(false);
          this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
        },
      });
  }

  formatDateTime(value: string | undefined | null): string {
    if (!value) return '';
    return String(value).replace('T', ' ').slice(0, 16);
  }

  statusLabel(status: TimesheetStatus | undefined): string {
    return TimesheetStatus[status ?? TimesheetStatus.Draft] ?? 'Draft';
  }

  openTimesheet(row: DailyTimesheetSummaryRowDto): void {
    if (!row.timesheetId) return;
    this.router.navigate(['/projects/timesheets', row.timesheetId]);
  }

  openProject(row: DailyTimesheetSummaryRowDto, event: Event): void {
    event.stopPropagation();
    if (!row.projectId) return;
    this.router.navigate(['/projects', row.projectId]);
  }

  exportCsv(): void {
    const rows = this.report()?.rows ?? [];
    if (rows.length === 0) return;

    const data = rows.map(row => ({
      Timesheet: row.timesheetLabel ?? '',
      'Employee Name': row.employeeName ?? '',
      'From Datetime': this.formatDateTime(row.fromTime),
      'To Datetime': this.formatDateTime(row.toTime),
      Hours: (row.hours ?? 0).toFixed(2),
      'Activity Type': row.activityType ?? '',
      Task: row.taskNumber ?? '',
      Project: row.projectName ?? '',
      Status: this.statusLabel(row.status),
    }));
    exportToCsv(`daily-timesheet-summary-${formatDayBookVoucherDate(new Date())}.csv`, data, [
      'Timesheet',
      'Employee Name',
      'From Datetime',
      'To Datetime',
      'Hours',
      'Activity Type',
      'Task',
      'Project',
      'Status',
    ]);
  }
}
