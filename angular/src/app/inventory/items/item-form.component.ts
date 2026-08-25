import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { barcodeTypeOptions } from '../../proxy/inventory/barcode-type.enum';
import { valuationMethodOptions } from '../../proxy/inventory/valuation-method.enum';
import { materialRequestTypeOptions } from '../../proxy/purchasing/material-request-type.enum';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ItemService } from '../../proxy/inventory/item.service';
import { CompanyService } from '../../proxy/core/company.service';
import { StockBalanceService } from '../../proxy/inventory/stock-balance.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { ItemStore } from '../store/item.store';
import { ToasterService } from '@abp/ng.theme.shared';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyRestrictionComponent } from '../../shared/components/company-restriction/company-restriction.component';

@Component({
  selector: 'app-item-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, CompanyRestrictionComponent, CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule, RouterModule],
  templateUrl: './item-form.component.html',
  styleUrls: ['./item-form.component.scss'],
})
export class ItemFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private companyService = inject(CompanyService);
  private stockBalanceService = inject(StockBalanceService);
  private supplierService = inject(SupplierService);
  private customerService = inject(CustomerService);
  private warehouseService = inject(WarehouseService);
  private store = inject(ItemStore);
  private service = inject(ItemService);
  private toaster = inject(ToasterService);

  stockLevels = signal<any[]>([]);
  companies = signal<any[]>([]);
  suppliers = signal<any[]>([]);
  customers = signal<any[]>([]);
  warehouses = signal<any[]>([]);

  materialRequestTypeOptions = materialRequestTypeOptions;
  valuationMethodOptions = valuationMethodOptions;

  form = this.fb.group({
    companyId: ['', Validators.required],
    itemCode: ['', [Validators.required, Validators.maxLength(50)]],
    itemName: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    itemType: [0, Validators.required],
    itemGroup: [''],
    uom: ['Unit'],
    valuationMethod: [0],
    standardSellingPrice: [0, Validators.min(0)],
    standardBuyingPrice: [0, Validators.min(0)],
    maintainStock: [true],
    isActive: [true],
    reorderLevel: [0, Validators.min(0)],
    reorderQty: [0, Validators.min(0)],
    safetyStock: [0, Validators.min(0)],
    minOrderQty: [0, Validators.min(0)],
    inspectionRequiredBeforePurchase: [false],
    inspectionRequiredBeforeDelivery: [false],
    barcodes: this.fb.array([]),
    suppliers: this.fb.array([]),
    customerDetails: this.fb.array([]),
    reorders: this.fb.array([]),
  });

  isEditMode = false;
  entityId: string | null = null;

  itemTypes = [
    { value: 0, label: 'Goods' },
    { value: 1, label: 'Service' },
    { value: 2, label: 'Fixed Asset' }];

  barcodeTypeOptions = barcodeTypeOptions;

  get barcodesArray(): FormArray {
    return this.form.get('barcodes') as FormArray;
  }

  addBarcode(barcode = '', barcodeType = 0, isDefault = false): void {
    this.barcodesArray.push(this.fb.group({
      barcode: [barcode, Validators.required],
      barcodeType: [barcodeType],
      isDefault: [isDefault],
    }));
  }

  removeBarcode(index: number): void {
    this.barcodesArray.removeAt(index);
  }

  get suppliersArray(): FormArray {
    return this.form.get('suppliers') as FormArray;
  }

  addSupplier(supplierId = '', supplierPartNo = ''): void {
    this.suppliersArray.push(this.fb.group({
      supplierId: [supplierId, Validators.required],
      supplierPartNo: [supplierPartNo],
    }));
  }

  removeSupplier(index: number): void {
    this.suppliersArray.removeAt(index);
  }

  get customerDetailsArray(): FormArray {
    return this.form.get('customerDetails') as FormArray;
  }

  addCustomerDetail(customerId = '', refCode = ''): void {
    this.customerDetailsArray.push(this.fb.group({
      customerId: [customerId, Validators.required],
      refCode: [refCode, Validators.required],
    }));
  }

  removeCustomerDetail(index: number): void {
    this.customerDetailsArray.removeAt(index);
  }

  get reordersArray(): FormArray {
    return this.form.get('reorders') as FormArray;
  }

  addReorder(
    warehouseId = '', warehouseGroupId = '',
    warehouseReorderLevel = 0, warehouseReorderQty = 0, materialRequestType = 0,
  ): void {
    this.reordersArray.push(this.fb.group({
      warehouseId: [warehouseId, Validators.required],
      warehouseGroupId: [warehouseGroupId],
      warehouseReorderLevel: [warehouseReorderLevel],
      warehouseReorderQty: [warehouseReorderQty],
      materialRequestType: [materialRequestType],
    }));
  }

  removeReorder(index: number): void {
    this.reordersArray.removeAt(index);
  }

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(
      res => this.companies.set(res.items ?? []));

    // New items default to the selected company's configured valuation method (per
    // stock-ledger-engine's documented fallback chain). Existing items keep whatever
    // is already patched onto them below — never overwritten by a company change.
    if (!this.isEditMode) {
      this.form.get('companyId')?.valueChanges.subscribe((companyId) => {
        const company = this.companies().find(c => c.id === companyId);
        if (company?.defaultValuationMethod != null) {
          this.form.patchValue({ valuationMethod: company.defaultValuationMethod });
        }
      });
    }
    this.supplierService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }).subscribe(
      res => this.suppliers.set(res.items ?? []));
    this.customerService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }).subscribe(
      res => this.customers.set(res.items ?? []));
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }).subscribe(
      res => this.warehouses.set(res.items ?? []));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((item) => {
        this.form.patchValue(item as any);
        for (const b of item.barcodes ?? []) {
          this.addBarcode(b.barcode, b.barcodeType, b.isDefault);
        }
        for (const s of item.suppliers ?? []) {
          this.addSupplier(s.supplierId, s.supplierPartNo ?? '');
        }
        for (const c of item.customerDetails ?? []) {
          this.addCustomerDetail(c.customerId, c.refCode);
        }
        for (const r of item.reorders ?? []) {
          this.addReorder(r.warehouseId, r.warehouseGroupId ?? '', r.warehouseReorderLevel, r.warehouseReorderQty, r.materialRequestType);
        }
        // Load stock levels for this item
        this.stockBalanceService.getItemStock(this.entityId!)
          .subscribe(levels => this.stockLevels.set(levels ?? []));
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue() as any;
    for (const r of value.reorders ?? []) {
      r.warehouseGroupId = r.warehouseGroupId || null;
    }

    if (this.isEditMode) {
      this.service.update(this.entityId!, value).subscribe({
        next: () => this.router.navigate(['/inventory/items']),
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    } else {
      this.service.create(value).subscribe({
        next: () => this.router.navigate(['/inventory/items']),
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
