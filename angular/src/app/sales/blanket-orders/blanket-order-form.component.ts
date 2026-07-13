import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';

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
            <select class="form-select" [(ngModel)]="form.orderType">
              <option value="Selling">Selling</option>
              <option value="Buying">Buying</option>
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'ValidFrom' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.fromDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'ValidUntil' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.toDate" />
          </div>
        </div>
        <div class="mb-3">
          <label class="form-label">{{ 'Party' | abpLocalization }}</label>
          <input class="form-control" [(ngModel)]="form.partyName" placeholder="Customer / Supplier name" />
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
          <button class="btn btn-secondary" routerLink="/sales/blanket-orders">{{ 'Cancel' | abpLocalization }}</button>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class BlanketOrderFormComponent {
  private restService = inject(RestService);
  private router = inject(Router);
  saving = false;
  form: any = { orderType: 'Selling', fromDate: '', toDate: '', partyName: '', items: [{ itemName: '', qty: 0, rate: 0 }] };

  save() {
    this.saving = true;
    this.restService.request({ method: 'POST', url: '/api/app/blanket-order', body: this.form }, { apiName: 'Default' })
      .subscribe({ next: () => { this.router.navigate(['/sales/blanket-orders']); }, error: () => { this.saving = false; } });
  }
}
