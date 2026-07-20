import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SubscriptionService } from '../../proxy/sales/subscription.service';

@Component({
  selector: 'app-subscription-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewSubscription' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Party' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.partyName" placeholder="Customer name" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'BillingInterval' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.billingInterval">
              <option>{{ 'Monthly' | abpLocalization }}</option>
              <option>{{ 'Quarterly' | abpLocalization }}</option>
              <option>Half-Yearly</option>
              <option>{{ 'Yearly' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'StartDate' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.startDate" />
          </div>
        </div>

        <h6 class="mb-2">{{ 'Plans' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th>{{ 'Quantity' | abpLocalization }}</th><th>{{ 'Rate' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (p of form.plans; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="p.itemName" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="p.qty" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="p.rate" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.plans.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="form.plans.push({itemName:'',qty:1,rate:0})"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-between">
          <span class="fw-bold">{{ 'Total' | abpLocalization }}: {{ getTotal() | number:'1.2-2' }} / {{ form.billingInterval }}</span>
          <div class="d-flex gap-2">
            <a class="btn btn-secondary" routerLink="/sales/subscriptions">{{ 'Cancel' | abpLocalization }}</a>
            <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          </div>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class SubscriptionFormComponent {
  private service = inject(SubscriptionService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  form: any = { startDate: new Date().toISOString().split('T')[0], billingInterval: 'Monthly', partyName: '', partyType: 'Customer', plans: [{ itemName: '', qty: 1, rate: 0 }] };

  getTotal(): number { return this.form.plans.reduce((s: number, p: any) => s + (p.qty || 0) * (p.rate || 0), 0); }

  save() {
    this.saving = true;
    this.service.create(this.form)
      .subscribe({ next: () => this.router.navigate(['/sales/subscriptions']), error: () => { this.saving = false;
  this.isDirty = false; } });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}