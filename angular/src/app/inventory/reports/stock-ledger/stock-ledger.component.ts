import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockLedgerService } from '../../../proxy/inventory/stock-ledger.service';
import { CompanyService } from '../../../proxy/core/company.service';
import type { StockLedgerRowDto } from '../../../proxy/inventory/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-stock-ledger',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, LocalizationModule],
  templateUrl: './stock-ledger.component.html',
  styleUrls: ['./stock-ledger.component.scss'],
})
export class StockLedgerComponent {
  private fb = inject(FormBuilder);
  private stockLedgerService = inject(StockLedgerService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  rows = signal<StockLedgerRowDto[]>([]);
  totalIn = signal(0);
  totalOut = signal(0);
  isLoading = signal(false);
  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0], Validators.required],
    toDate: [new Date().toISOString().split('T')[0], Validators.required],
  });

  constructor() {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(r => this.companies.set(r.items ?? []));
  }

  loadReport(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const value = this.filters.getRawValue();
    this.stockLedgerService.getStockLedger({
      companyId: value.companyId!,
      fromDate: value.fromDate!,
      toDate: value.toDate!,
    }).subscribe({
      next: (report) => {
        this.rows.set(report.rows ?? []);
        this.totalIn.set(report.totalIn ?? 0);
        this.totalOut.set(report.totalOut ?? 0);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Failed to load report');
      },
    });
  }
}
