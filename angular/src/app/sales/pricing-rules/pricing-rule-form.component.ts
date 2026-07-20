import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { PricingRuleService } from '../../proxy/sales/pricing-rule.service';

@Component({
  selector: 'app-pricing-rule-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewPricingRule' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'Title' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.title" placeholder="e.g., 10% Off Bulk Orders" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ApplyOn' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.applyOn">
              <option [ngValue]="0">{{ 'ItemCode' | abpLocalization }}</option>
              <option [ngValue]="1">{{ 'ItemGroup' | abpLocalization }}</option>
              <option [ngValue]="2">{{ 'Brand' | abpLocalization }}</option>
              <option [ngValue]="3">{{ 'TransactionTotal' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Priority' | abpLocalization }}</label>
            <input type="number" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.priority" min="1" />
          </div>
        </div>
        <div class="row mb-3">
          <div class="col-md-3">
            <label class="form-label">Rule Type</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.ruleType">
              <option [ngValue]="0">Discount</option>
              <option [ngValue]="1">Rate</option>
              <option [ngValue]="2">{{ 'FreeItem' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Discount' | abpLocalization }} %</label>
            <input type="number" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.discountPercentage" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'MinQty' | abpLocalization }}</label>
            <input type="number" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.minQty" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'MaxQty' | abpLocalization }}</label>
            <input type="number" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.maxQty" />
          </div>
        </div>
        <div class="row mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ 'ValidFrom' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.validFrom" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ValidUntil' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.validUpto" />
          </div>
          <div class="col-md-3">
            <label class="form-label">For</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.applicableFor">
              <option>{{ 'Selling' | abpLocalization }}</option>
              <option>{{ 'Buying' | abpLocalization }}</option>
              <option>{{ 'Both' | abpLocalization }}</option>
            </select>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/sales/pricing-rules">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class PricingRuleFormComponent {
  private service = inject(PricingRuleService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  form: any = { title: '', applyOn: 0, ruleType: 0, priority: 1, discountPercentage: 0, minQty: 0, maxQty: 0, applicableFor: 'Selling' };

  save() {
    this.saving = true;
    this.service.create(this.form)
      .subscribe({ next: () => this.router.navigate(['/sales/pricing-rules']), error: () => { this.saving = false;
  this.isDirty = false; } });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}