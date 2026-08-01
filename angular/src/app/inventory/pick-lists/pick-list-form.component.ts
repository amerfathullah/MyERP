import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PickListService } from '../../proxy/inventory/pick-list.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { ItemService } from '../../proxy/inventory/item.service';

@Component({
  selector: 'app-pick-list-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe, SaveShortcutDirective, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid py-3">
      <h4 class="mb-3">
        <i class="fa fa-clipboard-list me-2 text-primary"></i>
        {{ '::NewPickList' | abpLocalization }}
      </h4>

      <form [formGroup]="form" (appSaveShortcut)="save()">
        <div class="card shadow-sm mb-3">
          <div class="card-header"><h6 class="mb-0">{{ '::PickListDetails' | abpLocalization }}</h6></div>
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-4">
                <label class="form-label">{{ '::Purpose' | abpLocalization }} *</label>
                <select class="form-select" formControlName="purpose">
                  <option value="Delivery">{{ '::Delivery' | abpLocalization }}</option>
                  <option value="MaterialTransfer">{{ '::MaterialTransfer' | abpLocalization }}</option>
                  <option value="MaterialTransferForManufacture">{{ '::MaterialTransferForManufacture' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ '::Customer' | abpLocalization }}</label>
                <select class="form-select" formControlName="customerId">
                  <option value="">{{ '::SelectCustomer' | abpLocalization }}</option>
                  @for (c of customers(); track c.id) {
                    <option [value]="c.id">{{ c.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ '::SalesOrder' | abpLocalization }}</label>
                <select class="form-select" formControlName="salesOrderId">
                  <option value="">{{ '::None' | abpLocalization }}</option>
                  @for (so of salesOrders(); track so.id) {
                    <option [value]="so.id">{{ so.orderNumber }}</option>
                  }
                </select>
              </div>
            </div>
          </div>
        </div>

        <!-- Items -->
        <div class="card shadow-sm mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">{{ '::Items' | abpLocalization }}</h6>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addItem()">
              <i class="fa fa-plus me-1"></i>{{ '::AddItem' | abpLocalization }}
            </button>
          </div>
          <div class="card-body p-0">
            <table class="table mb-0">
              <thead>
                <tr>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>{{ '::Quantity' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody formArrayName="items">
                @for (item of itemsArray.controls; track $index; let i = $index) {
                  <tr [formGroupName]="i">
                    <td>
                      <select class="form-select form-select-sm" formControlName="itemId">
                        <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                        @for (it of availableItems(); track it.id) {
                          <option [value]="it.id">{{ it.itemCode }} — {{ it.itemName }}</option>
                        }
                      </select>
                    </td>
                    <td>
                      <select class="form-select form-select-sm" formControlName="warehouseId">
                        <option value="">{{ '::SelectWarehouse' | abpLocalization }}</option>
                        @for (w of warehouses(); track w.id) {
                          <option [value]="w.id">{{ w.name }}</option>
                        }
                      </select>
                    </td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="quantity" min="0.01" step="0.01"></td>
                    <td>
                      <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeItem(i)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <button type="button" class="btn btn-outline-secondary" routerLink="/inventory/pick-lists">{{ '::Cancel' | abpLocalization }}</button>
          <button type="button" class="btn btn-primary" (click)="save()" [disabled]="saving()">
            <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </div>
  `,
})
export class PickListFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(PickListService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private salesOrderProxyService = inject(SalesOrderService);
  private warehouseService = inject(WarehouseService);
  private customerService = inject(CustomerService);
  private itemService = inject(ItemService);

  saving = signal(false);
  customers = signal<any[]>([]);
  salesOrders = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  warehouses = signal<any[]>([]);

  form = this.fb.group({
    companyId: ['', Validators.required],
    purpose: ['Delivery'],
    customerId: [''],
    salesOrderId: [''],
    items: this.fb.array([]),
  });

  get itemsArray() { return this.form.get('items') as FormArray; }

  ngOnInit() {
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });

    const params = this.route.snapshot.queryParams;
    if (params['salesOrderId']) this.form.patchValue({ salesOrderId: params['salesOrderId'] });
    if (params['customerId']) this.form.patchValue({ customerId: params['customerId'] });
    if (params['companyId']) this.form.patchValue({ companyId: params['companyId'] });

    this.customerService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: (res: any) => this.customers.set((res.items || []).map((c: any) => ({ id: c.id, name: c.name || c.customerName }))),
      error: () => {},
    });
    this.itemService.getList({ skipCount: 0, maxResultCount: 500 } as any).subscribe({
      next: (res: any) => this.availableItems.set(res.items || []),
      error: () => {},
    });
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: (res: any) => this.warehouses.set(res.items || []),
      error: () => {},
    });
    this.salesOrderProxyService.getList({ skipCount: 0, maxResultCount: 100, status: 2 } as any).subscribe({
      next: (res: any) => this.salesOrders.set(res.items || []),
      error: () => {},
    });

    this.addItem();
  }

  addItem() {
    this.itemsArray.push(this.fb.group({
      itemId: ['', Validators.required],
      warehouseId: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
    }));
  }

  removeItem(i: number) { this.itemsArray.removeAt(i); }

  save() {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);

    const raw = this.form.getRawValue();
    const dto = {
      companyId: raw.companyId,
      purpose: raw.purpose,
      customerId: raw.customerId || undefined,
      salesOrderId: raw.salesOrderId || undefined,
      items: (raw.items || []).filter((i: any) => i.itemId).map((i: any) => ({
        itemId: i.itemId,
        warehouseId: i.warehouseId,
        quantity: i.quantity,
      })),
    };

    this.service.create(dto as any).subscribe({
      next: (created) => {
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/inventory/pick-lists', created.id]);
      },
      error: () => this.saving.set(false),
    });
  }

  hasUnsavedChanges() { return this.form.dirty; }
}
