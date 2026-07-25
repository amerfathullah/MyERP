import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StockBalanceService } from '../../proxy/inventory/stock-balance.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import type { StockBalanceDto } from '../../proxy/inventory/models';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-stock-balance',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent],
  templateUrl: './stock-balance.component.html',
  styleUrls: ['./stock-balance.component.scss'],
})
export class StockBalanceComponent implements OnInit {
  private service = inject(StockBalanceService);
  private warehouseService = inject(WarehouseService);

  items = signal<any[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  warehouses = signal<any[]>([]);

  // Filters
  warehouseFilter = '';
  pageSize = 50;
  currentPage = 0;

  ngOnInit(): void {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)));
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    const params: any = {
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
    };
    if (this.warehouseFilter) params.warehouseId = this.warehouseFilter;

    this.service.getStockBalance(params).subscribe({
      next: (result) => {
        this.items.set(result.items ?? []);
        this.totalCount.set(result.totalCount ?? 0);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  onWarehouseChange(): void {
    this.currentPage = 0;
    this.load();
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.load();
  }

  exportCsv(): void {
    exportToCsv('stock-balance', this.items(), [
      'itemName', 'warehouseName', 'actualQty', 'orderedQty',
      'reservedQty', 'projectedQty', 'stockValue', 'valuationRate',
    ]);
  }
}
