import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { barcodeTypeOptions } from '../../proxy/inventory/barcode-type.enum';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ItemService } from '../../proxy/inventory/item.service';
import { CompanyService } from '../../proxy/core/company.service';
import { StockBalanceService } from '../../proxy/inventory/stock-balance.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { ItemStore } from '../store/item.store';

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
  private store = inject(ItemStore);
  private service = inject(ItemService);

  stockLevels = signal<any[]>([]);
  companies = signal<any[]>([]);
  suppliers = signal<any[]>([]);
  customers = signal<any[]>([]);

  form = this.fb.group({
    companyId: ['', Validators.required],
    itemCode: ['', [Validators.required, Validators.maxLength(50)]],
    itemName: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    itemType: [0, Validators.required],
    itemGroup: [''],
    uom: ['Unit'],
    standardSellingPrice: [0],
    standardBuyingPrice: [0],
    maintainStock: [true],
    isActive: [true],
    reorderLevel: [0],
    reorderQty: [0],
    safetyStock: [0],
    minOrderQty: [0],
    inspectionRequiredBeforePurchase: [false],
    inspectionRequiredBeforeDelivery: [false],
    barcodes: this.fb.array([]),
    suppliers: this.fb.array([]),
    customerDetails: this.fb.array([]),
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

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(
      res => this.companies.set(res.items ?? []));
    this.supplierService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }).subscribe(
      res => this.suppliers.set(res.items ?? []));
    this.customerService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }).subscribe(
      res => this.customers.set(res.items ?? []));

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

    if (this.isEditMode) {
      this.service.update(this.entityId!, value).subscribe({
        next: () => this.router.navigate(['/inventory/items']),
        error: () => {},
      });
    } else {
      this.service.create(value).subscribe({
        next: () => this.router.navigate(['/inventory/items']),
        error: () => {},
      });
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
