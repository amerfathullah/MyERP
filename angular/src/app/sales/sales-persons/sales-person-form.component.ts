import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe , RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-sales-person-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe, AutoValidationDirective],
  template: `
    <abp-page [title]="'NewSalesPerson' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'Name' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="name">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'CommissionRate' | abpLocalization }} (%)</label>
              <input type="number" class="form-control" formControlName="commissionRate" min="0" max="100" step="0.5">
            </div>
            <div class="col-md-3">
              <label class="form-label">Parent Sales Person</label>
              <select class="form-select" formControlName="parentSalesPersonId">
                <option value="">None (root)</option>
                @for (sp of existingPersons; track sp.id) {
                  <option [value]="sp.id">{{ sp.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'Type' | abpLocalization }}</label>
              <div class="form-check mt-2">
                <input type="checkbox" class="form-check-input" formControlName="isGroup" id="isGroup">
                <label class="form-check-label" for="isGroup">Is Group</label>
              </div>
            </div>
          </div>
        </div></div>

        <div class="d-flex gap-2">
          <button type="submit" class="btn btn-primary" [disabled]="!form.valid || saving">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
          <a class="btn btn-outline-secondary" routerLink="/sales/sales-persons">{{ 'Cancel' | abpLocalization }}</a>
        </div>
      </form>
    </abp-page>
  `
})
export class SalesPersonFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private restService = inject(RestService);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  saving = false;
  existingPersons: any[] = [];

  form = this.fb.group({
    name: ['', Validators.required],
    commissionRate: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
    parentSalesPersonId: [''],
    isGroup: [false]
  });

  ngOnInit() {
    this.restService.request<any, any>({ method: 'GET', url: '/api/app/sales-person/tree' }, { apiName: 'Default' }).subscribe({
      next: res => { this.existingPersons = res ?? []; }
    });
  }

  save() {
    if (!this.form.valid) return;
    this.saving = true;
    const dto = { ...this.form.value };
    if (!dto.parentSalesPersonId) dto.parentSalesPersonId = null;
    this.restService.request<any, void>({ method: 'POST', url: '/api/app/sales-person', body: dto }, { apiName: 'Default' }).subscribe({
      next: () => { this.toaster.success('Sales person created'); this.router.navigate(['/sales/sales-persons']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty && !this.saving; }
}
