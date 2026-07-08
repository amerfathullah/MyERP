import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import type { SalesInvoiceDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-einvoice-logs',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    LhdnStatusBadgeComponent],
  templateUrl: './einvoice-logs.component.html',
  styleUrls: ['./einvoice-logs.component.scss'],
})
export class EinvoiceLogsComponent implements OnInit {
  private invoiceService = inject(SalesInvoiceService);
  private toaster = inject(ToasterService);

  submissions: SalesInvoiceDto[] = [];
  totalCount = 0;
  isLoading = false;
  ngOnInit(): void {
    this.loadLogs(0, 20);
  }

  loadLogs(skipCount: number, maxResultCount: number): void {
    this.isLoading = true;
    this.invoiceService.getList({ skipCount, maxResultCount, sorting: '' }).subscribe({
      next: (result) => {
        // Filter to only invoices that have been submitted to LHDN
        this.submissions = (result.items ?? []).filter(inv => inv.eInvoiceStatus && inv.eInvoiceStatus !== 'NotSubmitted');
        this.totalCount = this.submissions.length;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.toaster.error('Failed to load submission logs');
      },
    });
  }

  onPageChange(event: any): void {
    this.loadLogs(event.pageIndex * event.pageSize, event.pageSize);
  }

  refreshStatus(id: string): void {
    // Will be wired when dedicated EInvoiceService proxy is generated
    this.toaster.info('Status refresh will be available once e-invoice API is deployed');
  }
}
