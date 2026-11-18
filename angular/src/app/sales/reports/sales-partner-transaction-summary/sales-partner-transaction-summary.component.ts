import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SalesPartnerCommissionSummaryService } from '../../../proxy/sales/sales-partner-commission-summary.service';
import { SalesPartnerService } from '../../../proxy/sales/sales-partner.service';
import type {
  SalesPartnerDto,
  SalesPartnerTransactionSummaryReportDto,
  SalesPartnerTransactionSummaryRowDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { formatDayBookVoucherDate } from '../../../accounting/reports/day-book/day-book.component';

@Component({
  selector: 'app-sales-partner-transaction-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  templateUrl: './sales-partner-transaction-summary.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class SalesPartnerTransactionSummaryComponent implements OnInit {
  private router = inject(Router);
  private reportService = inject(SalesPartnerCommissionSummaryService);
  private partnerService = inject(SalesPartnerService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  report = signal<SalesPartnerTransactionSummaryReportDto | null>(null);
  salesPartners = signal<SalesPartnerDto[]>([]);
  loading = signal(false);

  selectedDocType = 'Sales Invoice';
  selectedSalesPartnerId = '';
  brand = '';
  showReturnEntries = false;
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
      .getTransactionSummary({
        companyId,
        docType: this.selectedDocType,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        salesPartnerId: this.selectedSalesPartnerId || null,
        brand: this.brand.trim() || null,
        showReturnEntries: this.showReturnEntries,
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

  drillDown(row: SalesPartnerTransactionSummaryRowDto): void {
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
      'Item Code': row.itemCode ?? '',
      'Item Group': row.itemGroupName ?? '',
      Brand: row.brand ?? '',
      Quantity: (row.quantity ?? 0).toFixed(2),
      Rate: (row.rate ?? 0).toFixed(2),
      Amount: (row.amount ?? 0).toFixed(2),
      'Sales Partner': row.salesPartnerName ?? '',
      'Commission Rate %': (row.commissionRate ?? 0).toFixed(2),
      Commission: (row.commission ?? 0).toFixed(2),
      Currency: currency,
    }));
    exportToCsv('sales-partner-transaction-summary.csv', data, [
      'Document',
      'Document Type',
      'Date',
      'Customer',
      'Territory',
      'Item Code',
      'Item Group',
      'Brand',
      'Quantity',
      'Rate',
      'Amount',
      'Sales Partner',
      'Commission Rate %',
      'Commission',
      'Currency',
    ]);
  }
}
