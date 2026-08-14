import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BankGuaranteeService } from '../../proxy/accounting/bank-guarantee.service';
import { BankGuaranteeType } from '../../proxy/accounting/bank-guarantee-type.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-bank-guarantee-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">
          <i class="bi bi-shield-check me-2"></i>
          {{ (isEditMode ? 'MyERP::EditBankGuarantee' : 'MyERP::NewBankGuarantee') | abpLocalization }}
        </h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <!-- General Info -->
          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::BankGuaranteeType' | abpLocalization }} *</label>
              <select class="form-select" formControlName="bgType">
                <option [ngValue]="BankGuaranteeType.Receiving">Receiving</option>
                <option [ngValue]="BankGuaranteeType.Providing">Providing</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::BankGuaranteeNumber' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="bankGuaranteeNumber" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::NameOfBeneficiary' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="nameOfBeneficiary" />
            </div>
          </div>

          <!-- Financial & Dates -->
          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Amount' | abpLocalization }} *</label>
              <input type="number" step="0.01" class="form-control" formControlName="amount" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::StartDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="startDate" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::ValidityInDays' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="validityDays" />
            </div>
          </div>

          <!-- Bank Account Info -->
          <h6 class="border-bottom pb-2 mt-4 mb-3 text-muted">
            <i class="bi bi-bank me-2"></i>{{ 'MyERP::BankAccountInfo' | abpLocalization }}
          </h6>
          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Bank' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="bank" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::BankAccountNo' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="bankAccountNumber" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Iban' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="iban" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::BranchCode' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="branchCode" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::SwiftNumber' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="swiftNumber" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::FixedDepositNumber' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="fixedDepositNumber" />
            </div>
          </div>

          <!-- Additional Info -->
          <h6 class="border-bottom pb-2 mt-4 mb-3 text-muted">
            <i class="bi bi-info-circle me-2"></i>{{ 'MyERP::OtherDetails' | abpLocalization }}
          </h6>
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::MarginMoney' | abpLocalization }}</label>
              <input type="number" step="0.01" class="form-control" formControlName="marginMoney" />
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::ChargesIncurred' | abpLocalization }}</label>
              <input type="number" step="0.01" class="form-control" formControlName="charges" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-12">
              <label class="form-label">{{ 'MyERP::ClausesAndConditions' | abpLocalization }}</label>
              <textarea class="form-control" rows="3" formControlName="clausesAndConditions"></textarea>
            </div>
          </div>

          <div class="d-flex justify-content-end gap-2 mt-4">
            <a routerLink=".." class="btn btn-secondary btn-sm">
              {{ 'MyERP::Cancel' | abpLocalization }}
            </a>
            <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || isSaving">
              @if (isSaving) {
                <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
              }
              {{ 'MyERP::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class BankGuaranteeFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(BankGuaranteeService);
  private readonly companyContext = inject(CompanyContextService);
  private readonly toaster = inject(ToasterService);

  protected readonly BankGuaranteeType = BankGuaranteeType;

  id?: string;
  isEditMode = false;
  isSaving = false;

  form: FormGroup = this.fb.group({
    companyId: ['', Validators.required],
    bgType: [BankGuaranteeType.Receiving, Validators.required],
    bankGuaranteeNumber: ['', Validators.required],
    nameOfBeneficiary: ['', Validators.required],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    startDate: [new Date().toISOString().substring(0, 10), Validators.required],
    validityDays: [90, Validators.min(0)],
    bank: ['', Validators.required],
    bankAccountNumber: [''],
    iban: [''],
    branchCode: [''],
    swiftNumber: [''],
    fixedDepositNumber: [''],
    marginMoney: [0],
    charges: [0],
    clausesAndConditions: [''],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.params['id'];
    this.isEditMode = !!this.id;

    const currentCompanyId = this.companyContext.selectedCompanyId();
    if (currentCompanyId) {
      this.form.patchValue({ companyId: currentCompanyId });
    }

    if (this.isEditMode && this.id) {
      this.service.get(this.id).subscribe((bg) => {
        this.form.patchValue({
          companyId: bg.companyId,
          bgType: bg.bgType,
          bankGuaranteeNumber: bg.bankGuaranteeNumber,
          nameOfBeneficiary: bg.nameOfBeneficiary,
          amount: bg.amount,
          startDate: bg.startDate ? bg.startDate.substring(0, 10) : '',
          validityDays: bg.validityDays,
          bank: bg.bank,
          bankAccountNumber: bg.bankAccountNumber,
          iban: bg.iban,
          branchCode: bg.branchCode,
          swiftNumber: bg.swiftNumber,
          fixedDepositNumber: bg.fixedDepositNumber,
          marginMoney: bg.marginMoney,
          charges: bg.charges,
          clausesAndConditions: bg.clausesAndConditions,
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;

    this.isSaving = true;
    const value = this.form.value;

    const request$ = this.isEditMode && this.id
      ? this.service.update(this.id, value)
      : this.service.create(value);

    request$.subscribe({
      next: () => {
        this.isSaving = false;
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['..'], { relativeTo: this.route });
      },
      error: (err) => {
        this.isSaving = false;
        this.toaster.error(err?.error?.error?.message ?? 'Save failed');
      }
    });
  }
}
