import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { StockLedgerService } from '../../../proxy/inventory/stock-ledger.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { StockLedgerRowDto } from '../../../proxy/inventory/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-stock-ledger',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './stock-ledger.component.html',
  styleUrls: ['./stock-ledger.component.scss'],
})
export class StockLedgerComponent implements OnInit {
  private fb = inject(FormBuilder);
  private stockLedgerService = inject(StockLedgerService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);

  companies = signal<CompanyDto[]>([]);
  items = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  warehouses = signal<{ id: string; name: string }[]>([]);
  rows = signal<StockLedgerRowDto[]>([]);
  totalIn = signal(0);
  totalOut = signal(0);
  isLoading = signal(false);
  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0], Validators.required],
    toDate: [new Date().toISOString().split('T')[0], Validators.required],
    itemId: [''],
    warehouseId: [''],
  });

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(r => {
        this.companies.set(r.items ?? []);
        const defaultId = this.companyContext.currentCompanyId();
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        if (this.filters.get('companyId')?.value) {
          this.loadReport();
        }
      });
    this.loadItems();
    this.loadWarehouses();
  }

  private loadItems(): void {
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: (res) => this.items.set((res.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName }))),
      error: () => {},
    });
  }

  private loadWarehouses(): void {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: (res) => this.warehouses.set((res.items ?? []).map((w: any) => ({ id: w.id, name: w.name }))),
      error: () => {},
    });
  }

  loadReport(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const value = this.filters.getRawValue();
    this.stockLedgerService.getStockLedger({
      companyId: value.companyId!,
      fromDate: value.fromDate!,
      toDate: value.toDate!,
      itemId: value.itemId || undefined,
      warehouseId: value.warehouseId || undefined,
    }).subscribe({
      next: (report) => {
        this.rows.set(report.rows ?? []);
        this.totalIn.set(report.totalIn ?? 0);
        this.totalOut.set(report.totalOut ?? 0);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::FailedToLoad');
      },
    });
  }

  getVoucherRoute(row: StockLedgerRowDto): string[] {
    const id = row.voucherId;
    if (!id) return ['/'];
    switch (row.voucherType) {
      case 'StockEntry': return ['/inventory/stock-entries', id];
      case 'DeliveryNote': return ['/sales/delivery-notes', id];
      case 'PurchaseReceipt': return ['/purchasing/receipts', id];
      case 'SalesInvoice': return ['/sales/invoices', id];
      case 'PurchaseInvoice': return ['/purchasing/invoices', id];
      case 'StockReconciliation': return ['/inventory/stock-reconciliation', id];
      default: return ['/'];
    }
  }

  exportCsv(): void {
    const data = this.rows();
    if (!data.length) return;
    exportToCsv('stock-ledger.csv', data, [
      'postingDate', 'itemCode', 'itemName', 'warehouse', 'voucherType', 'quantityChange', 'balanceQty', 'valuationRate'
    ]);
  }
}
