import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe , RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-shipping-rule-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe, AutoValidationDirective],
  template: `
    <abp-page [title]="'NewShippingRule' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <h6 class="card-title">{{ 'RuleDetails' | abpLocalization }}</h6>
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'Label' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="label">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'Type' | abpLocalization }}</label>
              <select class="form-select" formControlName="ruleType">
                <option [value]="0">{{ 'Selling' | abpLocalization }}</option>
                <option [value]="1">{{ 'Buying' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'Mode' | abpLocalization }}</label>
              <select class="form-select" formControlName="calculationMode">
                <option [value]="0">Fixed</option>
                <option [value]="1">Based on Net Total</option>
                <option [value]="2">Based on Net Weight</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'FixedAmount' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="fixedAmount" step="0.01">
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <h6 class="card-title mb-0">Conditions (for tiered modes)</h6>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addCondition()">
              <i class="fa fa-plus me-1"></i>Add Condition
            </button>
          </div>
          @if (conditions.length > 0) {
            <table class="table table-sm">
              <thead><tr><th>From</th><th>To (0=catch-all)</th><th>{{ 'Amount' | abpLocalization }}</th><th></th></tr></thead>
              <tbody>
                @for (cond of conditions.controls; track $index; let i = $index) {
                  <tr [formGroup]="$any(cond)">
                    <td><input type="number" class="form-control form-control-sm" formControlName="fromValue"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="toValue"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="shippingAmount" step="0.01"></td>
                    <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="conditions.removeAt(i)"><i class="fa fa-trash"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <h6 class="card-title">{{ 'Countries' | abpLocalization }} (leave empty for global)</h6>
          <div class="d-flex gap-2 align-items-center">
            <input type="text" class="form-control form-control-sm" style="max-width:100px" placeholder="e.g. MY" #countryInput>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addCountry(countryInput)">Add</button>
          </div>
          @if (countries.length > 0) {
            <div class="mt-2 d-flex gap-1 flex-wrap">
              @for (c of countries; track $index; let i = $index) {
                <span class="badge bg-light text-dark">{{ c }} <i class="fa fa-times ms-1 cursor-pointer" (click)="countries.splice(i,1)"></i></span>
              }
            </div>
          }
        </div></div>

        <div class="d-flex gap-2">
          <button type="submit" class="btn btn-primary" [disabled]="!form.valid || saving">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
          <a class="btn btn-outline-secondary" routerLink="/sales/shipping-rules">{{ 'Cancel' | abpLocalization }}</a>
        </div>
      </form>
    </abp-page>
  `
})
export class ShippingRuleFormComponent {
  private fb = inject(FormBuilder);
  private restService = inject(RestService);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  saving = false;
  countries: string[] = [];

  form = this.fb.group({
    label: ['', Validators.required],
    companyId: ['' as any],
    accountId: ['' as any, Validators.required],
    ruleType: [0],
    calculationMode: [0],
    fixedAmount: [0],
    conditions: this.fb.array([])
  });

  get conditions() { return this.form.get('conditions') as FormArray; }

  addCondition() {
    this.conditions.push(this.fb.group({
      fromValue: [0, Validators.required],
      toValue: [0],
      shippingAmount: [0, Validators.required]
    }));
  }

  addCountry(input: HTMLInputElement) {
    const val = input.value.trim().toUpperCase();
    if (val && val.length <= 3 && !this.countries.includes(val)) {
      this.countries.push(val);
      input.value = '';
    }
  }

  save() {
    if (!this.form.valid) return;
    this.saving = true;
    const dto = { ...this.form.value, countries: this.countries, isEnabled: true };
    this.restService.request<any, void>({ method: 'POST', url: '/api/app/shipping-rule', body: dto }, { apiName: 'Default' }).subscribe({
      next: () => { this.toaster.success('Shipping rule created'); this.router.navigate(['/sales/shipping-rules']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty && !this.saving; }
}
