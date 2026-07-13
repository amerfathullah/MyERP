import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { CompanyService } from '../../../proxy/core/company.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CompanyDto } from '../../../proxy/core/models';

interface AgingReportDto {
  reportType: string;
  asOfDate: string;
  bucketLabels: string[];
  bucketTotals: number[];
  totalOutstanding: number;
  invoiceCount: number;
}

@Component({
  selector: 'app-aging-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './aging-report.component.html',
  styleUrls: ['./aging-report.component.scss'],
})
export class AgingReportComponent {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private companyService = inject(CompanyService);

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
        // Auto-load if company is pre-selected
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
    const endpoint = reportType === 'receivables'
      ? '/api/app/aging-report/receivables-aging'
      : '/api/app/aging-report/payables-aging';

    this.http.get<AgingReportDto>(endpoint, { params: { companyId: companyId!, asOfDate: asOfDate! } })
      .subscribe({
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
