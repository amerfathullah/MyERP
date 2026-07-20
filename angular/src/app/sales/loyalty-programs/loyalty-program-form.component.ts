import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe , RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-loyalty-program-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe, AutoValidationDirective],
  template: `
    <abp-page [title]="'NewLoyaltyProgram' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <h6 class="card-title">{{ 'ProgramDetails' | abpLocalization }}</h6>
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'Name' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="name">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'ConversionFactor' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="conversionFactor" step="0.01">
              <small class="text-muted">Points = FLOOR(amount / factor)</small>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'ExpiryDays' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="expiryDurationDays">
              <small class="text-muted">0 = never expires</small>
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <h6 class="card-title mb-0">{{ 'Tiers' | abpLocalization }}</h6>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addTier()">
              <i class="fa fa-plus me-1"></i>{{ 'AddTier' | abpLocalization }}
            </button>
          </div>
          @if (tiers.length === 0) {
            <p class="text-muted">Add at least one tier. The lowest tier must have Min Spent = 0.</p>
          }
          @if (tiers.length > 0) {
          <table class="table table-sm">
            <thead><tr>
              <th>{{ 'TierName' | abpLocalization }}</th>
              <th>Min Spent</th>
              <th>Collection Factor</th>
              <th>Redemption Factor</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (tier of tiers.controls; track $index; let i = $index) {
                <tr [formGroup]="$any(tier)">
                  <td><input type="text" class="form-control form-control-sm" formControlName="tierName"></td>
                  <td><input type="number" class="form-control form-control-sm" formControlName="minSpent" step="100"></td>
                  <td><input type="number" class="form-control form-control-sm" formControlName="collectionFactor" step="0.1"></td>
                  <td><input type="number" class="form-control form-control-sm" formControlName="redemptionFactor" step="0.001"></td>
                  <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeTier(i)"><i class="fa fa-trash"></i></button></td>
                </tr>
              }
            </tbody>
          </table>
          }
        </div></div>

        <div class="d-flex gap-2">
          <button type="submit" class="btn btn-primary" [disabled]="!form.valid || saving">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
          <a class="btn btn-outline-secondary" routerLink="/sales/loyalty-programs">{{ 'Cancel' | abpLocalization }}</a>
        </div>
      </form>
    </abp-page>
  `
})
export class LoyaltyProgramFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private restService = inject(RestService);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  saving = false;
  form = this.fb.group({
    companyId: ['', Validators.required],
    name: ['', Validators.required],
    conversionFactor: [10, [Validators.required, Validators.min(0.01)]],
    expiryDurationDays: [365, Validators.required],
    tiers: this.fb.array([])
  });

  get tiers() { return this.form.get('tiers') as FormArray; }

  ngOnInit() {
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });
    this.addTier(); // Start with one default tier
  }

  addTier() {
    this.tiers.push(this.fb.group({
      tierName: ['', Validators.required],
      minSpent: [this.tiers.length === 0 ? 0 : 1000, Validators.required],
      collectionFactor: [1, Validators.required],
      redemptionFactor: [0.01, Validators.required]
    }));
  }

  removeTier(index: number) { this.tiers.removeAt(index); }

  save() {
    if (!this.form.valid) return;
    this.saving = true;
    this.restService.request<any, void>({ method: 'POST', url: '/api/app/loyalty-program', body: this.form.value }, { apiName: 'Default' }).subscribe({
      next: () => { this.toaster.success('Loyalty program created'); this.router.navigate(['/sales/loyalty-programs']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty && !this.saving; }
}
