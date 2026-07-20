import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CustomerRevenueService } from '../../../proxy/sales/customer-revenue.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CustomerRevenueLineDto } from '../../../proxy/sales/models';

@Component({
  selector: 'app-customer-revenue-report',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './customer-revenue-report.component.html',
  styleUrls: ['./customer-revenue-report.component.scss'],
})
export class CustomerRevenueReportComponent implements OnInit {
  private customerRevenueService = inject(CustomerRevenueService);
  private companyContext = inject(CompanyContextService);

  items = signal<CustomerRevenueLineDto[]>([]);
  isLoading = signal(false);
  totalRevenue = signal(0);
  totalOutstanding = signal(0);
  customerCount = signal(0);

  dateFrom = new Date(Date.now() - 90 * 86400000).toISOString().slice(0, 10);
  dateTo = new Date().toISOString().slice(0, 10);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.customerRevenueService.getReport({ companyId, fromDate: this.dateFrom, toDate: this.dateTo } as any).subscribe({
      next: (result) => {
        this.items.set(result.items ?? []);
        this.totalRevenue.set(result.totalRevenue ?? 0);
        this.totalOutstanding.set(result.totalOutstanding ?? 0);
        this.customerCount.set(result.customerCount ?? 0);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    exportToCsv('customer-revenue.csv', this.items(), [
      'customerName', 'invoiceCount', 'totalRevenue', 'totalPaid', 'totalOutstanding',
    ]);
  }
}
