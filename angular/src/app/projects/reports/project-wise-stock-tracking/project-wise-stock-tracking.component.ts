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
  ProjectDto,
  ProjectWiseStockTrackingReportDto,
  ProjectWiseStockTrackingRowDto,
} from '../../../proxy/projects/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { formatDayBookVoucherDate } from '../../../accounting/reports/day-book/day-book.component';
import { formatProjectStatusLabel } from '../project-summary/project-summary.component';

@Component({
  selector: 'app-project-wise-stock-tracking',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './project-wise-stock-tracking.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ProjectWiseStockTrackingComponent implements OnInit {
  private router = inject(Router);
  private projectService = inject(ProjectService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  readonly statusOptions = projectStatusOptions;
  readonly ProjectStatus = ProjectStatus;

  report = signal<ProjectWiseStockTrackingReportDto | null>(null);
  projects = signal<ProjectDto[]>([]);
  loading = signal(false);

  selectedProjectId: string | null = null;
  selectedStatus: ProjectStatus | null = null;

  ngOnInit(): void {
    this.loadProjects();
    this.loadReport();
  }

  private loadProjects(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.projectService
      .getList({ companyId, skipCount: 0, maxResultCount: 200 })
      .subscribe({
        next: res => this.projects.set(res.items ?? []),
      });
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.projectService
      .getProjectWiseStockTracking({
        companyId,
        projectId: this.selectedProjectId,
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

  openProject(row: ProjectWiseStockTrackingRowDto): void {
    if (!row.projectId) return;
    this.router.navigate(['/projects', row.projectId]);
  }

  exportCsv(): void {
    const rows = this.report()?.rows ?? [];
    if (rows.length === 0) return;

    const data = rows.map(row => ({
      'Project Id': row.projectNumber ?? '',
      'Cost of Purchased Items': (row.costOfPurchasedItems ?? 0).toFixed(2),
      'Cost of Issued Items': (row.costOfIssuedItems ?? 0).toFixed(2),
      'Cost of Delivered Items': (row.costOfDeliveredItems ?? 0).toFixed(2),
      'Project Name': row.projectName ?? '',
      'Project Status': this.statusLabel(row.status),
      Company: row.companyName ?? '',
      Customer: row.customerName ?? '',
      'Project Value': (row.estimatedCost ?? 0).toFixed(2),
      'Project Start Date': this.formatDate(row.expectedStartDate),
      'Completion Date': this.formatDate(row.expectedEndDate),
    }));
    exportToCsv('project-wise-stock-tracking.csv', data, [
      'Project Id',
      'Cost of Purchased Items',
      'Cost of Issued Items',
      'Cost of Delivered Items',
      'Project Name',
      'Project Status',
      'Company',
      'Customer',
      'Project Value',
      'Project Start Date',
      'Completion Date',
    ]);
  }
}
