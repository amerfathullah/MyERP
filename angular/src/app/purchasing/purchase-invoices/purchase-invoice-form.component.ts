import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { InvoiceItemGridComponent } from '../../sales/sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { PaymentTermsTemplateService } from '../../proxy/accounting/payment-terms-template.service';
import type { CreatePurchaseInvoiceDto } from '../../proxy/purchasing/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { PartyDetailsService } from '../../proxy/core/party-details.service';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import type { TaxRuleDto as TaxRuleModel } from '../../proxy/tax/models';
import { CurrencyExchangeService } from '../../proxy/accounting/currency-exchange.service';

@Component({
  selector: 'app-purchase-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
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
  private paymentTermsService = inject(PaymentTermsTemplateService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  private partyDetailsService = inject(PartyDetailsService);
  private taxCategoryService = inject(TaxCategoryService);
  private taxRuleService = inject(TaxRuleService);
  private http = inject(HttpClient);
  private currencyExchangeService = inject(CurrencyExchangeService);

  /** Multi-currency: true when selected currency differs from company base (MYR) */
  isMultiCurrency = signal(false);

  suppliers = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  taxCategories = signal<any[]>([]);
  taxTemplates = signal<any[]>([]);
  selectedTaxRules = signal<TaxRuleModel[]>([]);
  supplierAddress = signal<string>('');
  warehouses = signal<any[]>([]);
  paymentTermsTemplates = signal<any[]>([]);
  isLoadingPOItems = signal(false);
  isLoadingPRItems = signal(false);

  // Document-level discount
  discountOn: 'GrandTotal' | 'NetTotal' = 'GrandTotal';
  discountPercent = 0;
  discountAmount = signal(0);

  form = this.fb.group({
    invoiceNumber: [''],
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    supplierName: [''],
    supplierTin: [''],
    supplierInvoiceNumber: [''],
    issueDate: [new Date().toISOString().split('T')[0], Validators.required],
    dueDate: [''],
    paymentTermsTemplateId: [''],
    currencyCode: ['MYR'],
    exchangeRate: [1],
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
    this.paymentTermsService.getList({ skipCount: 0, maxResultCount: 50, sorting: 'name asc' })
      .subscribe({ next: res => this.paymentTermsTemplates.set(res.items ?? []), error: () => {} });

    // Load tax categories for tax template selector
    this.taxCategoryService.getList({ skipCount: 0, maxResultCount: 50, sorting: 'name asc' })
      .subscribe({ next: res => this.taxCategories.set((res.items ?? []).filter((c: any) => c.isActive)), error: () => {} });

    // Load purchase tax templates
    this.loadTaxTemplates();

    // Auto-resolve supplier details when supplier selection changes
    this.form.get('supplierId')?.valueChanges.subscribe(supplierId => {
      if (supplierId) {
        this.onSupplierChanged(supplierId);
        this.resolveSupplierDetails(supplierId);
      }
    });
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

  /**
   * Auto-resolves supplier TIN and name when supplier is selected.
   * Per LHDN: self-billed e-invoices require supplier TIN for submission.
   * Per ERPNext party.py: supplier details auto-populated on selection.
   */
  onSupplierChanged(supplierId: string): void {
    const supplier = this.suppliers().find((s: any) => s.id === supplierId);
    if (supplier) {
      this.form.patchValue({
        supplierName: supplier.name || supplier.supplierName || '',
        supplierTin: supplier.tin || supplier.taxIdentificationNumber || '',
      });
    }
  }

  /** Fetches exchange rate from backend when currency changes from MYR. Per ERPNext: foreign purchases auto-resolve rate. */
  onCurrencyChanged(): void {
    const currency = this.form.get('currencyCode')?.value;
    const baseCurrency = 'MYR';
    if (!currency || currency === baseCurrency) {
      this.isMultiCurrency.set(false);
      this.form.patchValue({ exchangeRate: 1 });
      return;
    }
    this.isMultiCurrency.set(true);
    const issueDate = this.form.get('issueDate')?.value || new Date().toISOString().split('T')[0];
    this.currencyExchangeService.getRate(currency, baseCurrency, issueDate).subscribe({
      next: (result) => {
        if (result?.rate) {
          this.form.patchValue({ exchangeRate: result.rate });
        }
      },
      error: () => { /* Non-blocking: user can manually enter exchange rate */ }
    });
  }

  recalculate(): void {
    const itemValues = this.items.controls.map(c => ({
      qty: c.get('quantity')?.value ?? 0,
      rate: c.get('unitPrice')?.value ?? 0,
      discountPercent: 0,
    }));

    const taxRules = this.selectedTaxRules().map(r => ({
      taxName: r.description || `Tax ${r.rate}%`,
      rate: r.rate ?? 0,
      chargeType: 'OnNetTotal' as const,
    }));

    let result = this.taxCalc.calculate(itemValues, taxRules);

    // Apply document-level discount per ERPNext ApplyDiscountOn logic
    const discountAmt = this.discountAmount();
    if (discountAmt > 0) {
      if (this.discountOn === 'NetTotal') {
        const reducedNet = Math.max(0, result.netTotal - discountAmt);
        const ratio = result.netTotal > 0 ? reducedNet / result.netTotal : 0;
        const newTaxLines = result.taxLines.map(t => ({
          ...t,
          taxAmount: Math.round(t.taxAmount * ratio * 100) / 100,
        }));
        const totalTax = newTaxLines.reduce((s, t) => s + t.taxAmount, 0);
        result = { ...result, netTotal: reducedNet, taxLines: newTaxLines, totalTax, grandTotal: reducedNet + totalTax };
      } else {
        result = { ...result, grandTotal: Math.max(0, result.grandTotal - discountAmt) };
      }
    }

    this.calcResult = result;
  }

  /** Discount percentage changed → compute amount */
  onDiscountPercentChanged(): void {
    const base = this.discountOn === 'NetTotal' ? this.calcResult.netTotal : this.calcResult.grandTotal;
    const amount = Math.round(base * this.discountPercent / 100 * 100) / 100;
    this.discountAmount.set(amount);
    this.recalculate();
  }

  /** Discount amount directly entered */
  onDiscountAmountChanged(value: string): void {
    const amount = Math.max(0, parseFloat(value) || 0);
    this.discountAmount.set(amount);
    const base = this.discountOn === 'NetTotal' ? this.calcResult.netTotal : this.calcResult.grandTotal;
    this.discountPercent = base > 0 ? Math.round(amount / base * 100 * 100) / 100 : 0;
    this.recalculate();
  }

  /** Discount-On mode changed */
  onDiscountChanged(): void {
    if (this.discountPercent > 0) {
      this.onDiscountPercentChanged();
    } else {
      this.recalculate();
    }
  }

  onTaxCategoryChanged(categoryId: string): void {
    if (!categoryId) {
      this.selectedTaxRules.set([]);
      this.recalculate();
      return;
    }
    this.taxRuleService.getList(categoryId, { skipCount: 0, maxResultCount: 50, sorting: 'priority asc' })
      .subscribe({
        next: res => {
          const rules = (res.items ?? []).filter(
            (r: any) => r.isActive
          );
          this.selectedTaxRules.set(rules);
          this.recalculate();
        },
        error: () => {},
      });
  }

  /** Load available purchase tax templates for this company. */
  loadTaxTemplates(): void {
    const companyId = this.companyContext?.currentCompanyId?.() ?? '';
    const params: any = { skipCount: '0', maxResultCount: '50', templateType: '1' }; // 1 = Buying
    if (companyId) params.companyId = companyId;
    this.http.get<any>('/api/app/tax-charges-template', { params }).subscribe({
      next: res => {
        const templates = (res.items ?? []).filter((t: any) => t.isEnabled);
        this.taxTemplates.set(templates);
        if (!this.isEditMode) {
          const defaultTmpl = templates.find((t: any) => t.isDefault);
          if (defaultTmpl) this.applyTaxTemplate(defaultTmpl);
        }
      },
      error: () => {},
    });
  }

  /** Apply a tax charges template to populate tax rules. */
  onTaxTemplateChanged(templateId: string): void {
    if (!templateId) { this.selectedTaxRules.set([]); this.recalculate(); return; }
    const template = this.taxTemplates().find((t: any) => t.id === templateId);
    if (template) this.applyTaxTemplate(template);
  }

  private applyTaxTemplate(template: any): void {
    const rules = (template.rows ?? []).map((row: any) => ({
      id: row.id, rate: row.rate,
      description: row.description || `Tax @ ${row.rate}%`,
      chargeType: row.chargeType, taxCategory: row.taxCategory,
      accountId: row.accountId, accountName: row.accountName,
      isActive: true,
    }));
    this.selectedTaxRules.set(rules);
    this.recalculate();
  }

  private resolveSupplierDetails(supplierId: string): void {
    this.partyDetailsService.getSupplierDetails({ partyType: 'Supplier', partyId: supplierId }).subscribe({
      next: (details: any) => {
        if (details.tin) {
          this.form.patchValue({ supplierTin: details.tin });
        }
        if (details.billingAddress) {
          const addr = details.billingAddress;
          const parts = [addr.addressLine1, addr.city, addr.state, addr.postalCode].filter(Boolean);
          this.supplierAddress.set(parts.join(', '));
        }
        if (details.defaultPaymentTermsTemplateId && !this.form.get('paymentTermsTemplateId')?.value) {
          this.form.patchValue({ paymentTermsTemplateId: details.defaultPaymentTermsTemplateId });
        }
      },
      error: () => {},
    });
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
      paymentTermsTemplateId: raw.paymentTermsTemplateId || undefined,
      dueDate: raw.dueDate || undefined,
      warehouseId: raw.warehouseId || undefined,
      returnAgainstId: raw.returnAgainstId || undefined,
      discountAmount: this.discountAmount() > 0 ? this.discountAmount() : undefined,
      applyDiscountOn: this.discountAmount() > 0 ? this.discountOn : undefined,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        description: item.description || item.itemName || '',
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        taxAmount: item.taxAmount ?? 0,
        uom: item.uom ?? 'Unit',
        purchaseOrderItemId: item.purchaseOrderItemId || undefined,
        purchaseReceiptItemId: item.purchaseReceiptItemId || undefined,
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

  getItemsFromPO(): void {
    const supplierId = this.form.get('supplierId')?.value;
    if (!supplierId) return;
    this.isLoadingPOItems.set(true);
    const companyId = this.form.get('companyId')?.value || undefined;
    this.service.getUnbilledPurchaseOrderItems(supplierId, companyId).subscribe({
      next: (items: any[]) => {
        this.isLoadingPOItems.set(false);
        if (!items || items.length === 0) {
          this.toaster.info('::NoUnbilledOrderItems');
          return;
        }
        // Clear existing items and populate from PO
        while (this.items.length > 0) this.items.removeAt(0);
        items.forEach((item: any) => {
          this.addItemRow({
            itemId: item.itemId,
            description: item.itemName || '',
            quantity: item.unbilledQty ?? item.quantity ?? 1,
            unitPrice: item.rate ?? item.unitPrice ?? 0,
            uom: item.uom ?? 'Unit',
            purchaseOrderItemId: item.purchaseOrderItemId ?? item.id ?? null,
          });
        });
        this.recalculate();
        this.toaster.success(this.l.instant('::ItemsLoadedFromPO', items.length.toString()));
      },
      error: () => {
        this.isLoadingPOItems.set(false);
      },
    });
  }

  getItemsFromPR(): void {
    const supplierId = this.form.get('supplierId')?.value;
    if (!supplierId) return;
    this.isLoadingPRItems.set(true);
    const companyId = this.form.get('companyId')?.value || undefined;
    this.service.getUnbilledPurchaseReceiptItems(supplierId, companyId).subscribe({
      next: (items: any[]) => {
        this.isLoadingPRItems.set(false);
        if (!items || items.length === 0) {
          this.toaster.info('::NoUnbilledReceiptItems');
          return;
        }
        // Clear existing items and populate from PR
        while (this.items.length > 0) this.items.removeAt(0);
        items.forEach((item: any) => {
          this.addItemRow({
            itemId: item.itemId,
            description: item.itemName || '',
            quantity: item.quantity ?? 1,
            unitPrice: item.rate ?? item.unitPrice ?? 0,
            uom: item.uom ?? 'Unit',
            purchaseReceiptItemId: item.purchaseReceiptItemId ?? item.id ?? null,
            purchaseOrderItemId: item.purchaseOrderItemId ?? null,
          });
        });
        this.recalculate();
        this.toaster.success(this.l.instant('::ItemsLoadedFromPR', items.length.toString()));
      },
      error: () => {
        this.isLoadingPRItems.set(false);
      },
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
