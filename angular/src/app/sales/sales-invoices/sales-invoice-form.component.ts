import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { InvoiceItemGridComponent } from './components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import type { CreateSalesInvoiceDto, SalesInvoiceItemDto } from '../../proxy/sales/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { ItemService } from '../../proxy/inventory/item.service';

@Component({
  selector: 'app-sales-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
    InvoiceItemGridComponent,
    AutoValidationDirective,
    SaveShortcutDirective],
  templateUrl: './sales-invoice-form.component.html',
  styleUrls: ['./sales-invoice-form.component.scss'],
})
export class SalesInvoiceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private taxCalc = inject(TaxCalculationService);
  private service = inject(SalesInvoiceService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);

  customers = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  warehouses = signal<any[]>([]);

  form = this.fb.group({
    invoiceNumber: [''],
    companyId: ['', Validators.required],
    customerId: ['', Validators.required],
    customerName: [''],
    buyerTin: [''],
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
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    // Auto-set companyId from context for new documents
    if (!this.isEditMode) {
      const companyId = this.companyContext.currentCompanyId();
      if (companyId) this.form.patchValue({ companyId });
    }

    // Load customer list for dropdown
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe(res => this.customers.set(res.items ?? []));

    // Load items for grid dropdown
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' })
      .subscribe(res => this.availableItems.set(res.items ?? []));

    // Load warehouses for UpdateStock option
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe(res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((invoice) => {
        this.form.patchValue({
          invoiceNumber: invoice.invoiceNumber,
          companyId: invoice.companyId,
          customerId: invoice.customerId,
          customerName: invoice.customerName,
          issueDate: invoice.issueDate,
          dueDate: invoice.dueDate,
          currencyCode: invoice.currencyCode,
        });
        // Rebuild child FormArray from loaded items
        invoice.items?.forEach((item) => this.addItemRow(item));
      });
    } else {
      // Check for duplicate-from param
      const duplicateFrom = this.route.snapshot.queryParams['duplicateFrom'];
      const returnAgainst = this.route.snapshot.queryParams['returnAgainst'];
      if (duplicateFrom) {
        this.service.get(duplicateFrom).subscribe((source) => {
          this.form.patchValue({
            companyId: source.companyId,
            customerId: source.customerId,
            customerName: source.customerName,
            issueDate: new Date().toISOString().split('T')[0],
            currencyCode: source.currencyCode,
          });
          source.items?.forEach((item) => this.addItemRow(item));
          this.recalculate();
        });
      } else if (returnAgainst) {
        this.service.get(returnAgainst).subscribe((source) => {
          this.form.patchValue({
            companyId: source.companyId,
            customerId: source.customerId,
            customerName: source.customerName,
            issueDate: new Date().toISOString().split('T')[0],
            currencyCode: source.currencyCode,
            notes: `Credit Note against ${source.invoiceNumber}`,
            isReturn: true,
            returnAgainstId: returnAgainst,
          });
          // Return items have negative quantities
          source.items?.forEach((item) => {
            if (item) {
              this.addItemRow({ ...item, quantity: -(item.quantity ?? 0) } as any);
            }
          });
          this.recalculate();
        });
      }
    }
  }

  addItemRow(item?: SalesInvoiceItemDto): void {
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

  /** Used by unsaved-changes route guard */
  hasUnsavedChanges(): boolean {
    return this.form.dirty;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    if (this.isEditMode) {
      // Invoices are immutable — use Amendment workflow instead of edit
      this.router.navigate(['/sales/invoices', this.entityId]);
      return;
    }
    this.recalculate();

    const raw = this.form.getRawValue() as any;
    // Map item fields: handles both grid-added (qty/rate/itemName) and pre-loaded (quantity/unitPrice/description)
    const dto: CreateSalesInvoiceDto = {
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

    this.service.create(dto).subscribe({
      next: () => this.router.navigate(['/sales/invoices']),
      error: () => { /* handled by global error interceptor */ },
    });
  }

  cancel(): void {
    this.router.navigate(['/sales/invoices']);
  }
}
