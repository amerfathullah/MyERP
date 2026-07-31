import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PosProfileService } from './pos-profile.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-pos-profile-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">
          <i class="bi bi-shop me-2"></i>
          {{ (isEditMode ? 'MyERP::EditPosProfile' : 'MyERP::NewPosProfile') | abpLocalization }}
        </h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::ProfileName' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="profileName" [placeholder]="'::Placeholder:ProfileName' | abpLocalization" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Warehouse' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="warehouseId" [placeholder]="'::Placeholder:WarehouseId' | abpLocalization" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Currency' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="currencyCode" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::InvoiceType' | abpLocalization }}</label>
              <select class="form-select" formControlName="invoiceType">
                <option value="POS Invoice">POS Invoice</option>
                <option value="Sales Invoice">Sales Invoice</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::WriteOffLimit' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="writeOffLimit" step="0.01" />
            </div>
            <div class="col-md-4">
              <div class="form-check mt-4">
                <input type="checkbox" class="form-check-input" formControlName="validateStock" id="validateStock" />
                <label class="form-check-label" for="validateStock">{{ 'MyERP::ValidateStock' | abpLocalization }}</label>
              </div>
              <div class="form-check">
                <input type="checkbox" class="form-check-input" formControlName="postChangeGlEntries" id="postChange" />
                <label class="form-check-label" for="postChange">{{ 'MyERP::PostChangeGl' | abpLocalization }}</label>
              </div>
            </div>
          </div>

          <!-- Payment Methods -->
          <h6 class="mt-4 mb-2">{{ 'MyERP::PaymentModes' | abpLocalization }}</h6>
          <table class="table table-sm table-bordered">
            <thead class="table-light">
              <tr>
                <th>{{ 'MyERP::ModeOfPayment' | abpLocalization }}</th>
                <th>{{ 'MyERP::GLAccount' | abpLocalization }}</th>
                <th class="text-center">{{ 'MyERP::Default' | abpLocalization }}</th>
                <th style="width: 50px;"></th>
              </tr>
            </thead>
            <tbody formArrayName="paymentMethods">
              @for (pm of paymentMethods.controls; track $index) {
                <tr [formGroupName]="$index">
                  <td><input type="text" class="form-control form-control-sm" formControlName="modeOfPaymentId" [placeholder]="'::Placeholder:ModeOfPaymentId' | abpLocalization" /></td>
                  <td><input type="text" class="form-control form-control-sm" formControlName="accountId" [placeholder]="'::Placeholder:AccountId' | abpLocalization" /></td>
                  <td class="text-center">
                    <input type="checkbox" class="form-check-input" formControlName="isDefault" />
                  </td>
                  <td>
                    <button type="button" class="btn btn-sm btn-outline-danger" (click)="removePaymentMethod($index)">
                      <i class="bi bi-trash"></i>
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
          <button type="button" class="btn btn-sm btn-outline-secondary mb-3" (click)="addPaymentMethod()">
            <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::AddRow' | abpLocalization }}
          </button>

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
export class PosProfileFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(PosProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  form!: FormGroup;
  saving = false;
  isEditMode = false;
  editId: string | null = null;

  get paymentMethods(): FormArray {
    return this.form.get('paymentMethods') as FormArray;
  }

  ngOnInit() {
    this.form = this.fb.group({
      profileName: ['', Validators.required],
      warehouseId: ['', Validators.required],
      currencyCode: ['MYR'],
      invoiceType: ['POS Invoice'],
      validateStock: [true],
      writeOffLimit: [0],
      postChangeGlEntries: [false],
      paymentMethods: this.fb.array([]),
    });

    this.editId = this.route.snapshot.paramMap.get('id');
    if (this.editId) {
      this.isEditMode = true;
      this.service.get(this.editId).subscribe({
        next: (p) => {
          this.form.patchValue({
            profileName: p.profileName,
            warehouseId: p.warehouseId,
            currencyCode: p.currencyCode,
            invoiceType: p.invoiceType,
            validateStock: p.validateStock,
            writeOffLimit: p.writeOffLimit,
            postChangeGlEntries: p.postChangeGlEntries,
          });
          (p.paymentMethods ?? []).forEach(pm => this.addPaymentMethod(pm));
        },
      });
    } else {
      this.addPaymentMethod(); // Start with one empty row
    }
  }

  addPaymentMethod(data?: any) {
    this.paymentMethods.push(this.fb.group({
      modeOfPaymentId: [data?.modeOfPaymentId || '', Validators.required],
      accountId: [data?.accountId || '', Validators.required],
      isDefault: [data?.isDefault || false],
    }));
  }

  removePaymentMethod(idx: number) {
    this.paymentMethods.removeAt(idx);
  }

  save() {
    if (!this.form.valid) return;
    this.saving = true;

    const payload = {
      ...this.form.value,
      companyId: this.companyContext.currentCompanyId(),
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
