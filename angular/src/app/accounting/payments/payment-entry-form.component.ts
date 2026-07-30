import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import type { AccountDto, CreatePaymentEntryDto } from '../../proxy/accounting/models';
import { AccountSubType } from '../../proxy/accounting/account-sub-type.enum';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-payment-entry-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, AutoValidationDirective, SaveShortcutDirective, LocalizationPipe],
  templateUrl: './payment-entry-form.component.html',
  styleUrls: ['./payment-entry-form.component.scss'],
})
export class PaymentEntryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private paymentService = inject(PaymentEntryService);
  private accountService = inject(AccountService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);
  private toaster = inject(ToasterService);
  private localization = inject(LocalizationService);
  private companyContext = inject(CompanyContextService);
  private http = inject(HttpClient);

  accounts = signal<AccountDto[]>([]);
  parties = signal<{ id: string; name: string }[]>([]);
  costCenters = signal<{ id: string; name: string }[]>([]);
  projects = signal<{ id: string; name: string }[]>([]);
  linkedDocLabel = signal('');
  outstandingInvoices = signal<any[]>([]);
  outstandingOrders = signal<any[]>([]);
  allocations = signal<Map<string, number>>(new Map());
  totalInvoiceOutstanding = signal(0);
  totalOrderPending = signal(0);
  isEditMode = false;
  entityId: string | null = null;

  // Filtered account lists by sub-type (per ERPNext PE account resolution)
  bankCashAccounts = computed(() =>
    this.accounts().filter(a => a.accountSubType === AccountSubType.BankAccount || a.accountSubType === AccountSubType.CashAccount)
  );
  receivableAccounts = computed(() =>
    this.accounts().filter(a => a.accountSubType === AccountSubType.AccountsReceivable)
  );
  payableAccounts = computed(() =>
    this.accounts().filter(a => a.accountSubType === AccountSubType.AccountsPayable)
  );

  // Dynamic account lists based on payment type (Receive: party=receivable, bank=paid_to; Pay: party=payable, bank=paid_from)
  partyAccountOptions = computed(() => {
    const type = this.form?.get('paymentType')?.value;
    return type === 'Pay' ? this.payableAccounts() : this.receivableAccounts();
  });
  bankAccountOptions = computed(() => this.bankCashAccounts());

  totalAllocated = computed(() => {
    let sum = 0;
    this.allocations().forEach(v => sum += v);
    return sum;
  });

  unallocatedAmount = computed(() => {
    return (this.form?.get('amount')?.value ?? 0) - this.totalAllocated();
  });

  form = this.fb.group({
    companyId: ['', Validators.required],
    paymentType: ['Receive', Validators.required],
    paymentDate: [new Date().toISOString().split('T')[0], Validators.required],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    paidFromAccount: ['', Validators.required],
    paidToAccount: ['', Validators.required],
    modeOfPayment: [''],
    partyType: ['Customer'],
    partyId: [''],
    reference: [''],
    costCenterId: [''],
    projectId: [''],
    remarks: [''],
    againstInvoiceId: [''],
    againstOrderId: [''],
    againstOrderType: [''],
    currency: ['MYR'],
    exchangeRate: [1, [Validators.min(0.0001)]],
  });

  isMultiCurrency = computed(() => {
    const currency = this.form?.get('currency')?.value;
    return !!currency && currency !== 'MYR';
  });

  baseAmount = computed(() => {
    const amount = this.form?.get('amount')?.value || 0;
    const rate = this.form?.get('exchangeRate')?.value || 1;
    return amount * rate;
  });

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (!this.isEditMode) {
      const cid = this.companyContext.currentCompanyId();
      if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });
    }

    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe({ next: (res) => {
        this.accounts.set(res.items ?? []);
        // Auto-resolve accounts once list is loaded (only for new entries)
        if (!this.isEditMode) { this.resolveAccounts(); }
      }, error: () => {} });

    // Load cost centers and projects for dimension selectors
    this.http.get<any>('/api/app/cost-center', { params: { skipCount: '0', maxResultCount: '200' } }).subscribe({
      next: (res) => this.costCenters.set((res.items ?? []).map((cc: any) => ({ id: cc.id, name: cc.name }))),
      error: () => {},
    });
    this.http.get<any>('/api/app/project', { params: { skipCount: '0', maxResultCount: '200' } }).subscribe({
      next: (res) => this.projects.set((res.items ?? []).map((p: any) => ({ id: p.id, name: p.projectName ?? p.name }))),
      error: () => {},
    });

    // Load parties based on initial party type
    this.loadParties(this.form.get('partyType')?.value ?? 'Customer');

    // Reload parties when party type changes
    this.form.get('partyType')?.valueChanges.subscribe({ next: (type) => {
      if (type) {
        this.loadParties(type);
        this.form.patchValue({ partyId: '' });
        this.outstandingInvoices.set([]);
        this.allocations.set(new Map());
        this.resolveAccounts();
      }
    }, error: () => {} });

    // Auto-resolve accounts when payment type changes
    this.form.get('paymentType')?.valueChanges.subscribe({ next: () => {
      this.resolveAccounts();
    }, error: () => {} });

    // Auto-resolve bank account when mode of payment changes
    this.form.get('modeOfPayment')?.valueChanges.subscribe({ next: () => {
      this.resolveBankAccount();
    }, error: () => {} });

    // Auto-fetch exchange rate when currency changes
    this.form.get('currency')?.valueChanges.subscribe({ next: (currency) => {
      if (!currency || currency === 'MYR') {
        this.form.patchValue({ exchangeRate: 1 });
      } else {
        this.fetchExchangeRate(currency);
      }
    }, error: () => {} });

    if (this.isEditMode) {
      this.paymentService.get(this.entityId!).subscribe(pe => {
        this.form.patchValue({
          companyId: pe.companyId,
          paymentType: pe.paymentType,
          paymentDate: pe.postingDate ?? '',
          amount: pe.paidAmount,
          paidFromAccount: '',
          paidToAccount: '',
          partyType: 'Customer',
          reference: pe.referenceNumber ?? '',
          remarks: '',
        });
      });
      return;
    }

    // Pre-fill from query params (from "Make Payment" buttons on SI/PI/SO/PO detail)
    const params = this.route.snapshot.queryParams;
    if (params['partyType']) {
      this.form.patchValue({ partyType: params['partyType'] });
      if (params['partyType'] === 'Supplier') {
        this.form.patchValue({ paymentType: 'Pay' });
      }
    }
    if (params['partyId']) {
      this.form.patchValue({ partyId: params['partyId'] });
    }
    if (params['amount']) {
      this.form.patchValue({ amount: parseFloat(params['amount']) });
    }
    if (params['companyId']) {
      this.form.patchValue({ companyId: params['companyId'] });
    }
    if (params['currency'] && params['currency'] !== 'MYR') {
      this.form.patchValue({ currency: params['currency'] });
    }
    if (params['againstInvoiceId']) {
      this.form.patchValue({ againstInvoiceId: params['againstInvoiceId'] });
      this.linkedDocLabel.set(`Against ${params['againstInvoiceType'] ?? 'Invoice'}: ${params['againstInvoiceId']?.substring(0, 8)}...`);
    }
    if (params['againstOrderId']) {
      this.form.patchValue({
        againstOrderId: params['againstOrderId'],
        againstOrderType: params['againstOrderType'] ?? '',
      });
      this.linkedDocLabel.set(`Advance against ${params['againstOrderType'] ?? 'Order'}`);
    }

    // Fetch outstanding invoices when party info is available from query params
    // partyId is now pre-filled, so loadOutstandingInvoices will succeed
    if (params['partyType'] && params['partyId'] && params['partyType'] !== 'InternalTransfer') {
      this.loadOutstandingInvoices(params['partyType']);
    }
  }

  /**
   * After outstanding invoices load, auto-select the specific invoice if navigated from "Make Payment" button.
   * Sets allocation to the invoice's outstanding amount and auto-fills the payment amount.
   */
  private autoSelectInvoiceFromParams(): void {
    const invoiceId = this.form.get('againstInvoiceId')?.value;
    if (!invoiceId) return;

    const invoices = this.outstandingInvoices();
    const match = invoices.find((inv: any) => inv.invoiceId === invoiceId);
    if (match) {
      const newMap = new Map<string, number>();
      newMap.set(match.invoiceId, match.outstanding);
      this.allocations.set(newMap);
      // Auto-fill amount if not already set by user or query param
      const currentAmount = this.form.get('amount')?.value ?? 0;
      if (currentAmount <= 0) {
        this.form.patchValue({ amount: match.outstanding });
      }
      this.linkedDocLabel.set(`Against ${match.invoiceType ?? 'Invoice'}: ${match.invoiceNumber}`);
    }
  }

  loadOutstandingInvoices(partyType: string): void {
    const companyId = this.form.get('companyId')?.value;
    const partyId = this.form.get('partyId')?.value;
    if (!partyId) {
      this.outstandingInvoices.set([]);
      this.outstandingOrders.set([]);
      return;
    }
    this.paymentService.getPartyOutstanding(partyType, partyId, companyId || '').subscribe({
      next: (result) => {
        this.outstandingInvoices.set(result?.invoices ?? []);
        this.outstandingOrders.set(result?.orders ?? []);
        this.totalInvoiceOutstanding.set(result?.totalInvoiceOutstanding ?? 0);
        this.totalOrderPending.set(result?.totalOrderPending ?? 0);
        this.autoSelectInvoiceFromParams();
      },
      error: () => {
        this.outstandingInvoices.set([]);
        this.outstandingOrders.set([]);
      },
    });
  }

  fetchExchangeRate(fromCurrency: string): void {
    const date = this.form.get('paymentDate')?.value || new Date().toISOString().split('T')[0];
    this.http.get<any>(`/api/app/currency-exchange/rate`, {
      params: { from: fromCurrency, to: 'MYR', date }
    }).subscribe({
      next: (res) => {
        if (res?.rate) {
          this.form.patchValue({ exchangeRate: res.rate });
        }
      },
      error: () => {} // Graceful: user can enter rate manually
    });
  }

  selectInvoice(inv: any): void {
    // Legacy single-invoice path (from "Make Payment" button with single target)
    const newMap = new Map(this.allocations());
    newMap.clear();
    newMap.set(inv.invoiceId, inv.outstanding);
    this.allocations.set(newMap);
    this.form.patchValue({
      againstInvoiceId: inv.invoiceId,
      amount: inv.outstanding,
    });
    this.linkedDocLabel.set(`Against ${inv.invoiceType}: ${inv.invoiceNumber}`);
  }

  toggleInvoice(inv: any, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const newMap = new Map(this.allocations());
    if (checked) {
      // Allocate remaining unallocated or full outstanding (whichever is less)
      const remaining = (this.form.get('amount')?.value ?? 0) - this.totalAllocated();
      const allocateAmount = Math.min(Math.max(remaining, 0), inv.outstanding);
      newMap.set(inv.invoiceId, allocateAmount > 0 ? allocateAmount : inv.outstanding);
    } else {
      newMap.delete(inv.invoiceId);
    }
    this.allocations.set(newMap);
    this.syncAllocationsToForm();
  }

  updateAllocation(invoiceId: string, event: Event): void {
    const value = parseFloat((event.target as HTMLInputElement).value) || 0;
    const newMap = new Map(this.allocations());
    newMap.set(invoiceId, Math.max(0, value));
    this.allocations.set(newMap);
    this.syncAllocationsToForm();
  }

  isInvoiceSelected(invoiceId: string): boolean {
    return this.allocations().has(invoiceId);
  }

  getAllocatedAmount(invoiceId: string): number {
    return this.allocations().get(invoiceId) ?? 0;
  }

  private syncAllocationsToForm(): void {
    // If single invoice selected, set legacy field for backward compat
    const entries = Array.from(this.allocations().entries());
    if (entries.length === 1) {
      this.form.patchValue({ againstInvoiceId: entries[0][0] });
      this.linkedDocLabel.set(`Against 1 invoice`);
    } else if (entries.length > 1) {
      this.form.patchValue({ againstInvoiceId: '' });
      this.linkedDocLabel.set(`Against ${entries.length} invoices`);
    } else {
      this.form.patchValue({ againstInvoiceId: '' });
      this.linkedDocLabel.set('');
    }
  }

  /**
   * Auto-resolve paid_from and paid_to accounts based on payment type.
   * Per ERPNext PE: Receive → FROM party receivable, TO bank; Pay → FROM bank, TO party payable
   */
  private resolveAccounts(): void {
    const paymentType = this.form.get('paymentType')?.value;
    const bankAccounts = this.bankCashAccounts();
    const receivable = this.receivableAccounts();
    const payable = this.payableAccounts();

    if (paymentType === 'Receive') {
      // Receive: FROM customer receivable → TO bank
      if (receivable.length > 0 && !this.form.get('paidFromAccount')?.value) {
        this.form.patchValue({ paidFromAccount: receivable[0].id });
      }
      if (bankAccounts.length > 0 && !this.form.get('paidToAccount')?.value) {
        this.form.patchValue({ paidToAccount: bankAccounts[0].id });
      }
    } else if (paymentType === 'Pay') {
      // Pay: FROM bank → TO supplier payable
      if (bankAccounts.length > 0 && !this.form.get('paidFromAccount')?.value) {
        this.form.patchValue({ paidFromAccount: bankAccounts[0].id });
      }
      if (payable.length > 0 && !this.form.get('paidToAccount')?.value) {
        this.form.patchValue({ paidToAccount: payable[0].id });
      }
    }
  }

  /**
   * Auto-resolve bank/cash account when Mode of Payment changes.
   * Per ERPNext: Cash MoP → cash account, Bank Transfer → bank account
   */
  private resolveBankAccount(): void {
    const mop = this.form.get('modeOfPayment')?.value;
    const paymentType = this.form.get('paymentType')?.value;
    if (!mop) return;

    const bankAccounts = this.bankCashAccounts();
    // Try to find account matching mode type (Cash→CashAccount, Wire Transfer→BankAccount)
    const isCash = mop.toLowerCase().includes('cash');
    const targetAccounts = bankAccounts.filter(a =>
      isCash ? a.accountSubType === AccountSubType.CashAccount : a.accountSubType === AccountSubType.BankAccount
    );
    const resolvedAccount = targetAccounts.length > 0 ? targetAccounts[0].id : '';

    if (resolvedAccount) {
      if (paymentType === 'Receive') {
        this.form.patchValue({ paidToAccount: resolvedAccount });
      } else if (paymentType === 'Pay') {
        this.form.patchValue({ paidFromAccount: resolvedAccount });
      }
    }
  }

  loadParties(partyType: string): void {
    const service$: any = partyType === 'Customer'
      ? this.customerService.getList({ skipCount: 0, maxResultCount: 200 } as any)
      : this.supplierService.getList({ skipCount: 0, maxResultCount: 200 } as any);

    service$.subscribe({
      next: (res: any) => {
        const items = res.items ?? [];
        this.parties.set(items.map((p: any) => ({
          id: p.id,
          name: p.customerName ?? p.name ?? p.supplierName ?? p.id,
        })));
      },
      error: () => this.parties.set([]),
    });
  }

  onPartySelected(): void {
    const partyId = this.form.get('partyId')?.value;
    const partyType = this.form.get('partyType')?.value;
    if (partyId && partyType) {
      this.loadOutstandingInvoices(partyType);
      this.resolveAccounts();
    } else {
      this.outstandingInvoices.set([]);
      this.outstandingOrders.set([]);
      this.allocations.set(new Map());
    }
  }

  selectOrder(order: any): void {
    this.form.patchValue({
      againstOrderId: order.orderId,
      againstOrderType: order.orderType,
      amount: order.pendingAdvance,
    });
    this.allocations.set(new Map());
    this.linkedDocLabel.set(`Advance against ${order.orderType}: ${order.orderNumber}`);
  }

  /** Auto-allocate payment amount FIFO across outstanding invoices (oldest first) */
  autoAllocate(): void {
    const paymentAmount = this.form.get('amount')?.value ?? 0;
    if (paymentAmount <= 0) return;

    const invoices = this.outstandingInvoices();
    if (invoices.length === 0) return;

    // Sort by posting date ascending (oldest first = FIFO per ERPNext)
    const sorted = [...invoices].sort((a, b) =>
      (a.postingDate ?? '').localeCompare(b.postingDate ?? '')
    );

    const newMap = new Map<string, number>();
    let remaining = paymentAmount;

    for (const inv of sorted) {
      if (remaining <= 0) break;
      const allocate = Math.min(remaining, inv.outstanding ?? 0);
      if (allocate > 0) {
        newMap.set(inv.invoiceId, allocate);
        remaining -= allocate;
      }
    }

    this.allocations.set(newMap);
    this.syncAllocationsToForm();

    const count = newMap.size;
    this.toaster.success(this.localization.instant('::AutoAllocated', count.toString()));
  }

  cancel(): void {
    this.router.navigate(['/accounting/payments']);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const dto: any = {
      ...raw,
      postingDate: raw.paymentDate,
      paidAmount: raw.amount,
      paidFromAccountId: raw.paidFromAccount,
      paidToAccountId: raw.paidToAccount,
      referenceNumber: raw.reference,
    };

    // Multi-invoice allocation: include references array
    const allocs = Array.from(this.allocations().entries());
    if (allocs.length > 1) {
      dto.references = allocs.map(([invoiceId, amount]) => ({
        referenceType: raw.partyType === 'Customer' ? 'SalesInvoice' : 'PurchaseInvoice',
        referenceId: invoiceId,
        allocatedAmount: amount,
        exchangeRate: raw.exchangeRate ?? 1,
      }));
      dto.againstInvoiceId = null; // Clear single-invoice field
    } else if (allocs.length === 1) {
      dto.againstInvoiceId = allocs[0][0];
    }

    if (this.isEditMode) {
      this.paymentService.update(this.entityId!, dto).subscribe({
        next: () => {
          this.toaster.success(this.localization.instant('::SuccessfullyUpdated'));
          this.router.navigate(['/accounting/payments', this.entityId]);
        },
        error: (err: any) => {
          this.toaster.error(err?.error?.error?.message ?? this.localization.instant('::FailedToCreate'));
        },
      });
    } else {
      this.paymentService.create(dto).subscribe({
        next: () => {
          this.toaster.success(this.localization.instant('::SuccessfullyCreated'));
          this.router.navigate(['/accounting/payments']);
        },
        error: (err: any) => {
          this.toaster.error(err?.error?.error?.message ?? this.localization.instant('::FailedToCreate'));
        },
      });
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
