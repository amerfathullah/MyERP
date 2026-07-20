import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe , RestService } from '@abp/ng.core';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import type { CompanyDto } from '../../../proxy/core/models';

interface PurchaseRegisterLine {
  invoiceId: string;
  invoiceNumber: string;
  postingDate: string;
  supplierId: string;
  supplierName?: string;
  netTotal: number;
  taxAmount: number;
  grandTotal: number;
  amountPaid: number;
  outstanding: number;
  isReturn: boolean;
}

interface RegisterReport {
  items: PurchaseRegisterLine[];
  totalNet: number;
  totalTax: number;
  totalGrand: number;
  count: number;
}

@Component({
  selector: 'app-purchase-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './purchase-register.component.html',
  styleUrls: ['./purchase-register.component.scss'],
})
export class PurchaseRegisterComponent implements OnInit {
  private fb = inject(FormBuilder);
  private restService = inject(RestService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

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
    this.restService.request<any, RegisterReport>({ method: 'GET', url: '/api/app/purchase-register/report', params: { companyId: companyId!, fromDate: fromDate!, toDate: toDate! } }, { apiName: 'Default' }).subscribe({
      next: data => { this.report.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }
}
