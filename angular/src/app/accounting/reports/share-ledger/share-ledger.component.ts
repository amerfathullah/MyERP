import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { ShareReportingService } from '../../../proxy/accounting/share-reporting.service';
import { ShareholderService } from '../../../proxy/accounting/shareholder.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  ShareLedgerReportDto,
  ShareLedgerRowDto,
  ShareholderDto,
} from '../../../proxy/accounting/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildShareLedgerCsvRows(
  rows: ShareLedgerRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    Shareholder: r.shareholderTitle ?? '',
    Date: r.date ? r.date.split('T')[0] : '',
    'Transfer Type': r.transferType ?? '',
    'Share Type': r.shareTypeTitle ?? '',
    'From No': r.fromNo ?? 0,
    'To No': r.toNo ?? 0,
    'No of Shares': r.noOfShares ?? 0,
    Rate: (r.rate ?? 0).toFixed(2),
    Amount: (r.amount ?? 0).toFixed(2),
    Company: r.companyName ?? '',
  }));
}

@Component({
  selector: 'app-share-ledger',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './share-ledger.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ShareLedgerComponent implements OnInit {
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
  report = signal<ShareLedgerReportDto | null>(null);
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
      .getShareLedger({
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
    exportToCsv('share-ledger.csv', buildShareLedgerCsvRows(r.rows), [
      'Shareholder',
      'Date',
      'Transfer Type',
      'Share Type',
      'From No',
      'To No',
      'No of Shares',
      'Rate',
      'Amount',
      'Company',
    ]);
  }
}
