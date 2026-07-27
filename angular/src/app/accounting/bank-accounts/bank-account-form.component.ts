import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BankAccountService } from '../../proxy/accounting/bank-account.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-bank-account-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">
          <i class="bi bi-bank me-2"></i>
          {{ (isEditMode ? 'MyERP::EditBankAccount' : 'MyERP::NewBankAccount') | abpLocalization }}
        </h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::AccountName' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="accountName"
                placeholder="e.g., CIMB Current Account" />
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::BankName' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="bankName"
                placeholder="e.g., CIMB Bank" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::BankAccountNo' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="bankAccountNo" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Iban' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="iban" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::SwiftCode' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="swiftCode" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::BranchCode' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="branchCode" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Currency' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="currencyCode" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::GLAccount' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="accountId"
                placeholder="GL Account ID" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <div class="form-check mt-3">
                <input type="checkbox" class="form-check-input" formControlName="isCompanyAccount" id="isCompanyAccount" />
                <label class="form-check-label" for="isCompanyAccount">
                  {{ 'MyERP::IsCompanyAccount' | abpLocalization }}
                </label>
              </div>
            </div>
            <div class="col-md-4">
              <div class="form-check mt-3">
                <input type="checkbox" class="form-check-input" formControlName="isCreditCard" id="isCreditCard" />
                <label class="form-check-label" for="isCreditCard">
                  {{ 'MyERP::CreditCard' | abpLocalization }}
                </label>
              </div>
            </div>
          </div>

          @if (!form.value.isCompanyAccount) {
            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::PartyType' | abpLocalization }}</label>
                <select class="form-select" formControlName="partyType">
                  <option value="">—</option>
                  <option value="Customer">Customer</option>
                  <option value="Supplier">Supplier</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::Party' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="partyId" placeholder="Party ID" />
              </div>
            </div>
          }

          <div class="d-flex justify-content-end gap-2 mt-4">
            <a routerLink=".." class="btn btn-secondary">{{ 'MyERP::Cancel' | abpLocalization }}</a>
            <button type="submit" class="btn btn-primary" [disabled]="!form.valid || saving">
              {{ 'MyERP::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
})
export class BankAccountFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(BankAccountService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  form!: FormGroup;
  saving = false;
  isEditMode = false;
  editId: string | null = null;

  ngOnInit() {
    this.form = this.fb.group({
      accountName: ['', Validators.required],
      bankName: ['', Validators.required],
      bankAccountNo: [''],
      iban: [''],
      swiftCode: [''],
      branchCode: [''],
      currencyCode: ['MYR', Validators.required],
      accountId: ['', Validators.required],
      isCompanyAccount: [true],
      isCreditCard: [false],
      partyType: [''],
      partyId: [null],
    });

    this.editId = this.route.snapshot.paramMap.get('id');
    if (this.editId) {
      this.isEditMode = true;
      this.service.get(this.editId).subscribe({
        next: (ba) => {
          this.form.patchValue({
            accountName: ba.accountName,
            bankName: ba.bankName,
            bankAccountNo: ba.bankAccountNo,
            iban: ba.iban,
            swiftCode: ba.swiftCode,
            branchCode: ba.branchCode,
            currencyCode: ba.currencyCode,
            accountId: ba.accountId,
            isCompanyAccount: ba.isCompanyAccount,
            isCreditCard: ba.isCreditCard,
            partyType: ba.partyType || '',
            partyId: ba.partyId,
          });
        },
      });
    }
  }

  save() {
    if (!this.form.valid) return;
    this.saving = true;

    const payload = {
      ...this.form.value,
      companyId: this.companyContext.currentCompanyId,
    };

    const action$ = this.isEditMode
      ? this.service.update(this.editId!, payload)
      : this.service.create(payload);

    action$.subscribe({
      next: () => {
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['..'], { relativeTo: this.route });
      },
      error: () => { this.saving = false; },
    });
  }
}
