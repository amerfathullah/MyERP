import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';

@Component({
  selector: 'app-lcv-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewLandedCostVoucher' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.postingDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'DistributionMethod' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.distributionMethod">
              <option [ngValue]="0">By Quantity</option>
              <option [ngValue]="1">By Amount</option>
              <option [ngValue]="2">Manual</option>
            </select>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Items' | abpLocalization }} (Receipt Items)</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'ReceiptType' | abpLocalization }}</th><th>{{ 'Description' | abpLocalization }}</th><th>{{ 'Quantity' | abpLocalization }}</th><th>{{ 'Amount' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (item of form.items; track $index) {
              <tr>
                <td><select class="form-select form-select-sm" [(ngModel)]="item.receiptType"><option>PurchaseReceipt</option><option>PurchaseInvoice</option></select></td>
                <td><input class="form-control form-control-sm" [(ngModel)]="item.description" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="item.quantity" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="item.amount" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.items.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addItem()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <h6 class="mb-2">{{ 'Charges' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Description' | abpLocalization }}</th><th>{{ 'Amount' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (ch of form.charges; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" [(ngModel)]="ch.description" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="ch.amount" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.charges.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addCharge()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-between align-items-center">
          <span class="fw-bold">{{ 'TotalCharges' | abpLocalization }}: {{ getTotalCharges() | number:'1.2-2' }}</span>
          <div class="d-flex gap-2">
            <a class="btn btn-secondary" routerLink="/inventory/landed-costs">{{ 'Cancel' | abpLocalization }}</a>
            <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          </div>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class LandedCostFormComponent {
  private restService = inject(RestService);
  private router = inject(Router);
  saving = false;
  form: any = { postingDate: new Date().toISOString().split('T')[0], distributionMethod: 1, items: [{ receiptType: 'PurchaseReceipt', description: '', quantity: 0, amount: 0 }], charges: [{ description: '', amount: 0 }] };

  addItem() { this.form.items.push({ receiptType: 'PurchaseReceipt', description: '', quantity: 0, amount: 0 }); }
  addCharge() { this.form.charges.push({ description: '', amount: 0 }); }
  getTotalCharges(): number { return this.form.charges.reduce((s: number, c: any) => s + (c.amount || 0), 0); }

  save() {
    this.saving = true;
    this.restService.request({ method: 'POST', url: '/api/app/landed-cost-voucher', body: this.form }, { apiName: 'Default' })
      .subscribe({ next: () => this.router.navigate(['/inventory/landed-costs']), error: () => { this.saving = false; } });
  }
}
