import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatRadioModule } from '@angular/material/radio';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';
import { AccountService } from '../../proxy/accounting/account.service';
import type { AccountDto, CreatePaymentEntryDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-payment-entry-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule,
    MatCardModule, MatFormFieldModule, MatInputModule,
    MatDatepickerModule, MatNativeDateModule, MatButtonModule,
    MatIconModule, MatSelectModule, MatRadioModule,
  ],
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
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    paidAmount: [0, [Validators.required, Validators.min(0.01)]],
    paidFromAccountId: ['', Validators.required],
    paidToAccountId: ['', Validators.required],
    modeOfPayment: [''],
    partyType: ['Customer'],
    partyId: [''],
    referenceNumber: [''],
    notes: [''],
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
