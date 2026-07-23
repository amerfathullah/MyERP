import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { RequestForQuotationService } from '../../proxy/purchasing/request-for-quotation.service';
@Component({
  selector: 'app-rfq-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewRFQ' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'Date' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.transactionDate" (ngModelChange)="isDirty=true" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'Currency' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.currencyCode" (ngModelChange)="isDirty=true" />
          </div>
        </div>
        <div class="mb-3">
          <label class="form-label">{{ 'MessageForSupplier' | abpLocalization }}</label>
          <textarea class="form-control" rows="3" [(ngModel)]="form.messageForSupplier" (ngModelChange)="isDirty=true"></textarea>
        </div>

        <h6>{{ 'Items' | abpLocalization }}</h6>
        <table class="table table-sm mb-3">
          <thead><tr><th>Item</th><th>Qty</th><th>UOM</th><th></th></tr></thead>
          <tbody>
            @for (item of form.items; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" [(ngModel)]="item.description" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="item.qty" /></td>
                <td><input class="form-control form-control-sm" [(ngModel)]="item.uom" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.items.splice($index, 1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="form.items.push({itemId:'',description:'',qty:1,uom:'Unit'})">
          <i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}
        </button>

        <h6>{{ 'Suppliers' | abpLocalization }}</h6>
        <table class="table table-sm mb-3">
          <thead><tr><th>Supplier</th><th>Email</th><th></th></tr></thead>
          <tbody>
            @for (s of form.suppliers; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" [(ngModel)]="s.supplierName" /></td>
                <td><input class="form-control form-control-sm" [(ngModel)]="s.email" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.suppliers.splice($index, 1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="form.suppliers.push({supplierId:'',email:''})">
          <i class="fa fa-plus me-1"></i>{{ 'AddSupplier' | abpLocalization }}
        </button>

        <div class="d-flex justify-content-end gap-2">
          <button class="btn btn-secondary" routerLink="/purchasing/rfq">{{ 'Cancel' | abpLocalization }}</button>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class RfqFormComponent {
  private service = inject(RequestForQuotationService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  form: any = {
    transactionDate: new Date().toISOString().split('T')[0],
    currencyCode: 'MYR',
    messageForSupplier: '',
    items: [{ itemId: '', description: '', qty: 1, uom: 'Unit' }],
    suppliers: [{ supplierId: '', email: '' }],
  };

  save() {
    this.saving = true;
    this.service.create(this.form).subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/purchasing/rfq']); },
      error: () => { this.saving = false; },
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}
