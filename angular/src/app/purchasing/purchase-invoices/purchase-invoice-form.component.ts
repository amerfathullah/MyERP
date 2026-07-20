import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { InvoiceItemGridComponent } from '../../sales/sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import type { CreatePurchaseInvoiceDto } from '../../proxy/purchasing/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';

@Component({
  selector: 'app-purchase-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
    InvoiceItemGridComponent,
    AutoValidationDirective,
    SaveShortcutDirective],
  templateUrl: './purchase-invoice-form.component.html',
  styleUrls: ['./purchase-invoice-form.component.scss'],
})
export class PurchaseInvoiceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private taxCalc = inject(TaxCalculationService);
  private service = inject(PurchaseInvoiceService);
  private supplierService = inject(SupplierService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);

  suppliers = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  warehouses = signal<any[]>([]);

  form = this.fb.group({
    invoiceNumber: [''],
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    supplierName: [''],
    supplierTin: [''],
    issueDate: [new Date().toISOString().split('T')[0], Validators.required],
    dueDate: [''],
    currencyCode: ['MYR'],
    notes: [''],
    isReturn: [false],
    returnAgainstId: [null as string | null],
    updateStock: [false],
    warehouseId: [''],
    items: this.fb.array([]),
  });

  isEditMode = false;
  entityId: string | null = null;

  calcResult: TaxCalculationResult = {
    netTotal: 0,
    taxLines: [],
    totalTax: 0,
    grandTotal: 0,
  };

  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  ngOnInit(): void {
    this.supplierService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.suppliers.set(res.items ?? [])
    );
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(
      res => this.availableItems.set(res.items ?? [])
    );
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup))
    );
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    // Auto-set companyId from company context for new documents
    if (!this.isEditMode && !this.form?.get?.('companyId')?.value) {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((invoice) => {
        this.form.patchValue({
          invoiceNumber: invoice.invoiceNumber,
          companyId: invoice.companyId,
          supplierId: invoice.supplierId,
          supplierTin: invoice.supplierTin,
          issueDate: invoice.issueDate,
          dueDate: invoice.dueDate,
        });
        invoice.items?.forEach((item: any) => this.addItemRow(item));
      });
    } else {
      const returnAgainst = this.route.snapshot.queryParams['returnAgainst'];
      if (returnAgainst) {
        this.service.get(returnAgainst).subscribe((source: any) => {
          this.form.patchValue({
            companyId: source.companyId,
            supplierId: source.supplierId,
            supplierTin: source.supplierTin,
            issueDate: new Date().toISOString().split('T')[0],
            currencyCode: source.currencyCode,
            notes: `Debit Note against ${source.invoiceNumber}`,
            isReturn: true,
            returnAgainstId: returnAgainst,
          });
          (source.items ?? []).forEach((item: any) => {
            if (item) this.addItemRow({ ...item, quantity: -(item.quantity ?? 0) });
          });
          this.recalculate();
        });
      }
    }
  }

  addItemRow(item?: any): void {
    this.items.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [item?.taxAmount ?? 0],
      uom: [item?.uom ?? 'EA'],
    }));
  }

  recalculate(): void {
    const itemValues = this.items.controls.map(c => ({
      qty: c.get('quantity')?.value ?? 0,
      rate: c.get('unitPrice')?.value ?? 0,
      discountPercent: 0,
    }));
    this.calcResult = this.taxCalc.calculate(itemValues, []);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.recalculate();
    const raw = this.form.getRawValue() as any;
    // Map item fields: handles both grid-added (qty/rate/itemName) and pre-loaded (quantity/unitPrice/description)
    const dto: CreatePurchaseInvoiceDto = {
      ...raw,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        description: item.description || item.itemName || '',
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        taxAmount: item.taxAmount ?? 0,
        uom: item.uom ?? 'Unit',
      })),
    };
    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe({
        next: () => this.router.navigate(['/purchasing/invoices', this.entityId]),
        error: () => {},
      });
      return;
    }
    this.service.create(dto).subscribe({
      next: () => this.router.navigate(['/purchasing/invoices']),
      error: () => { /* handled by global error interceptor */ },
    });
  }

  cancel(): void {
    this.router.navigate(['/purchasing/invoices']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
