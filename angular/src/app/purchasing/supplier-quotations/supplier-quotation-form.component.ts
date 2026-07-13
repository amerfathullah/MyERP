import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';

@Component({
  selector: 'app-supplier-quotation-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewSupplierQuotation' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Supplier' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.supplierName" placeholder="Supplier name" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Date' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.transactionDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'ValidTill' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.validTill" />
          </div>
        </div>

        <h6 class="mb-2">{{ 'Items' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th>{{ 'Quantity' | abpLocalization }}</th><th>{{ 'Rate' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (item of form.items; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" [(ngModel)]="item.itemName" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="item.qty" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="item.rate" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.items.splice($index, 1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="form.items.push({itemName:'',qty:0,rate:0})">
          <i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}
        </button>

        <div class="d-flex justify-content-end gap-2">
          <button class="btn btn-secondary" routerLink="/purchasing/supplier-quotations">{{ 'Cancel' | abpLocalization }}</button>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class SupplierQuotationFormComponent {
  private restService = inject(RestService);
  private router = inject(Router);
  saving = false;
  form: any = { transactionDate: new Date().toISOString().split('T')[0], supplierName: '', items: [{ itemName: '', qty: 0, rate: 0 }] };

  save() {
    this.saving = true;
    this.restService.request({ method: 'POST', url: '/api/app/supplier-quotation', body: this.form }, { apiName: 'Default' })
      .subscribe({ next: () => { this.router.navigate(['/purchasing/supplier-quotations']); }, error: () => { this.saving = false; } });
  }
}
