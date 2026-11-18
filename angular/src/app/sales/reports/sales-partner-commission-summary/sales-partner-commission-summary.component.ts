import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SalesPartnerCommissionSummaryService } from '../../../proxy/sales/sales-partner-commission-summary.service';
import { SalesPartnerService } from '../../../proxy/sales/sales-partner.service';
import type {
  SalesPartnerCommissionSummaryReportDto,
  SalesPartnerCommissionSummaryRowDto,
  SalesPartnerDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { formatDayBookVoucherDate } from '../../../accounting/reports/day-book/day-book.component';

@Component({
  selector: 'app-sales-partner-commission-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  templateUrl: './sales-partner-commission-summary.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class SalesPartnerCommissionSummaryComponent implements OnInit {
  private router = inject(Router);
  private reportService = inject(SalesPartnerCommissionSummaryService);
  private partnerService = inject(SalesPartnerService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  report = signal<SalesPartnerCommissionSummaryReportDto | null>(null);
  salesPartners = signal<SalesPartnerDto[]>([]);
  loading = signal(false);

  selectedDocType = 'Sales Invoice';
  selectedSalesPartnerId = '';
  fromDate = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];

  ngOnInit(): void {
    this.partnerService.getList({ maxResultCount: 200, skipCount: 0, sorting: '' }).subscribe({
      next: res => this.salesPartners.set(res.items ?? []),
      error: () => {},
    });
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.reportService
      .getReport({
        companyId,
        docType: this.selectedDocType,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        salesPartnerId: this.selectedSalesPartnerId || null,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: err => {
          this.loading.set(false);
          this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
        },
      });
  }

  formatDate(value: string | Date | undefined | null): string {
    return formatDayBookVoucherDate(value);
  }

  drillDown(row: SalesPartnerCommissionSummaryRowDto): void {
    if (!row.transactionId) return;
    if (row.docType === 'Sales Order') {
      this.router.navigate(['/sales/orders', row.transactionId]);
    } else {
      this.router.navigate(['/sales/invoices', row.transactionId]);
    }
  }

  exportCsv(): void {
    const rows = this.report()?.rows ?? [];
    if (rows.length === 0) return;

    const currency = this.report()?.currency ?? 'MYR';
    const data = rows.map(row => ({
      Document: row.transactionNumber ?? '',
      'Document Type': row.docType ?? '',
      Date: this.formatDate(row.postingDate),
      Customer: row.customerName ?? '',
      Territory: row.territoryName ?? '',
      'Sales Partner': row.salesPartnerName ?? '',
      'Commission Rate %': (row.commissionRate ?? 0).toFixed(2),
      Amount: (row.amount ?? 0).toFixed(2),
      'Total Commission': (row.totalCommission ?? 0).toFixed(2),
      Currency: currency,
    }));
    exportToCsv('sales-partner-commission-summary.csv', data, [
      'Document',
      'Document Type',
      'Date',
      'Customer',
      'Territory',
      'Sales Partner',
      'Commission Rate %',
      'Amount',
      'Total Commission',
      'Currency',
    ]);
  }
}
