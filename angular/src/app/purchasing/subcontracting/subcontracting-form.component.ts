import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SubcontractingService } from '../../proxy/purchasing/subcontracting.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';
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
                  <td><input type="number" class="form-control form-control-sm" [(ngModel)]="item.qty" [name]="'qty'+$index" min="1" /></td>
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
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  suppliers = signal<{ id: string; name: string }[]>([]);
  items = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
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
