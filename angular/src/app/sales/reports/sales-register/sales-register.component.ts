import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { CompanyService } from '../../../proxy/core/company.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CompanyDto } from '../../../proxy/core/models';

interface SalesRegisterLine {
  invoiceId: string;
  invoiceNumber: string;
  postingDate: string;
  customerId: string;
  customerName?: string;
  netTotal: number;
  taxAmount: number;
  grandTotal: number;
  amountPaid: number;
  outstanding: number;
  isReturn: boolean;
}

interface RegisterReport {
  items: SalesRegisterLine[];
  totalNet: number;
  totalTax: number;
  totalGrand: number;
  count: number;
}

@Component({
  selector: 'app-sales-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './sales-register.component.html',
  styleUrls: ['./sales-register.component.scss'],
})
export class SalesRegisterComponent {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private companyService = inject(CompanyService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<RegisterReport | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => {
        this.companies.set(res.items ?? []);
        if (this.filters.get('companyId')?.value) {
          this.generate();
        }
      });
  }

  generate(): void {
    if (this.filters.invalid) { this.filters.markAllAsTouched(); return; }
    this.isLoading.set(true);
    const { companyId, fromDate, toDate } = this.filters.getRawValue();
    this.http.get<RegisterReport>('/api/app/sales-register/report', {
      params: { companyId: companyId!, fromDate: fromDate!, toDate: toDate! }
    }).subscribe({
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
