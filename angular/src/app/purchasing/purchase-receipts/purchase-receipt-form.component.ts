import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemService } from '../../proxy/inventory/item.service';
import type { CreatePurchaseReceiptDto } from '../../proxy/purchasing/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-purchase-receipt-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, LocalizationPipe, PageModule],
  templateUrl: './purchase-receipt-form.component.html',
  styleUrls: ['./purchase-receipt-form.component.scss'],
})
export class PurchaseReceiptFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(PurchaseReceiptService);
  private toaster = inject(ToasterService);
  private supplierService = inject(SupplierService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);

  suppliers = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  isEditMode = false;
  isReturn = false;
  entityId: string | null = null;

  form = this.fb.group({
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    warehouseId: ['', Validators.required],
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    purchaseOrderId: [''],
    supplierDeliveryNote: [''],
    isReturn: [false],
    returnAgainstId: [''],
    notes: [''],
    items: this.fb.array([]),
  });

  get items(): FormArray { return this.form.get('items') as FormArray; }

  addItem(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      description: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
      uom: ['EA'],
    }));
  }

  removeItem(i: number): void { this.items.removeAt(i); }

  onItemSelected(index: number, event: Event): void {
    const itemId = (event.target as HTMLSelectElement).value;
    const item = this.availableItems().find((i: any) => i.id === itemId);
    if (item) {
      this.items.at(index).patchValue({ description: item.itemName ?? item.itemCode });
    }
  }

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (!this.isEditMode) {
      const cid = this.companyContext.currentCompanyId();
      if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });
    }

    this.supplierService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.suppliers.set(res.items ?? [])
    );
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup))
    );
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(
      res => this.availableItems.set(res.items ?? [])
    );

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(pr => {
        this.form.patchValue({
          companyId: pr.companyId,
          supplierId: pr.supplierId,
          warehouseId: pr.warehouseId ?? '',
          postingDate: pr.postingDate,
          purchaseOrderId: pr.purchaseOrderId ?? '',
          supplierDeliveryNote: pr.supplierDeliveryNote ?? '',
          notes: '',
        });
        (pr.items ?? []).forEach((item: any) => {
          this.items.push(this.fb.group({
            itemId: [item.itemId ?? '', Validators.required],
            description: [item.description ?? '', Validators.required],
            quantity: [item.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
            unitPrice: [item.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
            uom: [item.uom ?? 'EA'],
          }));
        });
      });
    }

    // Handle return creation from detail page "Create Return" action
    const returnAgainst = this.route.snapshot.queryParams['returnAgainst'];
    if (returnAgainst && !this.isEditMode) {
      this.isReturn = true;
      this.form.patchValue({ isReturn: true, returnAgainstId: returnAgainst });
      this.service.get(returnAgainst).subscribe(original => {
        this.form.patchValue({
          companyId: original.companyId,
          supplierId: original.supplierId,
          warehouseId: original.warehouseId ?? '',
          purchaseOrderId: original.purchaseOrderId ?? '',
        });
        (original.items ?? []).forEach((item: any) => {
          this.items.push(this.fb.group({
            itemId: [item.itemId ?? '', Validators.required],
            description: [item.description ?? '', Validators.required],
            quantity: [-(Math.abs(item.quantity ?? 0)), [Validators.required]],
            unitPrice: [item.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
            uom: [item.uom ?? 'EA'],
          }));
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue() as any;
    // Convert empty strings to null for nullable Guid fields
    const dto = {
      ...raw,
      purchaseOrderId: raw.purchaseOrderId || null,
      returnAgainstId: raw.returnAgainstId || null,
    } as unknown as CreatePurchaseReceiptDto;
    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe({
        next: () => {
          this.toaster.success('Purchase Receipt updated');
          this.router.navigate(['/purchasing/receipts', this.entityId]);
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed to update'),
      });
    } else {
      this.service.create(dto).subscribe({
        next: () => {
          this.toaster.success('Purchase Receipt created');
          this.router.navigate(['/purchasing/receipts']);
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed to create'),
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/purchasing/receipts']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
