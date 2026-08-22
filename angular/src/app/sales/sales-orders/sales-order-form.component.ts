import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators, FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { InvoiceItemGridComponent } from '../sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { StockAvailabilityComponent } from '../../shared/components/stock-availability/stock-availability.component';
import { PaymentTermsTemplateService } from '../../proxy/accounting/payment-terms-template.service';
import { PriceListService } from '../../proxy/inventory/price-list.service';

@Component({
  selector: 'app-sales-order-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, StockAvailabilityComponent, CommonModule, ReactiveFormsModule, FormsModule, PageModule, InvoiceItemGridComponent, LocalizationPipe],
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
  private warehouseService = inject(WarehouseService);
  private paymentTermsService = inject(PaymentTermsTemplateService);
  private taxCategoryService = inject(TaxCategoryService);
  private taxRuleService = inject(TaxRuleService);
  private priceListService = inject(PriceListService);

  customers = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  paymentTermsTemplates = signal<any[]>([]);
  priceLists = signal<any[]>([]);
  taxCategories = signal<any[]>([]);
  selectedTaxRules = signal<any[]>([]);
  selectedTaxCategoryId = signal<string>('');
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
    paymentTermsTemplateId: [''],
    priceListId: [''],
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

    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe(res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)));

    this.paymentTermsService.getList({ skipCount: 0, maxResultCount: 50, sorting: 'name asc' })
      .subscribe({ next: res => this.paymentTermsTemplates.set(res.items ?? []), error: () => {} });

    this.priceListService.getList({ skipCount: 0, maxResultCount: 100, sorting: 'name asc' })
      .subscribe({ next: res => this.priceLists.set((res.items ?? []).filter((p: any) => p.isSelling && p.isActive)), error: () => {} });

    this.taxCategoryService.getList({ skipCount: 0, maxResultCount: 50, sorting: 'name asc' })
      .subscribe({ next: res => {
        const categories = (res.items ?? []).filter((c: any) => c.isActive !== false);
        this.taxCategories.set(categories);
        // Auto-select default (first active) tax category for new orders
        if (!this.isEditMode && categories.length > 0) {
          this.onTaxCategoryChanged(categories[0].id);
        }
      }, error: () => {} });

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
          priceListId: so.priceListId ?? '',
        });
        (so.items ?? []).forEach((item: any) => {
          this.items.push(this.fb.group({
            itemId: [item.itemId ?? ''],
            description: [item.description ?? ''],
            qty: [item.quantity ?? 1],
            rate: [item.unitPrice ?? 0],
            discountPercent: [0],
            blanketOrderId: [item.blanketOrderId ?? null],
          }));
        });
        this.recalculate();
      });
    }
  }

  get items(): FormArray { return this.form.get('items') as FormArray; }

  onTaxCategoryChanged(categoryId: string): void {
    this.selectedTaxCategoryId.set(categoryId);
    if (!categoryId) {
      this.selectedTaxRules.set([]);
      this.recalculate();
      return;
    }
    this.taxRuleService.getList(categoryId, { skipCount: 0, maxResultCount: 50, sorting: '' })
      .subscribe({
        next: (res) => {
          this.selectedTaxRules.set(res.items ?? []);
          this.recalculate();
        },
        error: () => { this.selectedTaxRules.set([]); this.recalculate(); },
      });
  }

  recalculate(): void {
    const itemValues = this.items.controls.map(c => ({
      qty: c.get('qty')?.value ?? 0,
      rate: c.get('rate')?.value ?? 0,
      discountPercent: c.get('discountPercent')?.value ?? 0,
    }));
    this.calcResult = this.taxCalc.calculate(itemValues, this.selectedTaxRules());
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
        blanketOrderId: item.blanketOrderId || null,
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
