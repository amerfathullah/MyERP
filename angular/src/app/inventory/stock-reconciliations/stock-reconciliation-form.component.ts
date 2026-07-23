import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StockReconciliationService } from '../../proxy/inventory';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
@Component({
  selector: 'app-sr-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewReconciliation' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.postingDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Purpose' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.purpose">
              <option value="Stock Count">{{ 'StockCount' | abpLocalization }}</option>
              <option value="Opening Stock">{{ 'OpeningStock' | abpLocalization }}</option>
              <option value="Adjustment">{{ 'Adjustment' | abpLocalization }}</option>
            </select>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Items' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th>{{ 'Warehouse' | abpLocalization }}</th><th>{{ 'CurrentQty' | abpLocalization }}</th><th>{{ 'NewQty' | abpLocalization }}</th><th>{{ 'Rate' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (item of form.items; track $index) {
              <tr>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.itemId">
                    <option value="">-- {{ 'SelectItem' | abpLocalization }} --</option>
                    @for (i of availableItems(); track i.id) { <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option> }
                  </select>
                </td>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.warehouseId">
                    <option value="">-- --</option>
                    @for (w of warehouses(); track w.id) { <option [value]="w.id">{{ w.name }}</option> }
                  </select>
                </td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.currentQuantity" /></td>
                <td><input type="number" class="form-control form-control-sm" min="0" (ngModelChange)="isDirty=true" [(ngModel)]="item.newQuantity" /></td>
                <td><input type="number" class="form-control form-control-sm" min="0" step="0.01" (ngModelChange)="isDirty=true" [(ngModel)]="item.newValuationRate" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.items.splice($index,1); isDirty=true"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addItem()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/inventory/stock-reconciliations">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
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

  saving = false;
  isDirty = false;
  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  warehouses = signal<{ id: string; name: string }[]>([]);

  form: any = {
    postingDate: new Date().toISOString().split('T')[0], purpose: 'Stock Count',
    items: [{ itemId: '', warehouseId: '', currentQuantity: 0, newQuantity: 0, newValuationRate: 0 }]
  };

  ngOnInit(): void {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName })))
    );
    this.warehouseService.getList({ maxResultCount: 200 } as any).subscribe(r =>
      this.warehouses.set((r.items ?? []).map((w: any) => ({ id: w.id, name: w.warehouseName ?? w.name })))
    );
  }

  addItem() { this.form.items.push({ itemId: '', warehouseId: '', currentQuantity: 0, newQuantity: 0, newValuationRate: 0 }); this.isDirty = true; }

  save() {
    this.saving = true;
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      postingDate: this.form.postingDate,
      purpose: this.form.purpose,
      items: this.form.items
        .filter((i: any) => i.itemId && i.warehouseId)
        .map((i: any) => ({
          itemId: i.itemId, warehouseId: i.warehouseId,
          currentQuantity: i.currentQuantity || 0, currentValuationRate: 0,
          newQuantity: i.newQuantity || 0, newValuationRate: i.newValuationRate || 0,
        }))
    };
    this.stockReconciliationService.create(dto).subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/inventory/stock-reconciliations']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}