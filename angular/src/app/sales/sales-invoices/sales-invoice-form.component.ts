import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { InvoiceItemGridComponent } from './components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { PaymentTermsTemplateService } from '../../proxy/accounting/payment-terms-template.service';
import { CurrencyExchangeService } from '../../proxy/accounting/currency-exchange.service';
import { PartyDetailsService } from '../../proxy/core/party-details.service';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import type { CreateSalesInvoiceDto, SalesInvoiceItemDto } from '../../proxy/sales/models';
import type { TaxRuleDto as TaxRuleModel } from '../../proxy/tax/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { ItemService } from '../../proxy/inventory/item.service';

@Component({
  selector: 'app-sales-invoice-form',
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
  private paymentTermsService = inject(PaymentTermsTemplateService);
  private currencyExchangeService = inject(CurrencyExchangeService);
  private partyDetailsService = inject(PartyDetailsService);
  private taxCategoryService = inject(TaxCategoryService);
  private taxRuleService = inject(TaxRuleService);
  private http = inject(HttpClient);

  isMultiCurrency = signal(false);
  customers = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  taxCategories = signal<any[]>([]);
  taxTemplates = signal<any[]>([]);
  selectedTaxRules = signal<TaxRuleModel[]>([]);
  billingAddress = signal<string>('');
  partyTin = signal<string>('');
  warehouses = signal<any[]>([]);
  paymentTermsTemplates = signal<any[]>([]);
  isLoadingSoItems = signal(false);
  isLoadingDnItems = signal(false);
  paymentSchedulePreview = signal<{ dueDate: string; portion: number; amount: number }[]>([]);

  // Document-level discount
  discountOn: 'GrandTotal' | 'NetTotal' = 'GrandTotal';
  discountPercent = 0;
  discountAmount = signal(0);

  form = this.fb.group({
    invoiceNumber: [''],
    companyId: ['', Validators.required],
    customerId: ['', Validators.required],
    customerName: [''],
    buyerTin: [''],
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
    couponCode: [''],
    loyaltyPointsToRedeem: [0],
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
      .subscribe({ next: res => this.customers.set(res.items ?? []), error: () => {} });

    // Load items for grid dropdown
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' })
      .subscribe({ next: res => this.availableItems.set(res.items ?? []), error: () => {} });

    // Load warehouses for UpdateStock option
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe({ next: res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)), error: () => {} });

    // Load payment terms templates
    this.paymentTermsService.getList({ skipCount: 0, maxResultCount: 50, sorting: 'name asc' })
      .subscribe({ next: res => this.paymentTermsTemplates.set(res.items ?? []), error: () => {} });

    // Load tax categories for tax template selector
    this.taxCategoryService.getList({ skipCount: 0, maxResultCount: 50, sorting: 'name asc' })
      .subscribe({ next: res => this.taxCategories.set((res.items ?? []).filter((c: any) => c.isActive)), error: () => {} });

    // Load tax charges templates (selling type) for template selector
    this.loadTaxTemplates();

    // Listen for customer changes to auto-fill party details
    this.form.get('customerId')?.valueChanges.subscribe(customerId => {
      if (customerId) this.resolveCustomerDetails(customerId);
    });

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

    // Build tax rules from selected tax category rules
    const taxRules = this.selectedTaxRules().map(r => ({
      taxName: r.description || `Tax ${r.rate}%`,
      rate: r.rate ?? 0,
      chargeType: 'OnNetTotal' as const,
    }));

    // Calculate base result (before document-level discount)
    let result = this.taxCalc.calculate(itemValues, taxRules);

    // Apply document-level discount per ERPNext ApplyDiscountOn logic
    const discountAmt = this.discountAmount();
    if (discountAmt > 0) {
      if (this.discountOn === 'NetTotal') {
        // Discount on Net Total: reduce net BEFORE tax, then recalculate tax on reduced net
        const reducedNet = Math.max(0, result.netTotal - discountAmt);
        const ratio = result.netTotal > 0 ? reducedNet / result.netTotal : 0;
        const newTaxLines = result.taxLines.map(t => ({
          ...t,
          taxAmount: Math.round(t.taxAmount * ratio * 100) / 100,
        }));
        const totalTax = newTaxLines.reduce((s, t) => s + t.taxAmount, 0);
        result = { ...result, netTotal: reducedNet, taxLines: newTaxLines, totalTax, grandTotal: reducedNet + totalTax };
      } else {
        // Discount on Grand Total: reduce AFTER tax (post-tax deduction)
        result = { ...result, grandTotal: Math.max(0, result.grandTotal - discountAmt) };
      }
    }

    this.calcResult = result;
    // Update payment schedule preview when grand total changes
    this.onPaymentTermsChanged();
  }

  /** Discount percentage changed → compute amount */
  onDiscountPercentChanged(): void {
    const base = this.discountOn === 'NetTotal' ? this.calcResult.netTotal : this.calcResult.grandTotal;
    const amount = Math.round(base * this.discountPercent / 100 * 100) / 100;
    this.discountAmount.set(amount);
    this.recalculate();
  }

  /** Discount amount directly entered → back-calculate percentage */
  onDiscountAmountChanged(value: string): void {
    const amount = Math.max(0, parseFloat(value) || 0);
    this.discountAmount.set(amount);
    const base = this.discountOn === 'NetTotal' ? this.calcResult.netTotal : this.calcResult.grandTotal;
    this.discountPercent = base > 0 ? Math.round(amount / base * 100 * 100) / 100 : 0;
    this.recalculate();
  }

  /** Discount-On mode changed → recalculate amount from percentage */
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
    // Load tax rules for the selected category
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

  /** Load available tax charges templates (Selling type) for this company. */
  private loadTaxTemplates(): void {
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { skipCount: '0', maxResultCount: '50', templateType: '0' }; // 0 = Selling
    if (companyId) params.companyId = companyId;
    this.http.get<any>('/api/app/tax-charges-template', { params }).subscribe({
      next: res => {
        const templates = (res.items ?? []).filter((t: any) => t.isEnabled);
        this.taxTemplates.set(templates);
        // Auto-select default template (if not editing existing invoice)
        if (!this.isEditMode) {
          const defaultTmpl = templates.find((t: any) => t.isDefault);
          if (defaultTmpl) {
            this.applyTaxTemplate(defaultTmpl);
          }
        }
      },
      error: () => {},
    });
  }

  /** Apply a tax charges template — populates tax rules from template rows. */
  onTaxTemplateChanged(templateId: string): void {
    if (!templateId) {
      this.selectedTaxRules.set([]);
      this.recalculate();
      return;
    }
    const template = this.taxTemplates().find((t: any) => t.id === templateId);
    if (template) {
      this.applyTaxTemplate(template);
    }
  }

  /** Maps template rows to the tax rules signal for calculation. */
  private applyTaxTemplate(template: any): void {
    const rules = (template.rows ?? []).map((row: any) => ({
      id: row.id,
      rate: row.rate,
      description: row.description || `Tax @ ${row.rate}%`,
      chargeType: row.chargeType,
      taxCategory: row.taxCategory,
      accountId: row.accountId,
      accountName: row.accountName,
      referenceRowIndex: row.referenceRowIndex,
      includedInPrintRate: row.includedInPrintRate,
      isActive: true,
    }));
    this.selectedTaxRules.set(rules);
    this.recalculate();
  }

  private resolveCustomerDetails(customerId: string): void {
    this.partyDetailsService.getCustomerDetails({ partyType: 'Customer', partyId: customerId }).subscribe({
      next: (details: any) => {
        if (details.tin) {
          this.form.patchValue({ buyerTin: details.tin });
          this.partyTin.set(details.tin);
        }
        if (details.billingAddress) {
          const addr = details.billingAddress;
          const parts = [addr.addressLine1, addr.city, addr.state, addr.postalCode].filter(Boolean);
          this.billingAddress.set(parts.join(', '));
        }
        if (details.defaultPaymentTermsTemplateId && !this.form.get('paymentTermsTemplateId')?.value) {
          this.form.patchValue({ paymentTermsTemplateId: details.defaultPaymentTermsTemplateId });
          this.onPaymentTermsChanged();
        }
      },
      error: () => {},
    });
  }

  loadFromSalesOrders(): void {
    const customerId = this.form.get('customerId')?.value;
    const companyId = this.form.get('companyId')?.value;
    if (!customerId) return;
    this.isLoadingSoItems.set(true);
    this.service.getUnbilledOrderItems(customerId, companyId || undefined).subscribe({
      next: (items: any[]) => {
        this.isLoadingSoItems.set(false);
        if (!items?.length) return;
        // Clear existing items and load from SO
        this.items.clear();
        items.forEach((item: any) => {
          this.addItemRow({
            itemId: item.itemId,
            description: item.itemName ?? item.description,
            quantity: item.unbilledQty ?? item.quantity,
            unitPrice: item.rate ?? item.unitPrice ?? 0,
            uom: item.uom ?? 'EA',
          } as any);
        });
        this.recalculate();
      },
      error: () => this.isLoadingSoItems.set(false),
    });
  }

  loadFromDeliveryNotes(): void {
    const customerId = this.form.get('customerId')?.value;
    const companyId = this.form.get('companyId')?.value;
    if (!customerId) return;
    this.isLoadingDnItems.set(true);
    this.service.getUnbilledDeliveryItems(customerId, companyId || undefined).subscribe({
      next: (items: any[]) => {
        this.isLoadingDnItems.set(false);
        if (!items?.length) return;
        // Clear existing items and load from DN
        this.items.clear();
        items.forEach((item: any) => {
          this.addItemRow({
            itemId: item.itemId,
            description: item.itemName ?? item.description,
            quantity: item.unbilledQty ?? item.quantity,
            unitPrice: item.rate ?? item.unitPrice ?? 0,
            uom: item.uom ?? 'EA',
          } as any);
        });
        this.recalculate();
      },
      error: () => this.isLoadingDnItems.set(false),
    });
  }

  /** Used by unsaved-changes route guard */
  hasUnsavedChanges(): boolean {
    return this.form.dirty;
  }

  /** Fetches exchange rate when currency changes from base (MYR) */
  onCurrencyChanged(): void {
    const currency = this.form.get('currencyCode')?.value;
    const baseCurrency = 'MYR'; // Company base currency
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
      error: () => {
        // Non-blocking: user can manually enter exchange rate
      }
    });
  }

  /** Recalculates payment schedule preview when template or grand total changes */
  onPaymentTermsChanged(): void {
    const templateId = this.form.get('paymentTermsTemplateId')?.value;
    if (!templateId) {
      this.paymentSchedulePreview.set([]);
      return;
    }
    const template = this.paymentTermsTemplates().find((t: any) => t.id === templateId);
    if (!template?.terms?.length) {
      this.paymentSchedulePreview.set([]);
      return;
    }

    const issueDate = this.form.get('issueDate')?.value || new Date().toISOString().split('T')[0];
    const grandTotal = this.calcResult.grandTotal;
    const schedule: { dueDate: string; portion: number; amount: number }[] = [];

    for (const term of template.terms) {
      const dueDateObj = new Date(issueDate);
      dueDateObj.setDate(dueDateObj.getDate() + (term.creditDays ?? 0));
      const amount = grandTotal * (term.invoicePortion ?? 0) / 100;
      schedule.push({
        dueDate: dueDateObj.toISOString().split('T')[0],
        portion: term.invoicePortion ?? 0,
        amount: Math.round(amount * 100) / 100,
      });
    }

    this.paymentSchedulePreview.set(schedule);

    // Auto-set due date to the latest scheduled date (per ERPNext: DueDate = MAX(schedule dates))
    if (schedule.length > 0) {
      const maxDueDate = schedule.reduce((max, s) => s.dueDate > max ? s.dueDate : max, schedule[0].dueDate);
      this.form.patchValue({ dueDate: maxDueDate });
    }
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
