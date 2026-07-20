import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { SupplierQuotationService } from '../../proxy/purchasing/supplier-quotation.service';

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
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.supplierId">
              <option value="">Select supplier...</option>
              @for (s of suppliers(); track s.id) {
                <option [value]="s.id">{{ s.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Date' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.transactionDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'ValidTill' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.validTill" />
          </div>
        </div>

        <h6 class="mb-2">{{ 'Items' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th>{{ 'Quantity' | abpLocalization }}</th><th>{{ 'Rate' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (item of form.items; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.itemName" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.qty" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.rate" /></td>
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
export class SupplierQuotationFormComponent implements OnInit {
  private supplierService = inject(SupplierService);
  private sqService = inject(SupplierQuotationService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  suppliers = signal<any[]>([]);
  form: any = { transactionDate: new Date().toISOString().split('T')[0], supplierId: '', items: [{ itemName: '', qty: 0, rate: 0 }] };

  ngOnInit() {
    this.supplierService.getList({ filter: '', sorting: '', skipCount: 0, maxResultCount: 200 } as any).subscribe(
      res => this.suppliers.set(res.items ?? [])
    );
  }

  save() {
    this.saving = true;
    this.sqService.create(this.form)
      .subscribe({ next: () => { this.router.navigate(['/purchasing/supplier-quotations']); }, error: () => { this.saving = false;
  this.isDirty = false; } });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}
