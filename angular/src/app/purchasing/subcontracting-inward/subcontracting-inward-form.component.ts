import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SubcontractingInwardOrderService } from '../../proxy/purchasing/subcontracting-inward-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-subcontracting-inward-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">{{ 'MyERP::NewSubcontractingInwardOrder' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Supplier' | abpLocalization }} *</label>
              <select class="form-select" formControlName="supplierId">
                <option value="">{{ '::SelectSupplier' | abpLocalization }}</option>
                @for (s of suppliers(); track s.id) {
                  <option [value]="s.id">{{ s.name || s.supplierName }}</option>
                }
              </select>
              @if (form.get('supplierId')?.invalid && form.get('supplierId')?.touched) {
                <div class="text-danger small mt-1">{{ '::RequiredField' | abpLocalization }}</div>
              }
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::OrderDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="orderDate" />
              @if (form.get('orderDate')?.invalid && form.get('orderDate')?.touched) {
                <div class="text-danger small mt-1">{{ '::RequiredField' | abpLocalization }}</div>
              }
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Currency' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="currencyCode" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::SalesOrder' | abpLocalization }}</label>
              <select class="form-select" formControlName="salesOrderId">
                <option value="">{{ '::SelectSalesOrder' | abpLocalization }}</option>
                @for (so of salesOrders(); track so.id) {
                  <option [value]="so.id">{{ so.orderNumber }}</option>
                }
              </select>
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::SubcontractingOrder' | abpLocalization }} (SCO ID)</label>
              <input type="text" class="form-control" formControlName="subcontractingOrderId" placeholder="SCO Reference Guid / Number" />
            </div>
          </div>

          <div class="d-flex justify-content-between align-items-center mt-4 mb-2">
            <h6 class="mb-0">{{ 'MyERP::Items' | abpLocalization }}</h6>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addItem()">
              <i class="fa fa-plus me-1"></i> {{ '::AddRow' | abpLocalization }}
            </button>
          </div>

          <div class="table-responsive mb-3">
            <table class="table table-bordered table-sm">
              <thead class="table-light">
                <tr>
                  <th style="width: 30%">{{ 'Item' | abpLocalization }} *</th>
                  <th style="width: 15%">{{ 'Warehouse' | abpLocalization }}</th>
                  <th style="width: 12%" class="text-end">{{ 'Quantity' | abpLocalization }} *</th>
                  <th style="width: 12%" class="text-end">{{ 'Rate' | abpLocalization }} *</th>
                  <th style="width: 15%" class="text-end">{{ 'ServiceCostPerQty' | abpLocalization }}</th>
                  <th style="width: 12%" class="text-end">{{ 'Amount' | abpLocalization }}</th>
                  <th style="width: 4%"></th>
                </tr>
              </thead>
              <tbody formArrayName="items">
                @for (itemCtrl of itemsArray.controls; track $index; let i = $index) {
                  <tr [formGroupName]="i">
                    <td>
                      <select class="form-select form-select-sm" formControlName="itemId">
                        <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                        @for (it of items(); track it.id) {
                          <option [value]="it.id">{{ it.itemCode }} - {{ it.itemName }}</option>
                        }
                      </select>
                    </td>
                    <td>
                      <select class="form-select form-select-sm" formControlName="warehouseId">
                        <option value="">{{ '::SelectWarehouse' | abpLocalization }}</option>
                        @for (w of warehouses(); track w.id) {
                          <option [value]="w.id">{{ w.warehouseName || w.name }}</option>
                        }
                      </select>
                    </td>
                    <td>
                      <input type="number" step="0.01" class="form-control form-control-sm text-end"
                        formControlName="quantity" (input)="recalculateTotal()" />
                    </td>
                    <td>
                      <input type="number" step="0.01" class="form-control form-control-sm text-end"
                        formControlName="rate" (input)="recalculateTotal()" />
                    </td>
                    <td>
                      <input type="number" step="0.01" class="form-control form-control-sm text-end"
                        formControlName="serviceCostPerQty" />
                    </td>
                    <td class="text-end align-middle fw-semibold">
                      {{ getItemAmount(i) | number:'1.2-2' }}
                    </td>
                    <td class="text-center align-middle">
                      <button type="button" class="btn btn-link text-danger p-0" (click)="removeItem(i)"
                        [disabled]="itemsArray.length <= 1">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr class="fw-bold table-light">
                  <td colspan="5" class="text-end">{{ 'GrandTotal' | abpLocalization }}</td>
                  <td class="text-end">{{ grandTotal() | number:'1.2-2' }}</td>
                  <td></td>
                </tr>
              </tfoot>
            </table>
          </div>

          <div class="d-flex justify-content-end gap-2 mt-4">
            <button type="button" class="btn btn-outline-secondary" routerLink="/purchasing/subcontracting-inward">
              {{ '::Cancel' | abpLocalization }}
            </button>
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
              @if (isSaving()) {
                <i class="fa fa-spinner fa-spin me-1"></i>
              }
              {{ '::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class SubcontractingInwardFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private inwardService = inject(SubcontractingInwardOrderService);
  private supplierService = inject(SupplierService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private salesOrderService = inject(SalesOrderService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  suppliers = signal<any[]>([]);
  items = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  salesOrders = signal<any[]>([]);
  grandTotal = signal<number>(0);
  isSaving = signal(false);

  form: FormGroup = this.fb.group({
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    orderDate: [new Date().toISOString().substring(0, 10), Validators.required],
    currencyCode: ['MYR', Validators.required],
    salesOrderId: [''],
    subcontractingOrderId: [''],
    items: this.fb.array([])
  });

  get itemsArray(): FormArray {
    return this.form.get('items') as FormArray;
  }

  ngOnInit() {
    const companyId = this.companyContext.selectedCompanyId() || '00000000-0000-0000-0000-000000000001';
    this.form.patchValue({ companyId });

    this.addItem();

    this.supplierService.getList({ maxResultCount: 1000, skipCount: 0 } as any).subscribe({
      next: (res: any) => this.suppliers.set(res.items || [])
    });

    this.itemService.getList({ maxResultCount: 1000, skipCount: 0 } as any).subscribe({
      next: (res: any) => this.items.set(res.items || [])
    });

    this.warehouseService.getList({ maxResultCount: 1000, skipCount: 0 } as any).subscribe({
      next: (res: any) => this.warehouses.set(res.items || [])
    });

    this.salesOrderService.getList({ maxResultCount: 500, skipCount: 0 } as any).subscribe({
      next: (res: any) => this.salesOrders.set(res.items || [])
    });
  }

  addItem() {
    const itemGroup = this.fb.group({
      itemId: ['', Validators.required],
      warehouseId: [''],
      quantity: [1, [Validators.required, Validators.min(0.0001)]],
      rate: [0, [Validators.required, Validators.min(0)]],
      serviceCostPerQty: [0]
    });
    this.itemsArray.push(itemGroup);
    this.recalculateTotal();
  }

  removeItem(index: number) {
    if (this.itemsArray.length > 1) {
      this.itemsArray.removeAt(index);
      this.recalculateTotal();
    }
  }

  getItemAmount(index: number): number {
    const row = this.itemsArray.at(index)?.value;
    if (!row) return 0;
    const qty = Number(row.quantity) || 0;
    const rate = Number(row.rate) || 0;
    return qty * rate;
  }

  recalculateTotal() {
    let total = 0;
    for (let i = 0; i < this.itemsArray.length; i++) {
      total += this.getItemAmount(i);
    }
    this.grandTotal.set(total);
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    const val = this.form.value;

    const dto = {
      companyId: val.companyId,
      supplierId: val.supplierId,
      orderDate: val.orderDate,
      currencyCode: val.currencyCode || 'MYR',
      salesOrderId: val.salesOrderId || null,
      subcontractingOrderId: val.subcontractingOrderId || null,
      items: val.items.map((it: any) => ({
        itemId: it.itemId,
        warehouseId: it.warehouseId || null,
        quantity: Number(it.quantity) || 0,
        rate: Number(it.rate) || 0,
        serviceCostPerQty: Number(it.serviceCostPerQty) || 0
      }))
    };

    this.inwardService.create(dto as any).subscribe({
      next: (res) => {
        this.toaster.success('Subcontracting inward order created successfully');
        this.router.navigate(['/purchasing/subcontracting-inward', res.id]);
      },
      error: (err) => {
        this.isSaving.set(false);
        this.toaster.error(err?.error?.error?.message || 'Failed to create subcontracting inward order');
      }
    });
  }
}
