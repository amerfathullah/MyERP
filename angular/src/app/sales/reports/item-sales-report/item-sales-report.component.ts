import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemSalesService } from '../../../proxy/sales/item-sales.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { ItemSalesLineDto } from '../../../proxy/sales/models';

@Component({
  selector: 'app-item-sales-report',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './item-sales-report.component.html',
  styleUrls: ['./item-sales-report.component.scss'],
})
export class ItemSalesReportComponent implements OnInit {
  private itemSalesService = inject(ItemSalesService);
  private companyContext = inject(CompanyContextService);

  items = signal<ItemSalesLineDto[]>([]);
  isLoading = signal(false);
  totalRevenue = signal(0);
  totalQty = signal(0);
  uniqueItems = signal(0);

  dateFrom = new Date(Date.now() - 90 * 86400000).toISOString().slice(0, 10);
  dateTo = new Date().toISOString().slice(0, 10);

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.itemSalesService.getReport({ companyId, fromDate: this.dateFrom, toDate: this.dateTo } as any).subscribe({
      next: (result) => {
        this.items.set(result.items ?? []);
        this.totalRevenue.set(result.totalRevenue ?? 0);
        this.totalQty.set(result.totalQty ?? 0);
        this.uniqueItems.set(result.uniqueItems ?? 0);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    exportToCsv('item-sales-summary.csv', this.items(), [
      'itemName', 'totalQty', 'totalRevenue', 'averageRate', 'invoiceCount',
    ]);
  }
}
