import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { CompanyService } from '../../proxy/core/company.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import type { SupplierDto } from '../../proxy/purchasing/models';
import type { CompanyDto } from '../../proxy/core/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-purchase-order-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe, AutoValidationDirective, SaveShortcutDirective],
  templateUrl: './purchase-order-form.component.html',
  styleUrls: ['./purchase-order-form.component.scss'],
})
export class PurchaseOrderFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(PurchaseOrderService);
  private supplierService = inject(SupplierService);
  private companyService = inject(CompanyService);
  private itemService = inject(ItemService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private warehouseService = inject(WarehouseService);

  companies = signal<CompanyDto[]>([]);
  suppliers = signal<SupplierDto[]>([]);
  availableItems = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  isEditMode = false;
  entityId: string | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal', 'actions'];

  form = this.fb.group({
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    orderDate: [new Date().toISOString().split('T')[0], Validators.required],
    expectedDeliveryDate: [''],
    notes: [''],
    warehouseId: [''],
    items: this.fb.array([], Validators.minLength(1)),
  });

  get items(): FormArray { return this.form.get('items') as FormArray; }

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    // Auto-set companyId from company context for new documents
    if (!this.isEditMode && !this.form?.get?.('companyId')?.value) {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(r => this.companies.set(r.items ?? []));
    this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' })
      .subscribe(r => this.suppliers.set(r.items ?? []));
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'itemCode asc' } as any)
      .subscribe(r => this.availableItems.set(r.items ?? []));
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe(r => this.warehouses.set((r.items ?? []).filter((w: any) => !w.isGroup)));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(po => {
        // Resolve warehouse from first item (header-level representation)
        const itemWarehouse = (po.items ?? []).find((i: any) => i.warehouseId)?.warehouseId ?? '';
        this.form.patchValue({
          companyId: po.companyId,
          supplierId: po.supplierId,
          orderDate: po.orderDate,
          expectedDeliveryDate: po.expectedDeliveryDate ?? '',
          notes: '',
          warehouseId: itemWarehouse,
        });
        po.items?.forEach(item => this.addItemRow(item));
      });
    } else {
      this.addItemRow();
    }
  }

  addItemRow(item?: any): void {
    this.items.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [item?.taxAmount ?? 0, Validators.min(0)],
      uom: [item?.uom ?? 'Unit'],
    }));
  }

  removeItemRow(index: number): void {
    this.items.removeAt(index);
  }

  onItemSelected(index: number, itemId: string): void {
    const item = this.availableItems().find((i: any) => i.id === itemId);
    if (item) {
      const row = this.items.at(index) as FormGroup;
      row.patchValue({ description: item.itemName || item.itemCode });
    }
  }

  getLineTotal(row: FormGroup): number {
    const qty = row.get('quantity')?.value ?? 0;
    const price = row.get('unitPrice')?.value ?? 0;
    const tax = row.get('taxAmount')?.value ?? 0;
    return qty * price + tax;
  }

  get netTotal(): number {
    return this.items.controls.reduce((sum, row) => {
      const g = row as FormGroup;
      return sum + (g.get('quantity')?.value ?? 0) * (g.get('unitPrice')?.value ?? 0);
    }, 0);
  }

  get taxTotal(): number {
    return this.items.controls.reduce((sum, row) => {
      return sum + ((row as FormGroup).get('taxAmount')?.value ?? 0);
    }, 0);
  }

  get grandTotal(): number { return this.netTotal + this.taxTotal; }

  save(): void {
    if (this.form.invalid || this.items.length === 0) {
      this.form.markAllAsTouched();
      if (this.items.length === 0) this.toaster.warn('Add at least one item');
      return;
    }
    const raw = this.form.getRawValue() as any;
    const warehouseId = raw.warehouseId || null;
    const dto = {
      ...raw,
      items: (raw.items ?? []).map((item: any) => ({
        ...item,
        warehouseId,
      })),
    };
    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe({
        next: () => this.router.navigate(['/purchasing/orders', this.entityId]),
        error: () => { /* handled by global error interceptor */ },
      });
    } else {
      this.service.create(dto).subscribe({
        next: () => this.router.navigate(['/purchasing/orders']),
        error: () => { /* handled by global error interceptor */ },
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/purchasing/orders']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
