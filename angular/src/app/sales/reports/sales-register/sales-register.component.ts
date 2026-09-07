import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { SalesRegisterService } from '../../../proxy/sales/sales-register.service';
import { CustomerService } from '../../../proxy/sales/customer.service';
import type { RegisterReportDto, SalesRegisterLineDto, CustomerDto } from '../../../proxy/sales/models';
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
  private customerService = inject(CustomerService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    customerId: [''],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
    includePayments: [false],
  });

  companies = signal<CompanyDto[]>([]);
  customers = signal<CustomerDto[]>([]);
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

    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe(res => {
        this.customers.set(res.items ?? []);
      });
  }

  generate(): void {
    if (this.filters.invalid) { this.filters.markAllAsTouched(); return; }
    this.isLoading.set(true);
    const { companyId, fromDate, toDate, customerId, includePayments } = this.filters.getRawValue();
    this.reportService.getReport({
      companyId: companyId!,
      fromDate: fromDate!,
      toDate: toDate!,
      customerId: customerId || undefined,
      includePayments: includePayments || false,
    } as any).subscribe({
      next: data => { this.report.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.items?.length) return;
    const isPayments = this.filters.get('includePayments')?.value;
    const cols = isPayments
      ? ['voucherType', 'invoiceNumber', 'postingDate', 'debit', 'credit', 'balance', 'outstanding']
      : ['invoiceNumber', 'postingDate', 'netTotal', 'taxAmount', 'grandTotal', 'amountPaid', 'outstanding', 'isReturn'];
    exportToCsv('sales-register.csv', r.items, cols);
  }
}
