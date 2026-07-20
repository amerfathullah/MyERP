import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import type { AccountDto, CreatePaymentEntryDto } from '../../proxy/accounting/models';

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
  private companyContext = inject(CompanyContextService);

  accounts = signal<AccountDto[]>([]);
  parties = signal<{ id: string; name: string }[]>([]);
  linkedDocLabel = signal('');
  outstandingInvoices = signal<any[]>([]);
  allocations = signal<Map<string, number>>(new Map());
  isEditMode = false;
  entityId: string | null = null;

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
    remarks: [''],
    againstInvoiceId: [''],
    againstOrderId: [''],
    againstOrderType: [''],
  });

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (!this.isEditMode) {
      const cid = this.companyContext.currentCompanyId();
      if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });
    }

    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe((res) => this.accounts.set(res.items ?? []));

    // Load parties based on initial party type
    this.loadParties(this.form.get('partyType')?.value ?? 'Customer');

    // Reload parties when party type changes
    this.form.get('partyType')?.valueChanges.subscribe((type) => {
      if (type) {
        this.loadParties(type);
        this.form.patchValue({ partyId: '' });
        this.outstandingInvoices.set([]);
        this.allocations.set(new Map());
      }
    });

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

    // Pre-fill from query params (from "Make Payment" buttons)
    const params = this.route.snapshot.queryParams;
    if (params['partyType']) {
      this.form.patchValue({ partyType: params['partyType'] });
      if (params['partyType'] === 'Supplier') {
        this.form.patchValue({ paymentType: 'Pay' });
      }
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

    // Fetch outstanding invoices when party info is available
    if (params['partyType'] && params['partyType'] !== 'InternalTransfer') {
      this.loadOutstandingInvoices(params['partyType']);
    }
  }

  loadOutstandingInvoices(partyType: string): void {
    const companyId = this.form.get('companyId')?.value;
    this.paymentService.getOutstandingForParty(partyType, '', companyId || '').subscribe({
      next: (invoices) => this.outstandingInvoices.set(invoices ?? []),
      error: () => {},
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
    } else {
      this.outstandingInvoices.set([]);
      this.allocations.set(new Map());
    }
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
        exchangeRate: 1,
      }));
      dto.againstInvoiceId = null; // Clear single-invoice field
    } else if (allocs.length === 1) {
      dto.againstInvoiceId = allocs[0][0];
    }

    if (this.isEditMode) {
      this.paymentService.update(this.entityId!, dto).subscribe({
        next: () => {
          this.toaster.success('Payment entry updated');
          this.router.navigate(['/accounting/payments', this.entityId]);
        },
        error: (err: any) => {
          this.toaster.error(err?.error?.error?.message ?? 'Failed to update payment');
        },
      });
    } else {
      this.paymentService.create(dto).subscribe({
        next: () => {
          this.toaster.success('Payment entry created');
          this.router.navigate(['/accounting/payments']);
        },
        error: (err: any) => {
          this.toaster.error(err?.error?.error?.message ?? 'Failed to create payment');
        },
      });
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
