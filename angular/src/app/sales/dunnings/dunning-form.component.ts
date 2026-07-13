import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';

@Component({
  selector: 'app-dunning-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewDunning' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Customer' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.customerName" placeholder="Customer name" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Date' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.postingDate" />
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'Level' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.dunningLevel" min="1" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Fee' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.dunningFee" />
          </div>
        </div>

        <h6 class="mb-2">Overdue Invoices</h6>
        <table class="table table-sm">
          <thead><tr><th>Invoice</th><th>{{ 'DueDate' | abpLocalization }}</th><th>{{ 'Outstanding' | abpLocalization }}</th><th>Overdue Days</th><th></th></tr></thead>
          <tbody>
            @for (p of form.overduePayments; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" [(ngModel)]="p.invoiceRef" placeholder="INV-xxx" /></td>
                <td><input type="date" class="form-control form-control-sm" [(ngModel)]="p.dueDate" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="p.outstandingAmount" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="p.overdueDays" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.overduePayments.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addPayment()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-between">
          <span class="fw-bold text-danger">{{ 'Total' | abpLocalization }} {{ 'Outstanding' | abpLocalization }}: {{ getTotalOutstanding() | number:'1.2-2' }}</span>
          <div class="d-flex gap-2">
            <a class="btn btn-secondary" routerLink="/sales/dunnings">{{ 'Cancel' | abpLocalization }}</a>
            <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          </div>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class DunningFormComponent {
  private restService = inject(RestService);
  private router = inject(Router);
  saving = false;
  form: any = {
    customerName: '', postingDate: new Date().toISOString().split('T')[0],
    dunningLevel: 1, dunningFee: 0,
    overduePayments: [{ invoiceRef: '', dueDate: '', outstandingAmount: 0, overdueDays: 0 }]
  };

  addPayment() { this.form.overduePayments.push({ invoiceRef: '', dueDate: '', outstandingAmount: 0, overdueDays: 0 }); }
  getTotalOutstanding(): number { return this.form.overduePayments.reduce((s: number, p: any) => s + (p.outstandingAmount || 0), 0); }

  save() {
    this.saving = true;
    this.restService.request({ method: 'POST', url: '/api/app/dunning', body: this.form }, { apiName: 'Default' })
      .subscribe({ next: () => this.router.navigate(['/sales/dunnings']), error: () => { this.saving = false; } });
  }
}
