import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';

@Component({
  selector: 'app-sr-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewReconciliation' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.postingDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Purpose' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.purpose">
              <option>{{ 'StockCount' | abpLocalization }}</option>
              <option>{{ 'OpeningStock' | abpLocalization }}</option>
              <option>{{ 'Adjustment' | abpLocalization }}</option>
            </select>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Items' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th>{{ 'CurrentQty' | abpLocalization }}</th><th>{{ 'NewQty' | abpLocalization }}</th><th>{{ 'Rate' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (item of form.items; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.itemName" placeholder="Item" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.currentQuantity" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.newQuantity" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.newValuationRate" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.items.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addItem()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/inventory/stock-reconciliations">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class StockReconciliationFormComponent {
  private restService = inject(RestService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  form: any = { postingDate: new Date().toISOString().split('T')[0], purpose: 'Stock Count', items: [{ itemName: '', currentQuantity: 0, newQuantity: 0, newValuationRate: 0 }] };

  addItem() { this.form.items.push({ itemName: '', currentQuantity: 0, newQuantity: 0, newValuationRate: 0 }); }

  save() {
    this.saving = true;
    this.restService.request({ method: 'POST', url: '/api/app/stock-reconciliation', body: this.form }, { apiName: 'Default' })
      .subscribe({ next: () => this.router.navigate(['/inventory/stock-reconciliations']), error: () => { this.saving = false;
  this.isDirty = false; } });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}