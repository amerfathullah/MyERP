import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SupplierScorecardService } from '../../proxy/purchasing/supplier-scorecard.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';

@Component({
  selector: 'app-supplier-scorecard-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe, AutoValidationDirective],
  template: `
    <abp-page [title]="'NewScorecard' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <h6 class="card-title">Scorecard Settings</h6>
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'Supplier' | abpLocalization }}</label>
              <select class="form-select" formControlName="supplierId">
                <option value="">Select Supplier...</option>
                @for (s of suppliers; track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'Period' | abpLocalization }}</label>
              <select class="form-select" formControlName="periodType">
                <option [value]="0">Weekly</option>
                <option [value]="1">Monthly</option>
                <option [value]="2">Yearly</option>
              </select>
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <h6 class="card-title mb-0">{{ 'Standing' | abpLocalization }} Bands (must cover 0-100)</h6>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addStanding()">
              <i class="fa fa-plus me-1"></i>Add Band
            </button>
          </div>
          @if (standings.length > 0) {
            <table class="table table-sm">
              <thead><tr><th>Name</th><th>Min</th><th>Max</th><th>Block PO</th><th>Block RFQ</th><th></th></tr></thead>
              <tbody>
                @for (s of standings.controls; track $index; let i = $index) {
                  <tr [formGroup]="$any(s)">
                    <td><input type="text" class="form-control form-control-sm" formControlName="name"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="minScore" style="width:70px"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="maxScore" style="width:70px"></td>
                    <td class="text-center"><input type="checkbox" class="form-check-input" formControlName="preventPos"></td>
                    <td class="text-center"><input type="checkbox" class="form-check-input" formControlName="preventRfqs"></td>
                    <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="standings.removeAt(i)"><i class="fa fa-trash"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <h6 class="card-title mb-0">Criteria (weights must sum to 100%)</h6>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addCriterion()">
              <i class="fa fa-plus me-1"></i>Add Criterion
            </button>
          </div>
          @if (criteria.length > 0) {
            <table class="table table-sm">
              <thead><tr><th>{{ 'Name' | abpLocalization }}</th><th>Weight %</th><th>Max Score</th><th></th></tr></thead>
              <tbody>
                @for (c of criteria.controls; track $index; let i = $index) {
                  <tr [formGroup]="$any(c)">
                    <td><input type="text" class="form-control form-control-sm" formControlName="name"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="weight" style="width:70px"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="maxScore" style="width:70px"></td>
                    <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="criteria.removeAt(i)"><i class="fa fa-trash"></i></button></td>
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
          <a class="btn btn-outline-secondary" routerLink="/purchasing/scorecards">{{ 'Cancel' | abpLocalization }}</a>
        </div>
      </form>
    </abp-page>
  `
})
export class SupplierScorecardFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private scorecardService = inject(SupplierScorecardService);
  private supplierService = inject(SupplierService);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  saving = false;
  suppliers: any[] = [];

  form = this.fb.group({
    supplierId: ['', Validators.required],
    companyId: ['', Validators.required],
    periodType: [1],
    standings: this.fb.array([]),
    criteria: this.fb.array([])
  });

  get standings() { return this.form.get('standings') as FormArray; }
  get criteria() { return this.form.get('criteria') as FormArray; }

  ngOnInit() {
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });

    this.supplierService.getList({ filter: '', sorting: '', skipCount: 0, maxResultCount: 200 }).subscribe({
      next: res => { this.suppliers = res.items ?? []; }
    });

    // Default 3 bands
    this.addStanding('Poor', 0, 40, true, true);
    this.addStanding('Average', 40, 70, false, false);
    this.addStanding('Good', 70, 100, false, false);
  }

  addStanding(name = '', min = 0, max = 100, preventPos = false, preventRfqs = false) {
    this.standings.push(this.fb.group({
      name: [name, Validators.required],
      minScore: [min, Validators.required],
      maxScore: [max, Validators.required],
      preventPos: [preventPos],
      preventRfqs: [preventRfqs],
      warnPos: [false],
      warnRfqs: [false]
    }));
  }

  addCriterion() {
    this.criteria.push(this.fb.group({
      name: ['', Validators.required],
      weight: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
      maxScore: [5, Validators.required]
    }));
  }

  save() {
    if (!this.form.valid) return;
    this.saving = true;
    this.scorecardService.create(this.form.value as any).subscribe({
      next: () => { this.toaster.success('Scorecard created'); this.router.navigate(['/purchasing/scorecards']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty && !this.saving; }
}
