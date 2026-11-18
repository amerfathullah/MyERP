import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { TimesheetService } from '../../../proxy/projects/timesheet.service';
import type {
  TimesheetBillingSummaryReportDto,
  TimesheetBillingSummaryRowDto,
} from '../../../proxy/projects/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { formatDayBookVoucherDate } from '../../../accounting/reports/day-book/day-book.component';

@Component({
  selector: 'app-timesheet-billing-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './timesheet-billing-summary.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class TimesheetBillingSummaryComponent implements OnInit {
  private router = inject(Router);
  private timesheetService = inject(TimesheetService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  report = signal<TimesheetBillingSummaryReportDto | null>(null);
  loading = signal(false);

  fromDate = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];
  includeDraftTimesheets = false;

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.timesheetService
      .getTimesheetBillingSummary({
        companyId,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        includeDraftTimesheets: this.includeDraftTimesheets,
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

  formatDate(value: string | Date | undefined | null): string {
    return formatDayBookVoucherDate(value);
  }

  openTimesheet(row: TimesheetBillingSummaryRowDto): void {
    if (!row.timesheetId) return;
    this.router.navigate(['/projects/timesheets', row.timesheetId]);
  }

  openProject(row: TimesheetBillingSummaryRowDto, event: Event): void {
    event.stopPropagation();
    if (!row.projectId) return;
    this.router.navigate(['/projects', row.projectId]);
  }

  exportCsv(): void {
    const rows = this.report()?.rows ?? [];
    if (rows.length === 0) return;

    const currency = this.report()?.currencyCode ?? 'MYR';
    const data = rows.map(row => ({
      Date: this.formatDate(row.date),
      Timesheet: row.timesheetLabel ?? '',
      Employee: row.employeeName ?? '',
      Project: row.projectName ?? '',
      'Working Hours': (row.workingHours ?? 0).toFixed(2),
      'Billing Hours': (row.billingHours ?? 0).toFixed(2),
      'Billing Amount': (row.billingAmount ?? 0).toFixed(2),
      Currency: currency,
    }));
    exportToCsv('timesheet-billing-summary.csv', data, [
      'Date',
      'Timesheet',
      'Employee',
      'Project',
      'Working Hours',
      'Billing Hours',
      'Billing Amount',
      'Currency',
    ]);
  }
}
