import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProjectService } from '../../../proxy/projects/project.service';
import { ProjectStatus, projectStatusOptions } from '../../../proxy/projects/project-status.enum';
import type {
  ProjectSummaryReportDto,
  ProjectSummaryRowDto,
} from '../../../proxy/projects/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { formatDayBookVoucherDate } from '../../../accounting/reports/day-book/day-book.component';

export function formatProjectStatusLabel(status: ProjectStatus | undefined): string {
  switch (status) {
    case ProjectStatus.Open:
      return 'Open';
    case ProjectStatus.Completed:
      return 'Completed';
    case ProjectStatus.Cancelled:
      return 'Cancelled';
    default:
      return 'Open';
  }
}

@Component({
  selector: 'app-project-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './project-summary.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ProjectSummaryComponent implements OnInit {
  private router = inject(Router);
  private projectService = inject(ProjectService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  readonly statusOptions = projectStatusOptions;
  readonly ProjectStatus = ProjectStatus;

  report = signal<ProjectSummaryReportDto | null>(null);
  loading = signal(false);

  selectedStatus: ProjectStatus | null = null;

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.projectService
      .getProjectSummaryReport({
        companyId,
        status: this.selectedStatus,
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

  statusLabel(status: ProjectStatus | undefined): string {
    return formatProjectStatusLabel(status);
  }

  openProject(row: ProjectSummaryRowDto): void {
    if (!row.projectId) return;
    this.router.navigate(['/projects', row.projectId]);
  }

  exportCsv(): void {
    const rows = this.report()?.rows ?? [];
    if (rows.length === 0) return;

    const data = rows.map(row => ({
      Project: row.projectNumber ?? '',
      'Project Name': row.projectName ?? '',
      Type: row.projectTypeName ?? '',
      Status: this.statusLabel(row.status),
      'Total Tasks': row.totalTasks ?? 0,
      'Tasks Completed': row.completedTasks ?? 0,
      'Tasks Overdue': row.overdueTasks ?? 0,
      'Completion %': (row.percentComplete ?? 0).toFixed(1),
      'Start Date': this.formatDate(row.expectedStartDate),
      'End Date': this.formatDate(row.expectedEndDate),
    }));
    exportToCsv('project-summary.csv', data, [
      'Project',
      'Project Name',
      'Type',
      'Status',
      'Total Tasks',
      'Tasks Completed',
      'Tasks Overdue',
      'Completion %',
      'Start Date',
      'End Date',
    ]);
  }
}
