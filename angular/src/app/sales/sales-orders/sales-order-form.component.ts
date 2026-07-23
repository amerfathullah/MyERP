import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { InvoiceItemGridComponent } from '../sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { CustomerService } from '../../proxy/sales/customer.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { StockAvailabilityComponent } from '../../shared/components/stock-availability/stock-availability.component';

@Component({
  selector: 'app-sales-order-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, StockAvailabilityComponent, CommonModule, ReactiveFormsModule, PageModule, InvoiceItemGridComponent, LocalizationPipe],
  templateUrl: './sales-order-form.component.html',
  styleUrls: ['./sales-order-form.component.scss'],
})
export class SalesOrderFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private taxCalc = inject(TaxCalculationService);
  private soService = inject(SalesOrderService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);

  customers = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  isEditMode = false;
  entityId: string | null = null;

  form = this.fb.group({
    orderNumber: [''],
    companyId: ['', Validators.required],
    orderDate: [new Date(), Validators.required],
    deliveryDate: [null as Date | null, Validators.required],
    customerId: ['', Validators.required],
    customerName: [''],
    warehouseId: [''],
    couponCode: [''],
    loyaltyPointsToRedeem: [0],
    items: this.fb.array([]),
  });

  calcResult: TaxCalculationResult = { netTotal: 0, taxLines: [], totalTax: 0, grandTotal: 0 };

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (!this.isEditMode) {
      const cid = this.companyContext.currentCompanyId();
      if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });
    }

    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe(res => this.customers.set(res.items ?? []));

    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' })
      .subscribe(res => this.availableItems.set(res.items ?? []));

    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe(res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)));

    if (this.isEditMode) {
      this.soService.get(this.entityId!).subscribe(so => {
        // Resolve warehouse from first item (header-level representation)
        const itemWarehouse = (so.items ?? []).find((i: any) => i.warehouseId)?.warehouseId ?? '';
        this.form.patchValue({
          orderNumber: so.orderNumber,
          companyId: so.companyId,
          orderDate: so.orderDate ? new Date(so.orderDate) : new Date(),
          deliveryDate: so.deliveryDate ? new Date(so.deliveryDate) : null,
          customerId: so.customerId,
          warehouseId: itemWarehouse,
        });
        (so.items ?? []).forEach((item: any) => {
          this.items.push(this.fb.group({
            itemId: [item.itemId ?? ''],
            description: [item.description ?? ''],
            qty: [item.quantity ?? 1],
            rate: [item.unitPrice ?? 0],
            discountPercent: [0],
          }));
        });
        this.recalculate();
      });
    }
  }

  get items(): FormArray { return this.form.get('items') as FormArray; }

  recalculate(): void {
    const itemValues = this.items.controls.map(c => ({
      qty: c.get('qty')?.value ?? 0,
      rate: c.get('rate')?.value ?? 0,
      discountPercent: c.get('discountPercent')?.value ?? 0,
    }));
    this.calcResult = this.taxCalc.calculate(itemValues, []);
  }

  save(): void {
    if (this.form.invalid) return;
    this.recalculate();
    const raw = this.form.getRawValue() as any;
    // Map item fields from grid control names to DTO property names
    const warehouseId = raw.warehouseId || null;
    const dto = {
      ...raw,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        description: item.itemName || item.description || '',
        quantity: item.qty ?? item.quantity ?? 0,
        unitPrice: item.rate ?? item.unitPrice ?? 0,
        taxAmount: 0,
        uom: item.uom ?? 'Unit',
        warehouseId,
      })),
    };
    if (this.isEditMode) {
      this.soService.update(this.entityId!, dto).subscribe({
        next: () => this.router.navigate(['/sales/orders', this.entityId]),
        error: () => {},
      });
    } else {
      this.soService.create(dto).subscribe({
        next: () => this.router.navigate(['/sales/orders']),
        error: () => {},
      });
    }
  }

  cancel(): void { this.router.navigate(['/sales/orders']); }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
