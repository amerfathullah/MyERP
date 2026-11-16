import { Component, computed, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TaxChargesTemplateService } from '../../proxy/tax/tax-charges-template.service';
import { TaxTemplateType } from '../../proxy/tax/tax-template-type.enum';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { InvoiceItemGridComponent } from './components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SalesPersonService } from '../../proxy/sales/sales-person.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { PaymentTermsTemplateService } from '../../proxy/accounting/payment-terms-template.service';
import { CurrencyExchangeService } from '../../proxy/accounting/currency-exchange.service';
import { PartyDetailsService } from '../../proxy/core/party-details.service';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import { PriceListService } from '../../proxy/inventory/price-list.service';
import type { CreateSalesInvoiceDto, SalesInvoiceItemDto, SalesOrderDto, SalesTeamAllocationInputDto } from '../../proxy/sales/models';
import type { TaxRuleDto as TaxRuleModel } from '../../proxy/tax/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-sales-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    RouterLink,
    PageModule,
    LocalizationPipe,
    InvoiceItemGridComponent,
    AutoValidationDirective,
    SaveShortcutDirective],
  templateUrl: './sales-invoice-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./sales-invoice-form.component.scss'],
})
export class SalesInvoiceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private taxCalc = inject(TaxCalculationService);
  private service = inject(SalesInvoiceService);
  private salesOrderService = inject(SalesOrderService);
  private customerService = inject(CustomerService);
  private salesPersonService = inject(SalesPersonService);
  private companyContext = inject(CompanyContextService);
  private warehouseService = inject(WarehouseService);
  private paymentTermsService = inject(PaymentTermsTemplateService);
  private currencyExchangeService = inject(CurrencyExchangeService);
  private partyDetailsService = inject(PartyDetailsService);
  private taxCategoryService = inject(TaxCategoryService);
  private taxRuleService = inject(TaxRuleService);
  private taxTemplateService = inject(TaxChargesTemplateService);
  private priceListService = inject(PriceListService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  isMultiCurrency = signal(false);
  customers = signal<any[]>([]);
  priceLists = signal<any[]>([]);
  taxCategories = signal<any[]>([]);
  taxTemplates = signal<any[]>([]);
  selectedTaxRules = signal<TaxRuleModel[]>([]);
  billingAddress = signal<string>('');
  partyTin = signal<string>('');
  customerOutstanding = signal<number>(0);
  customerCreditLimit = signal<number>(0);
  creditUtilizationPct = computed(() => {
    const limit = this.customerCreditLimit();
    if (limit <= 0) return 0; // 0 = unlimited
    return Math.min(100, Math.round((this.customerOutstanding() / limit) * 100));
  });
  showCreditWarning = computed(() => this.creditUtilizationPct() >= 80);
  warehouses = signal<any[]>([]);
  paymentTermsTemplates = signal<any[]>([]);
  isLoadingSoItems = signal(false);
  isLoadingDnItems = signal(false);
  showBillableSoPicker = signal(false);
  billableSalesOrders = signal<SalesOrderDto[]>([]);
  selectedBillableSoIds = signal<Set<string>>(new Set());
  paymentSchedulePreview = signal<{ dueDate: string; portion: number; amount: number }[]>([]);
  returnAgainstNumber = signal<string>('');

  // Sales Team (commission split)
  salesPersons = signal<{ id: string; name: string; commissionRate: number }[]>([]);
  salesTeamRows = signal<{ salesPersonId: string; allocatedPercentage: number; commissionRate: number | null }[]>([]);
  salesTeamTotalPercentage = computed(() =>
    Math.round(this.salesTeamRows().reduce((s, r) => s + (r.allocatedPercentage || 0), 0) * 100) / 100);

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
    priceListId: [''],
    currencyCode: ['MYR'],
    exchangeRate: [1],
    notes: [''],
    isReturn: [false],
    isDebitNote: [false],
    isReturnRefund: [false],
    eInvoiceDocType: [1 as number | null],
    isSubmitToLhdn: [true],
    returnAgainstId: [null as string | null],
    originalInvoiceNumber: [''],
    originalInvoiceUuid: [''],
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

    // Auto-sync e-Invoice doc type based on isReturn (01/03 vs 02/04) and transaction flags
    this.form.get('isReturn')?.valueChanges.subscribe((isRet) => {
      const current = this.form.get('eInvoiceDocType')?.value;
      if (isRet) {
        if (current !== 2 && current !== 4) {
          this.form.patchValue({ eInvoiceDocType: 2, isDebitNote: false });
        }
      } else {
        if (current !== 1 && current !== 3) {
          this.form.patchValue({ eInvoiceDocType: 1, isReturnRefund: false });
        }
      }
    });

    this.form.get('eInvoiceDocType')?.valueChanges.subscribe((docType) => {
      const numType = docType != null ? Number(docType) : null;
      const isRet = numType === 2 || numType === 4;
      const isDebit = numType === 3;
      const isRefund = numType === 4;
      const patch: Record<string, unknown> = {};
      if (this.form.get('isReturn')?.value !== isRet) {
        patch['isReturn'] = isRet;
      }
      if (this.form.get('isDebitNote')?.value !== isDebit) {
        patch['isDebitNote'] = isDebit;
      }
      if (this.form.get('isReturnRefund')?.value !== isRefund) {
        patch['isReturnRefund'] = isRefund;
      }
      if (isDebit && this.form.get('updateStock')?.value) {
        patch['updateStock'] = false;
      }
      if (Object.keys(patch).length > 0) {
        this.form.patchValue(patch);
      }
    });

    // Load customer list for dropdown
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe({ next: res => this.customers.set(res.items ?? []), error: () => {} });

    this.priceListService.getList({ skipCount: 0, maxResultCount: 100, sorting: 'name asc' })
      .subscribe({ next: res => this.priceLists.set((res.items ?? []).filter((p: any) => p.isSelling && p.isActive)), error: () => {} });

    // Load warehouses for UpdateStock option
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe({ next: res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)), error: () => {} });

    // Load payment terms templates
    this.paymentTermsService.getList({ skipCount: 0, maxResultCount: 50, sorting: 'name asc' })
      .subscribe({ next: res => this.paymentTermsTemplates.set(res.items ?? []), error: () => {} });

    // Load sales persons for commission split
    this.salesPersonService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' } as any)
      .subscribe({
        next: (res: any) => this.salesPersons.set((res.items ?? [])
          .filter((sp: any) => sp.isEnabled && !sp.isGroup)
          .map((sp: any) => ({ id: sp.id, name: sp.name, commissionRate: sp.commissionRate ?? 0 }))),
        error: () => {},
      });

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
          priceListId: invoice.priceListId ?? '',
          isReturn: invoice.isReturn ?? false,
          isDebitNote: invoice.isDebitNote ?? false,
          isReturnRefund: invoice.isReturnRefund ?? false,
          eInvoiceDocType: invoice.eInvoiceDocType ?? (invoice.isDebitNote ? 3 : (invoice.isReturn ? (invoice.isReturnRefund ? 4 : 2) : 1)),
          isSubmitToLhdn: invoice.isSubmitToLhdn ?? true,
          returnAgainstId: invoice.returnAgainstId ?? null,
          originalInvoiceNumber: invoice.originalInvoiceNumber ?? '',
          originalInvoiceUuid: invoice.originalInvoiceUuid ?? '',
        });
        // Rebuild child FormArray from loaded items
        invoice.items?.forEach((item) => this.addItemRow(item));

        if (invoice.returnAgainstId) {
          this.service.get(invoice.returnAgainstId).subscribe({
            next: (source) => this.returnAgainstNumber.set(source.invoiceNumber ?? ''),
            error: () => {},
          });
        }
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
            priceListId: source.priceListId ?? '',
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
            isDebitNote: false,
            isReturnRefund: false,
            eInvoiceDocType: 2,
            returnAgainstId: returnAgainst,
            priceListId: source.priceListId ?? '',
          });
          this.returnAgainstNumber.set(source.invoiceNumber ?? '');
          // Per ERPNext PR #66918: return keeps taxes of source document; clear if source had no taxes
          if (!(source.taxAmount > 0) && (source.items ?? []).every(i => !(i?.taxAmount > 0))) {
            this.selectedTaxRules.set([]);
          }
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

  addItemRow(item?: any): void {
    this.items.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [item?.taxAmount ?? 0],
      uom: [item?.uom ?? 'EA'],
      salesOrderItemId: [item?.salesOrderItemId ?? null],
      deliveryNoteItemId: [item?.deliveryNoteItemId ?? null],
      classificationCode: [item?.classificationCode ?? ''],
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

  private isReturnAgainst(): boolean {
    return (!!this.form?.get('isReturn')?.value && !!this.form?.get('returnAgainstId')?.value)
      || !!this.route.snapshot.queryParams['returnAgainst'];
  }

  /** Load available tax charges templates (Selling type) for this company. */
  private loadTaxTemplates(): void {
    const companyId = this.form?.get('companyId')?.value || this.companyContext.currentCompanyId();
    const skipDefault = this.isEditMode || this.isReturnAgainst();
    if (!companyId) {
      this.taxTemplateService.getList({ skipCount: 0, maxResultCount: 100, templateType: TaxTemplateType.Selling } as any).subscribe({
        next: res => {
          const templates = (res.items ?? []).filter((t: any) => t.isEnabled && (t.templateType == null || t.templateType === TaxTemplateType.Selling));
          this.taxTemplates.set(templates);
          if (!skipDefault) {
            const defaultTmpl = templates.find((t: any) => t.isDefault);
            if (defaultTmpl) this.applyTaxTemplate(defaultTmpl);
          }
        },
        error: () => {},
      });
      return;
    }

    this.taxTemplateService.getActiveTemplates(companyId, TaxTemplateType.Selling).subscribe({
      next: list => {
        const templates = list ?? [];
        this.taxTemplates.set(templates);
        if (!skipDefault) {
          this.taxTemplateService.getDefault(companyId, TaxTemplateType.Selling).subscribe({
            next: def => {
              const target = def ?? templates.find((t: any) => t.isDefault);
              if (target && !this.isReturnAgainst()) this.applyTaxTemplate(target);
            },
            error: () => {
              const fallback = templates.find((t: any) => t.isDefault);
              if (fallback && !this.isReturnAgainst()) this.applyTaxTemplate(fallback);
            },
          });
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
    const companyId = this.form.get('companyId')?.value || undefined;
    this.partyDetailsService.getCustomerDetails({ partyId: customerId, companyId }).subscribe({
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
        // Price list fallback reset (ERPNext PR #58893)
        this.form.patchValue({ priceListId: details.priceListId ?? '' });

        // Credit exposure tracking
        this.customerOutstanding.set(details.outstandingAmount ?? 0);
        this.customerCreditLimit.set(details.creditLimit ?? 0);
        if (details.defaultPaymentTermsTemplateId && !this.form.get('paymentTermsTemplateId')?.value) {
          this.form.patchValue({ paymentTermsTemplateId: details.defaultPaymentTermsTemplateId });
          this.onPaymentTermsChanged();
        }
        if (companyId && !this.isEditMode && !this.isReturnAgainst() && details.taxCategoryId) {
          this.taxTemplateService.getDefault(companyId, TaxTemplateType.Selling, details.taxCategoryId).subscribe({
            next: def => { if (def && !this.isReturnAgainst()) this.applyTaxTemplate(def); },
            error: () => {},
          });
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
    this.salesOrderService.getBillableSalesOrders(customerId, companyId || undefined).subscribe({
      next: (orders: SalesOrderDto[]) => {
        const list = orders ?? [];
        if (list.length === 0) {
          this.isLoadingSoItems.set(false);
          this.showBillableSoPicker.set(false);
          this.billableSalesOrders.set([]);
          this.toaster.info(this.l.instant('::NoBillableSalesOrders'));
          return;
        }
        if (list.length === 1 && list[0].id) {
          this.fetchUnbilledSoItems(customerId, companyId || undefined, new Set([list[0].id]));
          return;
        }
        this.isLoadingSoItems.set(false);
        this.billableSalesOrders.set(list);
        this.selectedBillableSoIds.set(new Set(list.map(o => o.id).filter((id): id is string => !!id)));
        this.showBillableSoPicker.set(true);
      },
      error: () => this.isLoadingSoItems.set(false),
    });
  }

  toggleBillableSo(soId: string, checked: boolean): void {
    const next = new Set(this.selectedBillableSoIds());
    if (checked) next.add(soId);
    else next.delete(soId);
    this.selectedBillableSoIds.set(next);
  }

  loadSelectedBillableOrders(): void {
    const customerId = this.form.get('customerId')?.value;
    const companyId = this.form.get('companyId')?.value;
    const selectedIds = this.selectedBillableSoIds();
    if (!customerId || selectedIds.size === 0) return;
    this.isLoadingSoItems.set(true);
    this.fetchUnbilledSoItems(customerId, companyId || undefined, selectedIds);
  }

  closeBillableSoPicker(): void {
    this.showBillableSoPicker.set(false);
  }

  private fetchUnbilledSoItems(customerId: string, companyId?: string, filterOrderIds?: Set<string>): void {
    this.service.getUnbilledOrderItems(customerId, companyId).subscribe({
      next: (items: any[]) => {
        this.isLoadingSoItems.set(false);
        this.showBillableSoPicker.set(false);
        const scopedItems = filterOrderIds?.size
          ? (items ?? []).filter((item: any) => !item.salesOrderId || filterOrderIds.has(item.salesOrderId))
          : (items ?? []);
        if (!scopedItems.length) {
          this.toaster.info(this.l.instant('::NoUnbilledOrderItems'));
          return;
        }

        // Compute quantities already mapped into the target form to prevent duplicate rows (ERPNext PR #58617)
        const mappedQtyBySoItem = new Map<string, number>();
        this.items.controls.forEach(c => {
          const soItemId = c.get('salesOrderItemId')?.value;
          const qty = Number(c.get('quantity')?.value) || 0;
          if (soItemId) {
            mappedQtyBySoItem.set(soItemId, (mappedQtyBySoItem.get(soItemId) || 0) + qty);
          }
        });

        let loadedCount = 0;
        scopedItems.forEach((item: any) => {
          const soItemId = item.salesOrderItemId ?? item.id;
          const alreadyMapped = mappedQtyBySoItem.get(soItemId) || 0;
          const totalPending = item.unbilledQty ?? item.quantity ?? 0;
          const remainingQty = totalPending - alreadyMapped;

          if (remainingQty > 0) {
            this.addItemRow({
              itemId: item.itemId,
              description: item.itemName ?? item.description,
              quantity: remainingQty,
              unitPrice: item.rate ?? item.unitPrice ?? 0,
              uom: item.uom ?? 'EA',
              salesOrderItemId: soItemId,
            } as any);
            mappedQtyBySoItem.set(soItemId, alreadyMapped + remainingQty);
            loadedCount++;
          }
        });

        if (loadedCount > 0) {
          this.recalculate();
          this.toaster.success(this.l.instant('::ItemsLoadedFromSO', loadedCount.toString()));
        } else {
          this.toaster.info(this.l.instant('::AllItemsAlreadyBilled'));
        }
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
        if (!items?.length) {
          this.toaster.info(this.l.instant('::NoUnbilledDeliveryItems'));
          return;
        }

        // Compute quantities already mapped into the target form to prevent duplicate rows (ERPNext PR #58617)
        const mappedQtyByDnItem = new Map<string, number>();
        this.items.controls.forEach(c => {
          const dnItemId = c.get('deliveryNoteItemId')?.value;
          const qty = Number(c.get('quantity')?.value) || 0;
          if (dnItemId) {
            mappedQtyByDnItem.set(dnItemId, (mappedQtyByDnItem.get(dnItemId) || 0) + qty);
          }
        });

        let loadedCount = 0;
        items.forEach((item: any) => {
          const dnItemId = item.deliveryNoteItemId ?? item.id;
          const alreadyMapped = mappedQtyByDnItem.get(dnItemId) || 0;
          const totalPending = item.unbilledQty ?? item.quantity ?? 0;
          const remainingQty = totalPending - alreadyMapped;

          if (remainingQty > 0) {
            this.addItemRow({
              itemId: item.itemId,
              description: item.itemName ?? item.description,
              quantity: remainingQty,
              unitPrice: item.rate ?? item.unitPrice ?? 0,
              uom: item.uom ?? 'EA',
              deliveryNoteItemId: dnItemId,
            } as any);
            mappedQtyByDnItem.set(dnItemId, alreadyMapped + remainingQty);
            loadedCount++;
          }
        });

        if (loadedCount > 0) {
          // Per ERPNext PR #60230 / commit 637fadfee9: disable update stock for sales invoice created from DN
          this.form.patchValue({ updateStock: false });
          this.recalculate();
          this.toaster.success(this.l.instant('::ItemsLoadedFromDN', loadedCount.toString()));
        } else {
          this.toaster.info(this.l.instant('::AllItemsAlreadyBilled'));
        }
      },
      error: () => this.isLoadingDnItems.set(false),
    });
  }

  hasDeliveryNoteItems(): boolean {
    return this.items.controls.some(c => !!c.get('deliveryNoteItemId')?.value);
  }

  addSalesTeamRow(): void {
    this.salesTeamRows.update(rows => [...rows, { salesPersonId: '', allocatedPercentage: 0, commissionRate: null }]);
  }

  removeSalesTeamRow(index: number): void {
    this.salesTeamRows.update(rows => rows.filter((_, i) => i !== index));
  }

  updateSalesTeamRow(index: number, patch: Partial<{ salesPersonId: string; allocatedPercentage: number; commissionRate: number | null }>): void {
    this.salesTeamRows.update(rows => rows.map((r, i) => i === index ? { ...r, ...patch } : r));
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
    const salesTeamRows = this.salesTeamRows().filter(r => r.salesPersonId);
    if (salesTeamRows.length > 0 && this.salesTeamTotalPercentage() !== 100) {
      return;
    }
    this.recalculate();

    const raw = this.form.getRawValue() as any;
    // Map item fields: handles both grid-added (qty/rate/itemName) and pre-loaded (quantity/unitPrice/description)
    const dto: CreateSalesInvoiceDto = {
      ...raw,
      eInvoiceDocType: raw.eInvoiceDocType != null ? Number(raw.eInvoiceDocType) : undefined,
      isDebitNote: raw.eInvoiceDocType != null ? Number(raw.eInvoiceDocType) === 3 : (raw.isDebitNote ?? false),
      isReturnRefund: raw.eInvoiceDocType != null ? Number(raw.eInvoiceDocType) === 4 : (raw.isReturnRefund ?? false),
      paymentTermsTemplateId: raw.paymentTermsTemplateId || undefined,
      dueDate: raw.dueDate || undefined,
      warehouseId: raw.warehouseId || undefined,
      returnAgainstId: raw.returnAgainstId || undefined,
      originalInvoiceNumber: raw.originalInvoiceNumber || undefined,
      originalInvoiceUuid: raw.originalInvoiceUuid || undefined,
      discountAmount: this.discountAmount() > 0 ? this.discountAmount() : undefined,
      applyDiscountOn: this.discountAmount() > 0 ? this.discountOn : undefined,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        description: item.description || item.itemName || '',
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        taxAmount: item.taxAmount ?? 0,
        uom: item.uom ?? 'Unit',
        salesOrderItemId: item.salesOrderItemId || undefined,
        deliveryNoteItemId: item.deliveryNoteItemId || undefined,
        classificationCode: item.classificationCode || undefined,
      })),
      salesTeam: salesTeamRows.length > 0
        ? salesTeamRows.map((r): SalesTeamAllocationInputDto => ({
            salesPersonId: r.salesPersonId,
            allocatedPercentage: r.allocatedPercentage,
            commissionRate: r.commissionRate,
          }))
        : undefined,
    };

    this.service.create(dto).subscribe({
      next: () => { this.form.markAsPristine(); this.router.navigate(['/sales/invoices']); },
      error: () => { /* handled by global error interceptor */ },
    });
  }

  cancel(): void {
    this.router.navigate(['/sales/invoices']);
  }
}
