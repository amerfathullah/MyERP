import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { BlanketOrderService } from '../../proxy/sales/blanket-order.service';

@Component({
  selector: 'app-blanket-order-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewBlanketOrder' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Type' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.orderType">
              <option value="Selling">{{ 'Selling' | abpLocalization }}</option>
              <option value="Buying">{{ 'Buying' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'ValidFrom' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.fromDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'ValidUntil' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.toDate" />
          </div>
        </div>
        <div class="mb-3">
          <label class="form-label">{{ 'Party' | abpLocalization }}</label>
          <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.partyName" placeholder="Customer / Supplier name" />
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
          <button class="btn btn-secondary" routerLink="/sales/blanket-orders">{{ 'Cancel' | abpLocalization }}</button>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class BlanketOrderFormComponent {
  private service = inject(BlanketOrderService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  form: any = { orderType: 'Selling', fromDate: '', toDate: '', partyName: '', items: [{ itemName: '', qty: 0, rate: 0 }] };

  save() {
    this.saving = true;
    this.service.create(this.form)
      .subscribe({ next: () => { this.router.navigate(['/sales/blanket-orders']); }, error: () => { this.saving = false;
  this.isDirty = false; } });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}