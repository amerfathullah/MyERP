import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';

@Component({
  selector: 'app-subscription-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewSubscription' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Party' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.partyName" placeholder="Customer name" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'BillingInterval' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.billingInterval">
              <option>Monthly</option>
              <option>Quarterly</option>
              <option>Half-Yearly</option>
              <option>Yearly</option>
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'StartDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.startDate" />
          </div>
        </div>

        <h6 class="mb-2">{{ 'Plans' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th>{{ 'Quantity' | abpLocalization }}</th><th>{{ 'Rate' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (p of form.plans; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" [(ngModel)]="p.itemName" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="p.qty" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="p.rate" /></td>
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
  private restService = inject(RestService);
  private router = inject(Router);
  saving = false;
  form: any = { startDate: new Date().toISOString().split('T')[0], billingInterval: 'Monthly', partyName: '', partyType: 'Customer', plans: [{ itemName: '', qty: 1, rate: 0 }] };

  getTotal(): number { return this.form.plans.reduce((s: number, p: any) => s + (p.qty || 0) * (p.rate || 0), 0); }

  save() {
    this.saving = true;
    this.restService.request({ method: 'POST', url: '/api/app/subscription', body: this.form }, { apiName: 'Default' })
      .subscribe({ next: () => this.router.navigate(['/sales/subscriptions']), error: () => { this.saving = false; } });
  }
}
