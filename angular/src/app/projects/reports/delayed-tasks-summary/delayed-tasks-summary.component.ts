import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProjectService } from '../../../proxy/projects/project.service';
import { ProjectPriority, projectPriorityOptions } from '../../../proxy/projects/project-priority.enum';
import { ProjectTaskStatus, projectTaskStatusOptions } from '../../../proxy/projects/project-task-status.enum';
import type {
  DelayedTaskSummaryRowDto,
  DelayedTasksSummaryReportDto,
} from '../../../proxy/projects/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { formatDayBookVoucherDate } from '../../../accounting/reports/day-book/day-book.component';

@Component({
  selector: 'app-delayed-tasks-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './delayed-tasks-summary.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class DelayedTasksSummaryComponent implements OnInit {
  private router = inject(Router);
  private projectService = inject(ProjectService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  readonly priorityOptions = projectPriorityOptions;
  readonly statusOptions = projectTaskStatusOptions.filter(o => o.value !== ProjectTaskStatus.Cancelled);

  report = signal<DelayedTasksSummaryReportDto | null>(null);
  loading = signal(false);

  selectedPriority: ProjectPriority | null = null;
  selectedStatus: ProjectTaskStatus | null = null;
  fromDate = '';
  toDate = '';

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.projectService
      .getDelayedTasksSummary({
        companyId,
        priority: this.selectedPriority,
        status: this.selectedStatus,
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

  formatDate(value: string | Date | undefined | null): string {
    return formatDayBookVoucherDate(value);
  }

  statusLabel(status: ProjectTaskStatus | undefined): string {
    return ProjectTaskStatus[status ?? ProjectTaskStatus.Open] ?? 'Open';
  }

  priorityLabel(priority: ProjectPriority | undefined): string {
    return ProjectPriority[priority ?? ProjectPriority.Medium] ?? 'Medium';
  }

  openProject(row: DelayedTaskSummaryRowDto): void {
    if (!row.projectId) return;
    this.router.navigate(['/projects', row.projectId]);
  }

  exportCsv(): void {
    const rows = this.report()?.rows ?? [];
    if (rows.length === 0) return;

    const data = rows.map(row => ({
      Task: row.taskNumber ?? '',
      Subject: row.subject ?? '',
      Project: row.projectNumber ?? '',
      'Project Name': row.projectName ?? '',
      Status: this.statusLabel(row.status),
      Priority: this.priorityLabel(row.priority),
      'Progress (%)': (row.progress ?? 0).toFixed(1),
      'Expected Start Date': this.formatDate(row.expectedStartDate),
      'Expected End Date': this.formatDate(row.expectedEndDate),
      'Actual End Date': this.formatDate(row.actualEndDate),
      'Delay (Days)': row.delayInDays ?? 0,
    }));
    exportToCsv('delayed-tasks-summary.csv', data, [
      'Task',
      'Subject',
      'Project',
      'Project Name',
      'Status',
      'Priority',
      'Progress (%)',
      'Expected Start Date',
      'Expected End Date',
      'Actual End Date',
      'Delay (Days)',
    ]);
  }
}
