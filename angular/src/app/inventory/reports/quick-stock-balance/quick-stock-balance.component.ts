import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { StockBalanceService } from '../../../proxy/inventory/stock-balance.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type { ItemDto, QuickStockBalanceDto, WarehouseDto } from '../../../proxy/inventory/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';

@Component({
  selector: 'app-quick-stock-balance',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './quick-stock-balance.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class QuickStockBalanceComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private stockBalanceService = inject(StockBalanceService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private lastLookupBy: 'item' | 'barcode' = 'item';

  filters = this.fb.group({
    companyId: [''],
    warehouseId: [''],
    date: [new Date().toISOString().split('T')[0]],
    itemId: [''],
    barcode: [''],
  });

  companies = signal<CompanyDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  items = signal<ItemDto[]>([]);
  result = signal<QuickStockBalanceDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      const list = res.items ?? [];
      this.companies.set(list);
      const defaultId = this.companyContext.currentCompanyId() || list[0]?.id || '';
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
      }
      this.loadLookups();
    });
  }

  onCompanyChange(): void {
    this.filters.patchValue({ warehouseId: '', itemId: '' });
    this.result.set(null);
    this.loadLookups();
  }

  loadLookups(): void {
    const companyId = this.filters.get('companyId')?.value || undefined;
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name' }).subscribe(res => {
      const list = res.items ?? [];
      this.warehouses.set(companyId ? list.filter(w => w.companyId === companyId) : list);
    });
    this.itemService
      .getList({ companyId, skipCount: 0, maxResultCount: 500, sorting: 'itemCode' })
      .subscribe(res => {
        this.items.set(res.items ?? []);
      });
  }

  onItemChange(): void {
    this.lastLookupBy = 'item';
    this.lookupBalance();
  }

  onBarcodeChange(): void {
    this.lastLookupBy = 'barcode';
    this.lookupBalance();
  }

  onWarehouseOrDateChange(): void {
    const { itemId, barcode } = this.filters.getRawValue();
    if (itemId || barcode?.trim()) {
      this.lookupBalance();
    }
  }

  lookupBalance(): void {
    const { warehouseId, date, itemId, barcode } = this.filters.getRawValue();
    if (!warehouseId || !date) {
      this.toaster.warn('Please enter Warehouse and Date.');
      return;
    }

    const useBarcode = this.lastLookupBy === 'barcode' && Boolean(barcode?.trim());
    if (!useBarcode && !itemId && !barcode?.trim()) {
      this.result.set(null);
      return;
    }

    this.isLoading.set(true);
    this.stockBalanceService
      .getQuickStockBalance({
        warehouseId,
        date,
        itemId: useBarcode ? null : itemId || null,
        barcode: useBarcode ? barcode!.trim() : barcode?.trim() && !itemId ? barcode.trim() : null,
      })
      .subscribe({
        next: res => {
          this.result.set(res);
          const barcodes = res.barcodes ?? [];
          const currentBarcode = this.filters.get('barcode')?.value?.trim() || '';
          const patch: { itemId?: string; barcode?: string } = {};
          if (res.itemId && this.filters.get('itemId')?.value !== res.itemId) {
            patch.itemId = res.itemId;
          }
          if (currentBarcode && !barcodes.includes(currentBarcode)) {
            patch.barcode = '';
          }
          if (Object.keys(patch).length > 0) {
            this.filters.patchValue(patch, { emitEvent: false });
          }
          this.isLoading.set(false);
        },
        error: err => {
          this.result.set(null);
          this.toaster.error(err?.error?.error?.message || 'Failed to fetch Quick Stock Balance.');
          this.isLoading.set(false);
        },
      });
  }

  openStockBalanceReport(): void {
    const { warehouseId, itemId } = this.filters.getRawValue();
    this.router.navigate(['/inventory/reports/stock-balance'], {
      queryParams: {
        itemId: itemId || undefined,
        warehouseId: warehouseId || undefined,
      },
    });
  }
}
