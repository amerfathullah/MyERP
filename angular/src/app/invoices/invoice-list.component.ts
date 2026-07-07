import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListService } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';

@Component({
  selector: 'app-invoice-list',
  standalone: true,
  imports: [CommonModule, PageModule],
  providers: [ListService],
  template: `
    <abp-page [title]="'Sales Invoices'">
      <div class="card">
        <div class="card-header d-flex justify-content-between">
          <h5>Sales Invoices</h5>
          <button class="btn btn-primary btn-sm" (click)="createInvoice()">
            <i class="fa fa-plus me-1"></i> New Invoice
          </button>
        </div>
        <div class="card-body">
          <table class="table table-striped">
            <thead>
              <tr>
                <th>Invoice #</th>
                <th>Date</th>
                <th>Customer</th>
                <th>Total (MYR)</th>
                <th>Status</th>
                <th>e-Invoice</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let inv of invoices">
                <td><strong>{{ inv.invoiceNumber }}</strong></td>
                <td>{{ inv.issueDate | date:'dd/MM/yyyy' }}</td>
                <td>{{ inv.customerName }}</td>
                <td class="text-end">{{ inv.grandTotal | number:'1.2-2' }}</td>
                <td>
                  <span class="badge"
                    [class.bg-secondary]="inv.status === 'Draft'"
                    [class.bg-info]="inv.status === 'Submitted'"
                    [class.bg-success]="inv.status === 'Posted'"
                    [class.bg-danger]="inv.status === 'Cancelled'">
                    {{ inv.status }}
                  </span>
                </td>
                <td>
                  <span class="badge"
                    [class.bg-light]="inv.eInvoiceStatus === 'NotSubmitted'"
                    [class.bg-warning]="inv.eInvoiceStatus === 'Pending'"
                    [class.bg-success]="inv.eInvoiceStatus === 'Valid'"
                    [class.bg-danger]="inv.eInvoiceStatus === 'Invalid'">
                    {{ inv.eInvoiceStatus }}
                  </span>
                </td>
                <td>
                  <button *ngIf="inv.status === 'Draft'" class="btn btn-sm btn-outline-primary me-1" (click)="submit(inv)">Submit</button>
                  <button *ngIf="inv.status === 'Submitted'" class="btn btn-sm btn-outline-success me-1" (click)="post(inv)">Post</button>
                  <button *ngIf="inv.status === 'Posted'" class="btn btn-sm btn-outline-danger" (click)="cancel(inv)">Cancel</button>
                </td>
              </tr>
            </tbody>
          </table>
          <p *ngIf="invoices.length === 0" class="text-muted text-center py-4">
            No invoices yet.
          </p>
        </div>
      </div>
    </abp-page>
  `,
})
export class InvoiceListComponent implements OnInit {
  invoices: any[] = [];

  constructor(public readonly list: ListService) {}

  ngOnInit(): void {
    // TODO: Wire up to SalesInvoiceAppService proxy
  }

  createInvoice(): void {
    // TODO: Open create dialog
  }

  submit(inv: any): void {
    // TODO: Call SalesInvoiceAppService.submit(inv.id)
  }

  post(inv: any): void {
    // TODO: Call SalesInvoiceAppService.post(inv.id)
  }

  cancel(inv: any): void {
    // TODO: Call SalesInvoiceAppService.cancel(inv.id)
  }
}
