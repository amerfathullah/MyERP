import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { ShareReportingService } from '../../../proxy/accounting/share-reporting.service';
import { ShareholderService } from '../../../proxy/accounting/shareholder.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  ShareBalanceReportDto,
  ShareBalanceRowDto,
  ShareholderDto,
} from '../../../proxy/accounting/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildShareBalanceCsvRows(
  rows: ShareBalanceRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    Shareholder: r.shareholderTitle ?? '',
    'Share Type': r.shareTypeTitle ?? '',
    'No of Shares': r.noOfShares ?? 0,
    'Average Rate': (r.averageRate ?? 0).toFixed(4),
    Amount: (r.amount ?? 0).toFixed(2),
  }));
}

@Component({
  selector: 'app-share-balance',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './share-balance.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ShareBalanceComponent implements OnInit {
  private fb = inject(FormBuilder);
  private shareReportingService = inject(ShareReportingService);
  private shareholderService = inject(ShareholderService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: [''],
    date: [new Date().toISOString().split('T')[0], Validators.required],
    shareholderId: [''],
  });

  companies = signal<CompanyDto[]>([]);
  shareholders = signal<ShareholderDto[]>([]);
  report = signal<ShareBalanceReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      this.companies.set(res.items ?? []);
      const defaultId = this.companyContext.currentCompanyId();
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
      }
      this.loadShareholders();
    });
  }

  onCompanyChange(): void {
    this.filters.patchValue({ shareholderId: '' });
    this.loadShareholders();
  }

  loadShareholders(): void {
    const companyId = this.filters.get('companyId')?.value || undefined;
    this.shareholderService
      .getList({ companyId, skipCount: 0, maxResultCount: 500, sorting: '' })
      .subscribe(res => {
        this.shareholders.set(res.items ?? []);
      });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const { companyId, date, shareholderId } = this.filters.getRawValue();
    this.shareReportingService
      .getShareBalance({
        date: date!,
        companyId: companyId || null,
        shareholderId: shareholderId || null,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: err => {
          this.toaster.error(err?.error?.error?.message || 'Error generating report');
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows?.length) return;
    exportToCsv('share-balance.csv', buildShareBalanceCsvRows(r.rows), [
      'Shareholder',
      'Share Type',
      'No of Shares',
      'Average Rate',
      'Amount',
    ]);
  }
}
