import { Component, inject } from '@angular/core';
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
export class PaymentEntryFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);

  form = this.fb.group({
    paymentType: ['Receive', Validators.required],
    paymentDate: [new Date(), Validators.required],
    partyType: ['Customer', Validators.required],
    partyName: ['', Validators.required],
    paidFromAccount: ['', Validators.required],
    paidToAccount: ['', Validators.required],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    reference: [''],
    remarks: [''],
  });

  save(): void {
    if (this.form.invalid) return;
    // TODO: Call PaymentEntryAppService.create()
    console.log('Saving payment entry:', this.form.getRawValue());
  }

  cancel(): void {
    this.router.navigate(['/accounting/accounts']);
  }
}
