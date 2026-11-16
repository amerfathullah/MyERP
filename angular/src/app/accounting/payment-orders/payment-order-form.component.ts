import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentOrderService } from '../../proxy/accounting/payment-order.service';
import type { CandidatePaymentEntryDto, CandidatePaymentRequestDto } from '../../proxy/accounting/models';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { BankAccountService } from '../../proxy/accounting/bank-account.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

export interface PaymentOrderCandidateRow {
  id: string;
  label: string;
  partyId: string;
  partyName: string;
  amount: number;
  modeOfPayment: string;
  referenceType: 'PaymentRequest' | 'PaymentEntry';
  selected: boolean;
}

export function mapCandidateRequests(items: CandidatePaymentRequestDto[] | undefined): PaymentOrderCandidateRow[] {
  return (items ?? []).filter(r => !!r.id).map(r => ({
    id: r.id!,
    label: r.referenceNumber ?? r.id!,
    partyId: r.partyId ?? '',
    partyName: r.partyName ?? '',
    amount: r.outstandingAmount ?? r.grandTotal ?? 0,
    modeOfPayment: 'Bank Transfer',
    referenceType: 'PaymentRequest',
    selected: false,
  }));
}

export function mapCandidateEntries(items: CandidatePaymentEntryDto[] | undefined): PaymentOrderCandidateRow[] {
  return (items ?? []).filter(e => !!e.id).map(e => ({
    id: e.id!,
    label: e.entryNumber ?? e.id!,
    partyId: e.partyId ?? '',
    partyName: e.partyName ?? '',
    amount: e.paidAmount ?? e.receivedAmount ?? 0,
    modeOfPayment: e.modeOfPayment ?? 'Bank Transfer',
    referenceType: 'PaymentEntry',
    selected: false,
  }));
}

@Component({
  selector: 'app-payment-order-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::New' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-3">
              <label class="form-label">{{ '::Type' | abpLocalization }}</label>
              <select class="form-select" formControlName="paymentOrderType">
                <option [ngValue]="0">{{ '::PaymentRequest' | abpLocalization }}</option>
                <option [ngValue]="1">{{ '::PaymentEntry' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::PostingDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="postingDate">
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ '::CompanyBankAccount' | abpLocalization }} *</label>
              <select class="form-select" formControlName="companyBankAccountId">
                <option value="">-- {{ '::Select' | abpLocalization }} --</option>
                @for (b of bankAccounts(); track b.id) { <option [value]="b.id">{{ b.accountName }}</option> }
              </select>
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ '::References' | abpLocalization }}</h6>
            <div class="d-flex gap-2">
              <button type="button" class="btn btn-sm btn-outline-info" (click)="fetchCandidates()" [disabled]="loadingCandidates()">
                <i class="fa fa-magnifying-glass me-1"></i>{{ '::FetchCandidates' | abpLocalization }}
              </button>
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="addReference()"><i class="fa fa-plus"></i></button>
            </div>
          </div>
          @if (candidates().length > 0) {
            <div class="border rounded p-2 mb-3 bg-light">
              <div class="d-flex justify-content-between align-items-center mb-2">
                <strong class="small">{{ '::FetchCandidates' | abpLocalization }} ({{ candidates().length }})</strong>
                <button type="button" class="btn btn-sm btn-success" (click)="addSelectedCandidates()">
                  <i class="fa fa-check me-1"></i>{{ '::AddSelected' | abpLocalization }}
                </button>
              </div>
              <div class="table-responsive" style="max-height: 200px; overflow-y: auto;">
                <table class="table table-sm table-bordered bg-white mb-0">
                  <thead class="table-light">
                    <tr>
                      <th style="width: 36px;"></th>
                      <th>{{ '::Document' | abpLocalization }}</th>
                      <th>{{ '::Supplier' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (c of candidates(); track c.id) {
                      <tr>
                        <td class="text-center">
                          <input type="checkbox" class="form-check-input" [checked]="c.selected" (change)="toggleCandidate(c.id)" />
                        </td>
                        <td class="font-monospace small">{{ c.label }}</td>
                        <td class="small">{{ c.partyName || c.partyId }}</td>
                        <td class="text-end font-monospace small">{{ c.amount | number:'1.2-2' }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
          <table class="table table-sm">
            <thead>
              <tr>
                <th>{{ '::Supplier' | abpLocalization }}</th>
                <th>{{ '::ReferenceType' | abpLocalization }}</th>
                <th>{{ '::ReferenceId' | abpLocalization }}</th>
                <th>{{ '::Amount' | abpLocalization }}</th>
                <th>{{ '::ModeOfPayment' | abpLocalization }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody formArrayName="references">
              @for (row of references.controls; track $index; let i = $index) {
                <tr [formGroupName]="i">
                  <td>
                    <select class="form-select form-select-sm" formControlName="supplierId">
                      <option value="">-- {{ '::Select' | abpLocalization }} --</option>
                      @for (s of suppliers(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
                    </select>
                  </td>
                  <td>
                    <select class="form-select form-select-sm" formControlName="referenceType">
                      @if (form.get('paymentOrderType')?.value === 1) {
                        <option value="PaymentEntry">{{ '::PaymentEntry' | abpLocalization }}</option>
                      } @else {
                        <option value="PaymentRequest">{{ '::PaymentRequest' | abpLocalization }}</option>
                      }
                    </select>
                  </td>
                  <td><input type="text" class="form-control form-control-sm" formControlName="referenceId" [placeholder]="'::ReferenceId' | abpLocalization"></td>
                  <td><input type="number" class="form-control form-control-sm" formControlName="amount" min="0"></td>
                  <td>
                    <select class="form-select form-select-sm" formControlName="modeOfPayment">
                      <option value="">-- {{ '::Select' | abpLocalization }} --</option>
                      <option value="Cash">{{ '::Cash' | abpLocalization }}</option>
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
          <a class="btn btn-secondary" routerLink="/accounting/payment-orders">{{ '::Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid || references.length === 0">
            <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
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
  candidates = signal<PaymentOrderCandidateRow[]>([]);
  loadingCandidates = signal(false);

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

    // Sync reference types on payment order type change (per ERPNext PR #62244)
    this.form.get('paymentOrderType')?.valueChanges.subscribe((val) => {
      const expected = Number(val) === 1 ? 'PaymentEntry' : 'PaymentRequest';
      for (let i = 0; i < this.references.length; i++) {
        this.references.at(i).get('referenceType')?.setValue(expected);
      }
    });

    this.addReference();
  }

  addReference(): void {
    const refType = Number(this.form.get('paymentOrderType')?.value) === 1 ? 'PaymentEntry' : 'PaymentRequest';
    this.references.push(this.fb.group({
      supplierId: ['', Validators.required],
      referenceType: [refType],
      referenceId: ['', Validators.required],
      amount: [0, [Validators.required, Validators.min(0.01)]],
      modeOfPayment: [''],
    }));
  }

  removeReference(index: number): void { this.references.removeAt(index); }

  fetchCandidates(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.loadingCandidates.set(true);
    const isPaymentEntry = Number(this.form.get('paymentOrderType')?.value) === 1;
    if (isPaymentEntry) {
      this.service.getCandidatePaymentEntries(companyId).subscribe({
        next: res => {
          this.candidates.set(mapCandidateEntries(res));
          this.loadingCandidates.set(false);
        },
        error: () => this.loadingCandidates.set(false),
      });
    } else {
      this.service.getCandidatePaymentRequests(companyId).subscribe({
        next: res => {
          this.candidates.set(mapCandidateRequests(res));
          this.loadingCandidates.set(false);
        },
        error: () => this.loadingCandidates.set(false),
      });
    }
  }

  toggleCandidate(id: string): void {
    this.candidates.update(rows => rows.map(r => (r.id === id ? { ...r, selected: !r.selected } : r)));
  }

  addSelectedCandidates(): void {
    const chosen = this.candidates().filter(c => c.selected);
    if (chosen.length === 0) return;
    if (this.references.length === 1 && !this.references.at(0).get('referenceId')?.value) {
      this.references.removeAt(0);
    }
    const existingIds = new Set(this.references.controls.map(c => c.get('referenceId')?.value));
    for (const c of chosen) {
      if (existingIds.has(c.id)) continue;
      this.references.push(this.fb.group({
        supplierId: [c.partyId, Validators.required],
        referenceType: [c.referenceType],
        referenceId: [c.id, Validators.required],
        amount: [c.amount, [Validators.required, Validators.min(0.01)]],
        modeOfPayment: [c.modeOfPayment],
      }));
    }
    this.candidates.set([]);
  }

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
