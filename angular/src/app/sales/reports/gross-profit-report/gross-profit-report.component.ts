import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CompanyDto } from '../../../proxy/core/models';

interface GrossProfitLineDto {
  invoiceId: string;
  invoiceNumber: string;
  issueDate: string;
  customerName?: string;
  revenue: number;
  cost: number;
  grossProfit: number;
  grossProfitPercentage: number;
}

interface GrossProfitReportDto {
  totalRevenue: number;
  totalCost: number;
  grossProfit: number;
  grossProfitPercentage: number;
  items: GrossProfitLineDto[];
}

@Component({
  selector: 'app-gross-profit-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './gross-profit-report.component.html',
  styleUrls: ['./gross-profit-report.component.scss'],
})
export class GrossProfitReportComponent implements OnInit {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0], Validators.required],
    toDate: [new Date().toISOString().split('T')[0], Validators.required],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<GrossProfitReportDto | null>(null);
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
    const { companyId, fromDate, toDate } = this.filters.getRawValue();

    this.http.get<GrossProfitReportDto>('/api/app/gross-profit-report/report', {
      params: { companyId: companyId!, fromDate: fromDate!, toDate: toDate! }
    }).subscribe({
      next: data => { this.report.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.items?.length) return;
    exportToCsv('gross-profit.csv', r.items, [
      'invoiceNumber', 'issueDate', 'revenue', 'cost', 'grossProfit', 'grossProfitPercentage'
    ]);
  }
}
