import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { SalesRegisterService } from '../../../proxy/sales/sales-register.service';
import type { RegisterReportDto, SalesRegisterLineDto } from '../../../proxy/sales/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-sales-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './sales-register.component.html',
  styleUrls: ['./sales-register.component.scss'],
})
export class SalesRegisterComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(SalesRegisterService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<RegisterReportDto<SalesRegisterLineDto> | null>(null);
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
    if (this.filters.invalid) { this.filters.markAllAsTouched(); return; }
    this.isLoading.set(true);
    const { companyId, fromDate, toDate } = this.filters.getRawValue();
    this.reportService.getReport({ companyId: companyId!, fromDate: fromDate!, toDate: toDate! }).subscribe({
      next: data => { this.report.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.items?.length) return;
    exportToCsv('sales-register.csv', r.items, [
      'invoiceNumber', 'postingDate', 'netTotal', 'taxAmount', 'grandTotal', 'amountPaid', 'outstanding', 'isReturn'
    ]);
  }
}
