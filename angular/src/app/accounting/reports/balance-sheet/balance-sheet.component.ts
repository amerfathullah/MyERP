import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import type { BalanceSheetRowDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-balance-sheet',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule],
  templateUrl: './balance-sheet.component.html',
  styleUrls: ['./balance-sheet.component.scss'],
})
export class BalanceSheetComponent {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    asOfDate: [new Date().toISOString().split('T')[0], Validators.required],
  });

  companies = signal<CompanyDto[]>([]);
  assets = signal<BalanceSheetRowDto[]>([]);
  liabilities = signal<BalanceSheetRowDto[]>([]);
  equity = signal<BalanceSheetRowDto[]>([]);
  totalAssets = signal(0);
  totalLiabilities = signal(0);
  totalEquity = signal(0);
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
    const { companyId, asOfDate } = this.filters.getRawValue();

    this.reportingService.getBalanceSheet({
      companyId: companyId!,
      asOfDate: asOfDate!,
    }).subscribe({
      next: (report) => {
        this.assets.set(report.assetRows ?? []);
        this.liabilities.set(report.liabilityRows ?? []);
        this.equity.set(report.equityRows ?? []);
        this.totalAssets.set(report.totalAssets ?? 0);
        this.totalLiabilities.set(report.totalLiabilities ?? 0);
        this.totalEquity.set(report.totalEquity ?? 0);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Failed to generate report');
      },
    });
  }
}
