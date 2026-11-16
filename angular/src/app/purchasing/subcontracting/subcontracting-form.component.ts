import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SubcontractingService } from '../../proxy/purchasing/subcontracting.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { SubcontractingBomItemsDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

interface ScoFormItem {
  itemId: string;
  itemName: string;
  qty: number;
  rate: number;
  bomId: string;
  warehouseId: string;
}

@Component({
  selector: 'app-subcontracting-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0"><i class="bi bi-people me-2"></i>{{ 'MyERP::NewSubcontractingOrder' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'MyERP::Supplier' | abpLocalization }} *</label>
            <select class="form-select" [(ngModel)]="form.supplierId" name="supplier">
              <option value="">-- Select Supplier --</option>
              @for (s of suppliers(); track s.id) {
                <option [value]="s.id">{{ s.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'MyERP::OrderDate' | abpLocalization }} *</label>
            <input type="date" class="form-control" [(ngModel)]="form.orderDate" name="orderDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'MyERP::PurchaseOrder' | abpLocalization }}</label>
            <input type="text" class="form-control" [(ngModel)]="form.purchaseOrderId" name="poId" [placeholder]="'::Placeholder:PurchaseOrderId' | abpLocalization" />
          </div>
        </div>

        <div class="row mb-3">
          <div class="col-12">
            <label class="form-label">{{ 'MyERP::Notes' | abpLocalization }}</label>
            <textarea class="form-control" [(ngModel)]="form.notes" name="notes" rows="2"></textarea>
          </div>
        </div>

        <h6 class="mt-4 mb-2">{{ 'MyERP::Items' | abpLocalization }} (Finished Goods to Subcontract)</h6>
        <div class="table-responsive">
          <table class="table table-sm table-bordered">
            <thead class="table-light">
              <tr>
                <th>{{ 'MyERP::Item' | abpLocalization }} *</th>
                <th>{{ 'MyERP::Qty' | abpLocalization }} *</th>
                <th>{{ 'MyERP::Rate' | abpLocalization }}</th>
                <th>{{ 'MyERP::Amount' | abpLocalization }}</th>
                <th style="width: 50px;"></th>
              </tr>
            </thead>
            <tbody>
              @for (item of form.items; track $index) {
                <tr>
                  <td>
                    <select class="form-select form-select-sm" [(ngModel)]="item.itemId" [name]="'item'+$index"
                      (ngModelChange)="onItemChange($index)">
                      <option value="">--</option>
                      @for (i of items(); track i.id) {
                        <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option>
                      }
                    </select>
                  </td>
                  <td><input type="number" class="form-control form-control-sm" [(ngModel)]="item.qty" [name]="'qty'+$index" min="1" (ngModelChange)="onItemChange($index)" /></td>
                  <td><input type="number" class="form-control form-control-sm" [(ngModel)]="item.rate" [name]="'rate'+$index" step="0.01" /></td>
                  <td class="text-end font-monospace align-middle">{{ (item.qty * item.rate) | number:'1.2-2' }}</td>
                  <td>
                    <button class="btn btn-sm btn-outline-danger" (click)="removeItem($index)"><i class="bi bi-trash"></i></button>
                  </td>
                </tr>
              }
            </tbody>
            <tfoot>
              <tr>
                <td colspan="3" class="text-end fw-bold">Total</td>
                <td class="text-end font-monospace fw-bold">{{ grandTotal | number:'1.2-2' }}</td>
                <td></td>
              </tr>
            </tfoot>
          </table>
        </div>
        <button class="btn btn-sm btn-outline-secondary mb-3" (click)="addItem()">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::AddRow' | abpLocalization }}
        </button>

        @if (bomPreview(); as preview) {
          @if (preview.items?.length) {
            <div class="card bg-light mb-3">
              <div class="card-header py-2 d-flex justify-content-between align-items-center">
                <span class="fw-semibold small">{{ '::SubcontractingBomRawMaterials' | abpLocalization }}</span>
                @if (preview.bomNumber) {
                  <span class="badge bg-info text-dark">{{ preview.bomNumber }}</span>
                }
              </div>
              <div class="card-body p-0">
                <table class="table table-sm mb-0">
                  <thead>
                    <tr>
                      <th>{{ '::Item' | abpLocalization }}</th>
                      <th>{{ '::UOM' | abpLocalization }}</th>
                      <th class="text-end">{{ '::RequiredQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (rm of preview.items; track rm.itemId ?? $index) {
                      <tr>
                        <td>{{ rm.itemCode ? (rm.itemCode + ' — ') : '' }}{{ rm.itemName }}</td>
                        <td>{{ rm.uom || '—' }}</td>
                        <td class="text-end">{{ rm.requiredQty | number:'1.2-2' }}</td>
                        <td class="text-end">{{ rm.rate | number:'1.2-2' }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        }

        <div class="d-flex justify-content-end gap-2 mt-4">
          <a routerLink="/purchasing/subcontracting" class="btn btn-secondary">{{ 'MyERP::Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving || !form.supplierId || form.items.length === 0">
            <i class="bi bi-check-lg me-1"></i>{{ 'MyERP::Save' | abpLocalization }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class SubcontractingFormComponent implements OnInit {
  private service = inject(SubcontractingService);
  private supplierService = inject(SupplierService);
  private itemService = inject(ItemService);
  private mfgService = inject(ManufacturingService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  suppliers = signal<{ id: string; name: string }[]>([]);
  items = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  bomPreview = signal<SubcontractingBomItemsDto | null>(null);
  saving = false;

  form = {
    supplierId: '',
    orderDate: new Date().toISOString().substring(0, 10),
    purchaseOrderId: '',
    notes: '',
    items: [{ itemId: '', itemName: '', qty: 1, rate: 0, bomId: '', warehouseId: '' }] as ScoFormItem[],
  };

  get grandTotal(): number {
    return this.form.items.reduce((s, i) => s + i.qty * i.rate, 0);
  }

  ngOnInit() {
    this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe(res => {
      this.suppliers.set((res.items ?? []).map((s: any) => ({ id: s.id, name: s.name ?? s.supplierName ?? s.id })));
    });
    this.itemService.getList({ skipCount: 0, maxResultCount: 500 } as any).subscribe(res => {
      this.items.set((res.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode ?? '', itemName: i.itemName ?? '' })));
    });
  }

  addItem() {
    this.form.items.push({ itemId: '', itemName: '', qty: 1, rate: 0, bomId: '', warehouseId: '' });
  }

  removeItem(idx: number) {
    this.form.items.splice(idx, 1);
  }

  onItemChange(idx: number) {
    const item = this.form.items[idx];
    const found = this.items().find(i => i.id === item.itemId);
    if (found) item.itemName = `${found.itemCode} - ${found.itemName}`;
    const companyId = this.companyContext.currentCompanyId() ?? '';
    if (!item.itemId || !companyId) {
      this.bomPreview.set(null);
      return;
    }
    this.mfgService.getBomItemsForSubcontracting(item.itemId, companyId, item.qty || 1).subscribe({
      next: (bom) => {
        if (bom?.bomId) item.bomId = bom.bomId;
        if (bom?.sourceWarehouseId && !item.warehouseId) item.warehouseId = bom.sourceWarehouseId;
        this.bomPreview.set(bom ?? null);
      },
      error: () => this.bomPreview.set(null),
    });
  }

  save() {
    this.saving = true;
    const payload = {
      companyId: this.companyContext.currentCompanyId(),
      supplierId: this.form.supplierId,
      orderDate: this.form.orderDate,
      purchaseOrderId: this.form.purchaseOrderId || undefined,
      notes: this.form.notes || undefined,
      items: this.form.items
        .filter(i => i.itemId)
        .map(i => ({ itemId: i.itemId, itemName: i.itemName, qty: i.qty, rate: i.rate, bomId: i.bomId || undefined, warehouseId: i.warehouseId || undefined })),
    };

    this.service.createOrder(payload).subscribe({
      next: (result) => {
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['/purchasing/subcontracting', result.id]);
      },
      error: () => { this.saving = false; },
    });
  }
}
