import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, FormGroup, Validators, FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { SupplierQuotationService } from '../../proxy/purchasing/supplier-quotation.service';
import { CompanyService } from '../../proxy/core/company.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemDetailsService } from '../../proxy/inventory/item-details.service';
import { PartyDetailsService } from '../../proxy/core/party-details.service';
import type { SupplierDto } from '../../proxy/purchasing/models';
import type { CompanyDto } from '../../proxy/core/models';
import { CurrencyExchangeService } from '../../proxy/accounting/currency-exchange.service';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-purchase-order-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule, PageModule, LocalizationPipe, AutoValidationDirective, SaveShortcutDirective],
  templateUrl: './purchase-order-form.component.html',
  styleUrls: ['./purchase-order-form.component.scss'],
})
export class PurchaseOrderFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(PurchaseOrderService);
  private supplierService = inject(SupplierService);
  private supplierQuotationService = inject(SupplierQuotationService);
  private companyService = inject(CompanyService);
  private itemService = inject(ItemService);
  private itemDetailsService = inject(ItemDetailsService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  private companyContext = inject(CompanyContextService);
  private warehouseService = inject(WarehouseService);
  private partyDetailsService = inject(PartyDetailsService);
  private currencyExchangeService = inject(CurrencyExchangeService);
  private taxCategoryService = inject(TaxCategoryService);
  private taxRuleService = inject(TaxRuleService);
  private taxCalc = inject(TaxCalculationService);

  /** Multi-currency: true when selected currency differs from company base (MYR) */
  isMultiCurrency = signal(false);

  companies = signal<CompanyDto[]>([]);
  suppliers = signal<SupplierDto[]>([]);
  availableItems = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  taxCategories = signal<any[]>([]);
  selectedTaxRules = signal<any[]>([]);
  selectedTaxCategoryId = signal<string>('');
  calcResult: TaxCalculationResult = { netTotal: 0, taxLines: [], totalTax: 0, grandTotal: 0 };
  isLoadingMrItems = signal(false);
  isLoadingSqItems = signal(false);
  supplierQuotations = signal<any[]>([]);
  supplierAddress = signal<string>('');
  supplierTin = signal<string>('');
  supplierScorecardWarning = signal<string>('');
  supplierBlocked = signal(false);
  isEditMode = false;
  entityId: string | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal', 'actions'];

  form = this.fb.group({
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    orderDate: [new Date().toISOString().split('T')[0], Validators.required],
    expectedDeliveryDate: [''],
    currencyCode: ['MYR'],
    exchangeRate: [1],
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

    this.taxCategoryService.getList({ skipCount: 0, maxResultCount: 50, sorting: 'name asc' })
      .subscribe({ next: res => {
        const categories = (res.items ?? []).filter((c: any) => c.isActive !== false);
        this.taxCategories.set(categories);
        if (!this.isEditMode && categories.length > 0) {
          this.onTaxCategoryChanged(categories[0].id);
        }
      }, error: () => {} });

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
    const row = this.items.at(index) as FormGroup;
    if (item) {
      row.patchValue({ description: item.itemName || item.itemCode });
    }
    // Auto-resolve last purchase rate + UOM from backend (with supplier-specific pricing)
    if (itemId) {
      this.itemDetailsService.getItemDetails({
        itemId,
        transactionType: 'Buying',
        companyId: this.form.get('companyId')?.value || undefined,
        supplierId: this.form.get('supplierId')?.value || undefined,
      }).subscribe({
        next: (details) => {
          if (details) {
            const patch: any = {};
            if (details.rate > 0 && !row.get('unitPrice')?.value) patch.unitPrice = details.rate;
            if (details.description && !row.get('description')?.value) patch.description = details.description;
            if (Object.keys(patch).length > 0) row.patchValue(patch);
          }
        },
        error: () => {} // Graceful fallback
      });
    }
  }

  getLineTotal(row: FormGroup): number {
    const qty = row.get('quantity')?.value ?? 0;
    const price = row.get('unitPrice')?.value ?? 0;
    const tax = row.get('taxAmount')?.value ?? 0;
    return qty * price + tax;
  }

  onSupplierChanged(): void {
    const supplierId = this.form.get('supplierId')?.value;
    this.supplierAddress.set('');
    this.supplierTin.set('');
    this.supplierQuotations.set([]);
    this.supplierScorecardWarning.set('');
    this.supplierBlocked.set(false);
    if (!supplierId) return;

    // Check supplier scorecard standing (warn/block per ERPNext supplier_scorecard enforcement)
    const supplier = this.suppliers().find(s => s.id === supplierId);
    if (supplier) {
      if ((supplier as any).preventPos) {
        this.supplierBlocked.set(true);
        this.supplierScorecardWarning.set(this.l.instant('::SupplierBlockedByScorecard'));
      } else if ((supplier as any).holdType === 'All') {
        this.supplierBlocked.set(true);
        this.supplierScorecardWarning.set(this.l.instant('::SupplierOnHold'));
      }
    }

    this.loadSupplierQuotations();

    this.partyDetailsService.getSupplierDetails({ partyId: supplierId }).subscribe({
      next: (details: any) => {
        if (details?.tin) this.supplierTin.set(details.tin);
        const parts = [details?.addressLine1, details?.city, details?.state, details?.postalCode].filter(Boolean);
        if (parts.length > 0) this.supplierAddress.set(parts.join(', '));
      },
      error: () => {}
    });
  }

  /** Fetches exchange rate when currency changes from MYR. Per ERPNext: foreign POs auto-resolve rate. */
  onCurrencyChanged(): void {
    const currency = this.form.get('currencyCode')?.value;
    const baseCurrency = 'MYR';
    if (!currency || currency === baseCurrency) {
      this.isMultiCurrency.set(false);
      this.form.patchValue({ exchangeRate: 1 });
      return;
    }
    this.isMultiCurrency.set(true);
    const orderDate = this.form.get('orderDate')?.value || new Date().toISOString().split('T')[0];
    this.currencyExchangeService.getRate(currency, baseCurrency, orderDate).subscribe({
      next: (result) => {
        if (result?.rate) {
          this.form.patchValue({ exchangeRate: result.rate });
        }
      },
      error: () => { /* Non-blocking: user can manually enter exchange rate */ }
    });
  }

  get netTotal(): number {
    return this.items.controls.reduce((sum, row) => {
      const g = row as FormGroup;
      return sum + (g.get('quantity')?.value ?? 0) * (g.get('unitPrice')?.value ?? 0);
    }, 0);
  }

  get taxTotal(): number {
    // Use tax template calculation when available; fall back to per-item taxAmount
    if (this.selectedTaxRules().length > 0) return this.calcResult.totalTax;
    return this.items.controls.reduce((sum, row) => {
      return sum + ((row as FormGroup).get('taxAmount')?.value ?? 0);
    }, 0);
  }

  get grandTotal(): number { return this.netTotal + this.taxTotal; }

  onTaxCategoryChanged(categoryId: string): void {
    this.selectedTaxCategoryId.set(categoryId);
    if (!categoryId) {
      this.selectedTaxRules.set([]);
      this.recalculateTax();
      return;
    }
    this.taxRuleService.getList(categoryId, { skipCount: 0, maxResultCount: 50, sorting: '' })
      .subscribe({
        next: (res) => { this.selectedTaxRules.set(res.items ?? []); this.recalculateTax(); },
        error: () => { this.selectedTaxRules.set([]); this.recalculateTax(); },
      });
  }

  recalculateTax(): void {
    const itemValues = this.items.controls.map(c => ({
      qty: (c as FormGroup).get('quantity')?.value ?? 0,
      rate: (c as FormGroup).get('unitPrice')?.value ?? 0,
      discountPercent: 0,
    }));
    this.calcResult = this.taxCalc.calculate(itemValues, this.selectedTaxRules());
  }

  /** Load pending items from Material Requests (Purchase type) for this company. */
  loadItemsFromMaterialRequest(): void {
    const companyId = this.form.get('companyId')?.value || undefined;
    if (!companyId) {
      this.toaster.warn('::PleaseSelectCompanyFirst');
      return;
    }
    this.isLoadingMrItems.set(true);
    this.service.getPendingMaterialRequestItems(companyId).subscribe({
      next: (mrItems: any[]) => {
        this.isLoadingMrItems.set(false);
        if (!mrItems || mrItems.length === 0) {
          this.toaster.info('::NoPendingMaterialRequestItems');
          return;
        }
        // Clear existing items and load from MR
        while (this.items.length > 0) this.items.removeAt(0);
        mrItems.forEach(mrItem => {
          this.items.push(this.fb.group({
            itemId: [mrItem.itemId, Validators.required],
            description: [mrItem.itemName, Validators.required],
            quantity: [mrItem.pendingQty, [Validators.required, Validators.min(0.01)]],
            unitPrice: [0, [Validators.required, Validators.min(0)]],
            taxAmount: [0, Validators.min(0)],
            uom: [mrItem.uom || 'Unit'],
          }));
        });
        this.toaster.success(`${mrItems.length} items loaded from Material Requests`);
      },
      error: () => {
        this.isLoadingMrItems.set(false);
        this.toaster.error('::FailedToLoad');
      },
    });
  }

  /** Load items from a Supplier Quotation (SQ→PO pipeline). Per ERPNext: most common PO creation for negotiated purchases. */
  loadItemsFromSupplierQuotation(sqId: string): void {
    if (!sqId) return;
    this.isLoadingSqItems.set(true);
    this.supplierQuotationService.get(sqId).subscribe({
      next: (sq: any) => {
        this.isLoadingSqItems.set(false);
        if (!sq?.items || sq.items.length === 0) {
          this.toaster.info(this.l.instant('::NoItemsInQuotation'));
          return;
        }
        // Auto-fill supplier from SQ if not already set
        if (sq.supplierId && !this.form.get('supplierId')?.value) {
          this.form.patchValue({ supplierId: sq.supplierId });
          this.onSupplierChanged();
        }
        // Clear existing items and load from SQ
        while (this.items.length > 0) this.items.removeAt(0);
        sq.items.forEach((sqItem: any) => {
          this.items.push(this.fb.group({
            itemId: [sqItem.itemId, Validators.required],
            description: [sqItem.description || sqItem.itemName, Validators.required],
            quantity: [sqItem.quantity, [Validators.required, Validators.min(0.01)]],
            unitPrice: [sqItem.rate || sqItem.unitPrice || 0, [Validators.required, Validators.min(0)]],
            taxAmount: [0, Validators.min(0)],
            uom: [sqItem.uom || 'Unit'],
          }));
        });
        this.toaster.success(this.l.instant('::ItemsLoadedFromSQ', sq.items.length.toString()));
      },
      error: () => {
        this.isLoadingSqItems.set(false);
        this.toaster.error(this.l.instant('::FailedToLoad'));
      },
    });
  }

  /** Load submitted SQs for the selected supplier (for dropdown selection). */
  loadSupplierQuotations(): void {
    const supplierId = this.form.get('supplierId')?.value;
    const companyId = this.form.get('companyId')?.value;
    if (!supplierId) return;
    this.supplierQuotationService.getList({ skipCount: 0, maxResultCount: 50, sorting: 'creationTime desc', status: 'Submitted', supplierId, companyId } as any).subscribe({
      next: (res) => this.supplierQuotations.set(res.items ?? []),
      error: () => {},
    });
  }

  save(): void {
    if (this.form.invalid || this.items.length === 0) {
      this.form.markAllAsTouched();
      if (this.items.length === 0) this.toaster.warn('::PleaseFillAllRequiredFields');
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
