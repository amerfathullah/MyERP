import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { UnreconcilePaymentService } from '../../proxy/accounting/unreconcile-payment.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-unreconcile-payment-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::New' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <p class="text-muted">{{ '::UnreconcilePaymentHint' | abpLocalization }}</p>
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ '::VoucherType' | abpLocalization }} *</label>
              <select class="form-select" formControlName="voucherType">
                <option [value]="0">{{ '::PaymentEntry' | abpLocalization }}</option>
                <option [value]="1">{{ '::JournalEntry' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-8">
              <label class="form-label">{{ '::VoucherId' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="voucherId" [placeholder]="'::VoucherIdHint' | abpLocalization">
            </div>
          </div>
        </div></div>
        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/accounting/unreconcile-payments">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-search me-1"></i>{{ '::LoadAllocations' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class UnreconcilePaymentFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(UnreconcilePaymentService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  form = this.fb.group({
    voucherType: [0],
    voucherId: ['', Validators.required],
  });

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const companyId = this.companyContext.currentCompanyId();
    this.service.create({
      companyId: companyId!,
      voucherType: Number(val.voucherType),
      voucherId: val.voucherId!,
    }).subscribe({
      next: (created) => this.router.navigate(['/accounting/unreconcile-payments', created.id]),
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
