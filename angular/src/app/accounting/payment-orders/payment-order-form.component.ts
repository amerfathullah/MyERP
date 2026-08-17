import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentOrderService } from '../../proxy/accounting/payment-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { BankAccountService } from '../../proxy/accounting/bank-account.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-payment-order-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::New' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-3">
              <label class="form-label">{{ '::Type' | abpLocalization }}</label>
              <select class="form-select" formControlName="paymentOrderType">
                <option [value]="0">{{ '::PaymentRequest' | abpLocalization }}</option>
                <option [value]="1">{{ '::PaymentEntry' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::PostingDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="postingDate">
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ '::CompanyBankAccount' | abpLocalization }} *</label>
              <select class="form-select" formControlName="companyBankAccountId">
                <option value="">-- {{ 'Select' | abpLocalization }} --</option>
                @for (b of bankAccounts(); track b.id) { <option [value]="b.id">{{ b.accountName }}</option> }
              </select>
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ '::References' | abpLocalization }}</h6>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addReference()"><i class="fa fa-plus"></i></button>
          </div>
          <table class="table table-sm">
            <thead>
              <tr>
                <th>{{ '::Supplier' | abpLocalization }}</th>
                <th>{{ '::ReferenceType' | abpLocalization }}</th>
                <th>{{ '::ReferenceId' | abpLocalization }}</th>
                <th>{{ 'Amount' | abpLocalization }}</th>
                <th>{{ '::ModeOfPayment' | abpLocalization }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody formArrayName="references">
              @for (row of references.controls; track $index; let i = $index) {
                <tr [formGroupName]="i">
                  <td>
                    <select class="form-select form-select-sm" formControlName="supplierId">
                      <option value="">-- {{ 'Select' | abpLocalization }} --</option>
                      @for (s of suppliers(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
                    </select>
                  </td>
                  <td>
                    <select class="form-select form-select-sm" formControlName="referenceType">
                      <option value="PaymentRequest">{{ '::PaymentRequest' | abpLocalization }}</option>
                      <option value="PaymentEntry">{{ '::PaymentEntry' | abpLocalization }}</option>
                    </select>
                  </td>
                  <td><input type="text" class="form-control form-control-sm" formControlName="referenceId" [placeholder]="'::ReferenceId' | abpLocalization"></td>
                  <td><input type="number" class="form-control form-control-sm" formControlName="amount" min="0"></td>
                  <td>
                    <select class="form-select form-select-sm" formControlName="modeOfPayment">
                      <option value="">-- {{ 'Select' | abpLocalization }} --</option>
                      <option value="Cash">{{ 'Cash' | abpLocalization }}</option>
                      <option value="Bank Transfer">{{ '::BankTransfer' | abpLocalization }}</option>
                      <option value="Cheque">{{ '::Cheque' | abpLocalization }}</option>
                    </select>
                  </td>
                  <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeReference(i)"><i class="fa fa-trash"></i></button></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/accounting/payment-orders">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid || references.length === 0">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class PaymentOrderFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(PaymentOrderService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private supplierService = inject(SupplierService);
  private bankAccountService = inject(BankAccountService);

  suppliers = signal<any[]>([]);
  bankAccounts = signal<any[]>([]);

  form = this.fb.group({
    paymentOrderType: [0],
    postingDate: [new Date().toISOString().substring(0, 10), Validators.required],
    companyBankAccountId: ['', Validators.required],
    references: this.fb.array([]),
  });

  get references(): FormArray { return this.form.get('references') as FormArray; }

  ngOnInit(): void {
    this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any)
      .subscribe({ next: (r) => this.suppliers.set(r.items ?? []), error: () => {} });
    this.bankAccountService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe({ next: (r) => this.bankAccounts.set(r.items ?? []), error: () => {} });
    this.addReference();
  }

  addReference(): void {
    this.references.push(this.fb.group({
      supplierId: ['', Validators.required],
      referenceType: ['PaymentRequest'],
      referenceId: ['', Validators.required],
      amount: [0, [Validators.required, Validators.min(0.01)]],
      modeOfPayment: [''],
    }));
  }

  removeReference(index: number): void { this.references.removeAt(index); }

  save(): void {
    if (this.form.invalid || this.references.length === 0) return;
    const val = this.form.getRawValue();
    const companyId = this.companyContext.currentCompanyId();
    const bankAccountId = val.companyBankAccountId!;
    this.service.create({
      companyId: companyId!,
      paymentOrderType: Number(val.paymentOrderType),
      postingDate: val.postingDate!,
      companyBankAccountId: bankAccountId,
      references: (val.references ?? []).map((r: any) => ({
        referenceType: r.referenceType,
        referenceId: r.referenceId,
        amount: Number(r.amount),
        supplierId: r.supplierId || undefined,
        modeOfPayment: r.modeOfPayment || undefined,
        bankAccountId,
      })),
    }).subscribe({
      next: (created) => {
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/accounting/payment-orders', created.id]);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }
}
