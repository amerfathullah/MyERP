import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyService } from '../../../proxy/core/company.service';
import { AgingReportService } from '../../../proxy/accounting/aging-report.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CompanyDto } from '../../../proxy/core/models';
import type { AgingReportDto } from '../../../proxy/accounting/models';

@Component({
  selector: 'app-aging-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './aging-report.component.html',
  styleUrls: ['./aging-report.component.scss'],
})
export class AgingReportComponent implements OnInit {
  private fb = inject(FormBuilder);
  private agingReportService = inject(AgingReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    asOfDate: [new Date().toISOString().split('T')[0], Validators.required],
    reportType: ['receivables'],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<AgingReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId();
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        if (this.filters.get('companyId')?.value) {
          this.generate();
        }
      });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const { companyId, asOfDate, reportType } = this.filters.getRawValue();
    const request = { companyId: companyId!, asOfDate: asOfDate! };
    const call$ = reportType === 'receivables'
      ? this.agingReportService.getReceivablesAging(request)
      : this.agingReportService.getPayablesAging(request);

    call$.subscribe({
        next: data => { this.report.set(data); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r) return;
    const row = r.bucketLabels.reduce((obj: any, label, i) => {
      obj[label] = r.bucketTotals[i]; return obj;
    }, { total: r.totalOutstanding });
    exportToCsv(`${r.reportType}-aging.csv`, [row], [...r.bucketLabels, 'total']);
  }
}
