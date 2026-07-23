import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../proxy/core/company.service';
import { ItemService } from '../../proxy/inventory/item.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { StockAvailabilityComponent } from '../../shared/components/stock-availability/stock-availability.component';

@Component({
  selector: 'app-delivery-note-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, StockAvailabilityComponent, CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule],
  templateUrl: './delivery-note-form.component.html',
  styleUrls: ['./delivery-note-form.component.scss'],
})
export class DeliveryNoteFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(DeliveryNoteService);
  private customerService = inject(CustomerService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);

  customers = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  companies = signal<any[]>([]);
  availableItems = signal<any[]>([]);

  form = this.fb.group({
    companyId: ['', Validators.required],
    customerId: ['', Validators.required],
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    salesOrderId: [''],
    warehouseId: ['', Validators.required],
    isReturn: [false],
    returnAgainstId: [''],
    items: this.fb.array([]),
  });

  isEditMode = false;
  isReturn = false;
  entityId: string | null = null;

  get items(): FormArray { return this.form.get('items') as FormArray; }

  ngOnInit(): void {
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.customers.set(res.items ?? [])
    );
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup))
    );
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(
      res => this.availableItems.set(res.items ?? [])
    );
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(
      res => this.companies.set(res.items ?? [])
    );
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    // Auto-set companyId from company context for new documents
    if (!this.isEditMode && !this.form?.get?.('companyId')?.value) {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((dn) => {
        this.form.patchValue({
          companyId: dn.companyId,
          customerId: dn.customerId,
          postingDate: dn.postingDate,
          salesOrderId: dn.salesOrderId,
          warehouseId: dn.warehouseId,
        });
        dn.items?.forEach((item: any) => this.addItemRow(item));
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
          customerId: original.customerId,
          warehouseId: original.warehouseId,
          salesOrderId: original.salesOrderId ?? '',
        });
        // Add items with negative quantities for return
        original.items?.forEach((item: any) => {
          this.addItemRow({
            ...item,
            quantity: -(Math.abs(item.quantity ?? 0)),
          });
        });
      });
    }
  }

  addItemRow(item?: any): void {
    this.items.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [item?.taxAmount ?? 0],
      uom: [item?.uom ?? 'Unit'],
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue() as any;
    // Convert empty strings to null for nullable Guid fields
    const value = {
      ...raw,
      salesOrderId: raw.salesOrderId || null,
      returnAgainstId: raw.returnAgainstId || null,
    };
    if (this.isEditMode) {
      this.service.update(this.entityId!, value).subscribe({
        next: () => this.router.navigate(['/sales/delivery-notes', this.entityId]),
        error: () => {},
      });
    } else {
      this.service.create(value).subscribe({
        next: () => this.router.navigate(['/sales/delivery-notes']),
        error: () => {},
      });
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
