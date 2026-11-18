import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockReconciliationService } from '../../proxy/inventory';
import type { StockReconciliationItemPreviewDto } from '../../proxy/dtos/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';

export interface ReconciliationFormRow {
  itemId: string;
  warehouseId: string;
  currentQuantity: number;
  currentValuationRate: number;
  newQuantity: number | null;
  newValuationRate: number | null;
}

export interface ReconciliationPreviewValues {
  effectiveQty: number;
  effectiveRate: number;
  currentAmount: number;
  newAmount: number;
  quantityDifference: number;
  amountDifference: number;
}

function roundTo(value: number, decimals: number): number {
  const factor = Math.pow(10, decimals);
  return Math.round((value + Number.EPSILON) * factor) / factor;
}

/**
 * Derives live Stock Reconciliation preview figures (ERPNext stock_reconciliation.js parity):
 * - Blank/null newQuantity or newValuationRate falls back to current value (no false write-off).
 * - Explicit 0 refreshes amounts and differences to 0 / -currentAmount.
 * - Derives currentAmount from currentQuantity * currentValuationRate and rounds diffs cleanly.
 */
export function computeReconciliationPreview(row: ReconciliationFormRow): ReconciliationPreviewValues {
  const curQty = Number(row.currentQuantity ?? 0);
  const curRate = Number(row.currentValuationRate ?? 0);
  const effectiveQty = row.newQuantity === null || row.newQuantity === undefined || (row.newQuantity as unknown) === ''
    ? curQty
    : Number(row.newQuantity);
  const effectiveRate = row.newValuationRate === null || row.newValuationRate === undefined || (row.newValuationRate as unknown) === ''
    ? curRate
    : Number(row.newValuationRate);

  const currentAmount = roundTo(curQty * curRate, 2);
  const newAmount = roundTo(effectiveQty * effectiveRate, 2);
  const quantityDifference = roundTo(effectiveQty - curQty, 4);
  const amountDifference = roundTo(newAmount - currentAmount, 2);

  return {
    effectiveQty,
    effectiveRate,
    currentAmount,
    newAmount,
    quantityDifference,
    amountDifference,
  };
}

export function mapReconciliationPreviewRows(
  items: StockReconciliationItemPreviewDto[] | undefined,
): ReconciliationFormRow[] {
  return (items ?? []).filter(i => !!i.itemId && !!i.warehouseId).map(i => ({
    itemId: i.itemId!,
    warehouseId: i.warehouseId!,
    currentQuantity: i.currentQuantity ?? 0,
    currentValuationRate: i.currentValuationRate ?? 0,
    newQuantity: i.currentQuantity ?? 0,
    newValuationRate: i.currentValuationRate ?? 0,
  }));
}
@Component({
  selector: 'app-sr-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::NewReconciliation' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3 align-items-end">
          <div class="col-md-3">
            <label class="form-label">{{ '::PostingDate' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.postingDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ '::Purpose' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.purpose">
              <option value="Stock Count">{{ '::StockCount' | abpLocalization }}</option>
              <option value="Opening Stock">{{ '::OpeningStock' | abpLocalization }}</option>
              <option value="Adjustment">{{ '::Adjustment' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="fetchWarehouseId">
              <option value="">-- {{ '::SelectWarehouse' | abpLocalization }} --</option>
              @for (w of warehouses(); track w.id) { <option [value]="w.id">{{ w.name }}</option> }
            </select>
          </div>
          <div class="col-md-3">
            <div class="form-check mb-1">
              <input class="form-check-input" type="checkbox" id="ignoreEmpty" [(ngModel)]="ignoreEmptyStock" />
              <label class="form-check-label small" for="ignoreEmpty">{{ '::IgnoreEmptyStock' | abpLocalization }}</label>
            </div>
            <button type="button" class="btn btn-outline-info btn-sm w-100"
              [disabled]="!fetchWarehouseId || fetchingItems" (click)="fetchItemsFromWarehouse()">
              <i class="fa fa-warehouse me-1"></i>{{ '::FetchItemsFromWarehouse' | abpLocalization }}
            </button>
          </div>
        </div>

        <h6 class="mb-2">{{ '::Items' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead>
            <tr>
              <th>{{ '::Item' | abpLocalization }}</th>
              <th>{{ '::Warehouse' | abpLocalization }}</th>
              <th>{{ '::CurrentQty' | abpLocalization }}</th>
              <th>{{ '::CurrentValuationRate' | abpLocalization }}</th>
              <th>{{ '::NewQty' | abpLocalization }}</th>
              <th>{{ '::Rate' | abpLocalization }}</th>
              <th class="text-end">{{ '::QtyDiff' | abpLocalization }}</th>
              <th class="text-end">{{ '::AmountDiff' | abpLocalization }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of form.items; track $index) {
              @let preview = getRowPreview(item);
              <tr>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true; onRowSelectionChanged(item)" [(ngModel)]="item.itemId">
                    <option value="">-- {{ '::SelectItem' | abpLocalization }} --</option>
                    @for (i of availableItems(); track i.id) { <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option> }
                  </select>
                </td>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true; onRowSelectionChanged(item)" [(ngModel)]="item.warehouseId">
                    <option value="">-- --</option>
                    @for (w of warehouses(); track w.id) { <option [value]="w.id">{{ w.name }}</option> }
                  </select>
                </td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.currentQuantity" /></td>
                <td><input type="number" class="form-control form-control-sm" step="0.01" (ngModelChange)="isDirty=true" [(ngModel)]="item.currentValuationRate" /></td>
                <td><input type="number" class="form-control form-control-sm" min="0" (ngModelChange)="isDirty=true" [(ngModel)]="item.newQuantity" /></td>
                <td><input type="number" class="form-control form-control-sm" min="0" step="0.01" (ngModelChange)="isDirty=true" [(ngModel)]="item.newValuationRate" /></td>
                <td class="text-end align-middle font-monospace" [class.text-success]="preview.quantityDifference > 0" [class.text-danger]="preview.quantityDifference < 0">
                  {{ preview.quantityDifference | number:'1.0-4' }}
                </td>
                <td class="text-end align-middle font-monospace" [class.text-success]="preview.amountDifference > 0" [class.text-danger]="preview.amountDifference < 0">
                  {{ preview.amountDifference | number:'1.2-2' }}
                </td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.items.splice($index,1); isDirty=true"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
          <tfoot>
            <tr class="fw-semibold">
              <td colspan="7" class="text-end">{{ '::DifferenceAmount' | abpLocalization }}</td>
              <td class="text-end font-monospace">{{ getTotalDifferenceAmount() | number:'1.2-2' }}</td>
              <td></td>
            </tr>
          </tfoot>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addItem()"><i class="fa fa-plus me-1"></i>{{ '::AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/inventory/stock-reconciliations">{{ '::Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class StockReconciliationFormComponent implements OnInit {
  private stockReconciliationService = inject(StockReconciliationService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private toaster = inject(ToasterService);

  saving = false;
  fetchingItems = false;
  ignoreEmptyStock = true;
  fetchWarehouseId = '';
  isDirty = false;
  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  warehouses = signal<{ id: string; name: string }[]>([]);

  form: any = {
    postingDate: new Date().toISOString().split('T')[0], purpose: 'Stock Count',
    items: [{ itemId: '', warehouseId: '', currentQuantity: 0, currentValuationRate: 0, newQuantity: null, newValuationRate: null }]
  };

  ngOnInit(): void {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName })))
    );
    this.warehouseService.getList({ maxResultCount: 200 } as any).subscribe(r =>
      this.warehouses.set((r.items ?? []).map((w: any) => ({ id: w.id, name: w.warehouseName ?? w.name })))
    );
  }

  addItem() {
    this.form.items.push({
      itemId: '',
      warehouseId: this.fetchWarehouseId || '',
      currentQuantity: 0,
      currentValuationRate: 0,
      newQuantity: null,
      newValuationRate: null,
    });
    this.isDirty = true;
  }

  getRowPreview(row: ReconciliationFormRow): ReconciliationPreviewValues {
    return computeReconciliationPreview(row);
  }

  getTotalDifferenceAmount(): number {
    return roundTo(
      (this.form.items as ReconciliationFormRow[])
        .filter(i => i.itemId && i.warehouseId)
        .reduce((sum, item) => sum + computeReconciliationPreview(item).amountDifference, 0),
      2,
    );
  }

  fetchItemsFromWarehouse(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.fetchWarehouseId) return;
    this.fetchingItems = true;
    this.stockReconciliationService.getItemsForReconciliation({
      companyId,
      warehouseId: this.fetchWarehouseId,
      postingDate: this.form.postingDate,
      ignoreEmptyStock: this.ignoreEmptyStock,
    }).subscribe({
      next: items => {
        const mapped = mapReconciliationPreviewRows(items);
        if (mapped.length > 0) {
          this.form.items = mapped;
          this.isDirty = true;
        }
        this.fetchingItems = false;
      },
      error: () => { this.fetchingItems = false; },
    });
  }

  onRowSelectionChanged(row: ReconciliationFormRow): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !row.itemId || !row.warehouseId) return;
    this.stockReconciliationService.getStockBalanceFor({
      companyId,
      itemId: row.itemId,
      warehouseId: row.warehouseId,
      postingDate: this.form.postingDate,
    }).subscribe({
      next: preview => {
        row.currentQuantity = preview.currentQuantity ?? 0;
        row.currentValuationRate = preview.currentValuationRate ?? 0;
        if (row.newQuantity === null || row.newQuantity === undefined) {
          row.newQuantity = preview.currentQuantity ?? 0;
        }
        if (row.newValuationRate === null || row.newValuationRate === undefined || !row.newValuationRate) {
          row.newValuationRate = preview.currentValuationRate ?? 0;
        }
      },
      error: () => {},
    });
  }

  save() {
    this.saving = true;
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      postingDate: this.form.postingDate,
      purpose: this.form.purpose,
      items: this.form.items
        .filter((i: any) => i.itemId && i.warehouseId)
        .map((i: ReconciliationFormRow) => {
          const preview = computeReconciliationPreview(i);
          return {
            itemId: i.itemId,
            warehouseId: i.warehouseId,
            currentQuantity: i.currentQuantity ?? 0,
            currentValuationRate: i.currentValuationRate ?? 0,
            newQuantity: preview.effectiveQty,
            newValuationRate: preview.effectiveRate,
          };
        })
    };
    this.stockReconciliationService.create(dto).subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/inventory/stock-reconciliations']); },
      error: (err) => {
        this.saving = false;
        this.toaster.error(err?.error?.error?.message || '::FailedToSaveStockReconciliation');
      }
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}