import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { PurchaseRegisterService } from '../../../proxy/purchasing/purchase-register.service';
import { SupplierService } from '../../../proxy/purchasing/supplier.service';
import type { PurchaseRegisterLineDto, SupplierDto } from '../../../proxy/purchasing/models';
import type { RegisterReportDto } from '../../../proxy/sales/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-purchase-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './purchase-register.component.html',
  styleUrls: ['./purchase-register.component.scss'],
})
export class PurchaseRegisterComponent implements OnInit {
  private fb = inject(FormBuilder);
  private registerService = inject(PurchaseRegisterService);
  private supplierService = inject(SupplierService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    supplierId: [''],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
    includePayments: [false],
  });

  companies = signal<CompanyDto[]>([]);
  suppliers = signal<SupplierDto[]>([]);
  report = signal<RegisterReportDto<PurchaseRegisterLineDto> | null>(null);
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

    this.supplierService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe(res => {
        this.suppliers.set(res.items ?? []);
      });
  }

  generate(): void {
    if (this.filters.invalid) { this.filters.markAllAsTouched(); return; }
    this.isLoading.set(true);
    const { companyId, fromDate, toDate, supplierId, includePayments } = this.filters.getRawValue();
    this.registerService.getReport({
      companyId: companyId!,
      fromDate: fromDate!,
      toDate: toDate!,
      supplierId: supplierId || undefined,
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
    exportToCsv('purchase-register.csv', r.items, cols);
  }
}
