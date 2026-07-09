import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';
import { AccountService } from '../../proxy/accounting/account.service';
import type { AccountDto, CreatePaymentEntryDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-payment-entry-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule],
  templateUrl: './payment-entry-form.component.html',
  styleUrls: ['./payment-entry-form.component.scss'],
})
export class PaymentEntryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private paymentService = inject(PaymentEntryService);
  private accountService = inject(AccountService);
  private toaster = inject(ToasterService);

  accounts = signal<AccountDto[]>([]);

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
  });

  ngOnInit(): void {
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe((res) => this.accounts.set(res.items ?? []));
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
