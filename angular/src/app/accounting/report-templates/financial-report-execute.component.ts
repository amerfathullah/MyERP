import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, DatePipe, NgClass } from '@angular/common';
import { LocalizationPipe , LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { FinancialReportTemplateService } from '../../proxy/accounting/financial-report-template.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';
import type {
  FinancialReportTemplateDto,
  FinancialReportResultDto,
  FinancialReportResultRowDto,
} from '../../proxy/accounting/models';

@Component({
  standalone: true,
  selector: 'app-financial-report-execute',
  imports: [FormsModule, DecimalPipe, DatePipe, NgClass, LocalizationPipe],
  templateUrl: './financial-report-execute.component.html',
  styleUrls: ['./financial-report-execute.component.scss'],
})
export class FinancialReportExecuteComponent implements OnInit {
  private service = inject(FinancialReportTemplateService);
  private localization = inject(LocalizationService);
  private toaster = inject(ToasterService);
  companyContext = inject(CompanyContextService);

  templates = signal<FinancialReportTemplateDto[]>([]);
  selectedTemplateId = '';
  fromDate = '';
  toDate = '';
  loading = signal(false);
  result = signal<FinancialReportResultDto | null>(null);

  visibleRows = computed(() => {
    const r = this.result();
    if (!r?.rows) return [];
    return r.rows.filter(row => !(row.value === 0 && this.isHideWhenEmpty(row)));
  });

  l(key: string) { return this.localization.instant(key); }

  ngOnInit(): void {
    this.loadTemplates();
    this.setDefaultDates();
  }

  private loadTemplates(): void {
    this.service.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name' }).subscribe({
      next: res => {
        const enabled = (res.items ?? []).filter(t => t.isEnabled);
        this.templates.set(enabled);
      },
      error: () => this.toaster.error(this.l('::FailedToLoad')),
    });
  }

  private setDefaultDates(): void {
    const now = new Date();
    const yearStart = new Date(now.getFullYear(), 0, 1);
    this.fromDate = this.formatDate(yearStart);
    this.toDate = this.formatDate(now);
  }

  private formatDate(d: Date): string {
    return d.toISOString().split('T')[0];
  }

  run(): void {
    if (!this.selectedTemplateId || !this.companyContext.currentCompanyId() || !this.fromDate || !this.toDate) {
      this.toaster.warn(this.l('::PleaseFillAllRequiredFields'));
      return;
    }
    this.loading.set(true);
    this.result.set(null);

    this.service.execute({
      templateId: this.selectedTemplateId,
      companyId: this.companyContext.currentCompanyId(),
      fromDate: this.fromDate,
      toDate: this.toDate,
    }).subscribe({
      next: res => {
        this.result.set(res);
        this.loading.set(false);
      },
      error: () => {
        this.toaster.error(this.l('::OperationFailed'));
        this.loading.set(false);
      },
    });
  }

  exportCsv(): void {
    const rows = this.visibleRows();
    if (!rows.length) return;
    const data = rows.map(r => ({
      Label: r.label ?? '',
      Amount: r.value ?? 0,
      Code: r.referenceCode ?? '',
    }));
    const name = this.result()?.templateName ?? 'report';
    exportToCsv(`${name}_${this.fromDate}_${this.toDate}.csv`, data, ['Label', 'Amount', 'Code']);
  }

  printReport(): void {
    window.print();
  }

  getIndentClass(row: FinancialReportResultRowDto): string {
    const level = row.indentLevel ?? 0;
    return `indent-${Math.min(level, 3)}`;
  }

  isSectionBreak(row: FinancialReportResultRowDto): boolean {
    return row.dataSource === 'SectionBreak';
  }

  private isHideWhenEmpty(row: FinancialReportResultRowDto): boolean {
    // The backend already filters most, but we also hide rows with no label that have zero value
    return !row.label || row.label.trim() === '';
  }
}
