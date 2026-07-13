import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';
import { AccountService } from '../../proxy/accounting/account.service';
import type { AccountDto, CreatePaymentEntryDto } from '../../proxy/accounting/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-payment-entry-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, AutoValidationDirective],
  templateUrl: './payment-entry-form.component.html',
  styleUrls: ['./payment-entry-form.component.scss'],
})
export class PaymentEntryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private paymentService = inject(PaymentEntryService);
  private accountService = inject(AccountService);
  private toaster = inject(ToasterService);

  accounts = signal<AccountDto[]>([]);
  linkedDocLabel = signal('');

  form = this.fb.group({
    companyId: ['', Validators.required],
    paymentType: ['Receive', Validators.required],
    paymentDate: [new Date().toISOString().split('T')[0], Validators.required],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    paidFromAccount: ['', Validators.required],
    paidToAccount: ['', Validators.required],
    modeOfPayment: [''],
    partyType: ['Customer'],
    partyName: [''],
    reference: [''],
    remarks: [''],
    againstInvoiceId: [''],
    againstOrderId: [''],
    againstOrderType: [''],
  });

  ngOnInit(): void {
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe((res) => this.accounts.set(res.items ?? []));

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
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const dto: CreatePaymentEntryDto = this.form.getRawValue() as any;
    this.paymentService.create(dto).subscribe({
      next: () => {
        this.toaster.success('Payment entry created');
        this.router.navigate(['/accounting/payments']);
      },
      error: (err) => {
        this.toaster.error(err?.error?.error?.message ?? 'Failed to create payment');
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/accounting/payments']);
  }
}
