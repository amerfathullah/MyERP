import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { GrossProfitReportService } from '../../../proxy/sales/gross-profit-report.service';
import type { GrossProfitReportDto } from '../../../proxy/sales/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-gross-profit-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './gross-profit-report.component.html',
  styleUrls: ['./gross-profit-report.component.scss'],
})
export class GrossProfitReportComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(GrossProfitReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0], Validators.required],
    toDate: [new Date().toISOString().split('T')[0], Validators.required],
    groupBy: ['Invoice', Validators.required],
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
    const { companyId, fromDate, toDate, groupBy } = this.filters.getRawValue();

    this.reportService.getReport({
      companyId: companyId!,
      fromDate: fromDate!,
      toDate: toDate!,
      groupBy: groupBy!
    }).subscribe({
      next: data => { this.report.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.items?.length) return;

    const groupBy = this.filters.get('groupBy')?.value || 'Invoice';
    let columns: string[];

    if (groupBy === 'Item') {
      columns = ['itemCode', 'itemName', 'itemGroup', 'quantity', 'sellingRate', 'valuationRate', 'revenue', 'cost', 'grossProfit', 'grossProfitPercentage'];
    } else if (groupBy === 'Customer') {
      columns = ['customerName', 'quantity', 'revenue', 'cost', 'grossProfit', 'grossProfitPercentage'];
    } else {
      // Per ERPNext PR #58631: include item_name in export
      columns = ['invoiceNumber', 'issueDate', 'customerName', 'itemCode', 'itemName', 'quantity', 'sellingRate', 'valuationRate', 'revenue', 'cost', 'grossProfit', 'grossProfitPercentage'];
    }

    exportToCsv(`gross-profit-${groupBy.toLowerCase()}.csv`, r.items, columns);
  }
}
