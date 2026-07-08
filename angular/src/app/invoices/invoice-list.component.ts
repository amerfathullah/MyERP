import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { SalesInvoiceService } from '../proxy/sales/sales-invoice.service';
import { SalesInvoiceStore } from '../sales/store/sales-invoice.store';
import type { SalesInvoiceDto } from '../proxy/sales/models';

@Component({
  selector: 'app-invoice-list',
  standalone: true,
  imports: [CommonModule, PageModule],
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
              @for (inv of store.entities(); track inv.id) {
                <tr>
                  <td><strong>{{ inv.invoiceNumber }}</strong></td>
                  <td>{{ inv.issueDate }}</td>
                  <td>{{ inv.customerName }}</td>
                  <td class="text-end">{{ inv.grandTotal }}</td>
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
                      [class.bg-warning]="inv.eInvoiceStatus === 'Submitted'"
                      [class.bg-success]="inv.eInvoiceStatus === 'Valid'"
                      [class.bg-danger]="inv.eInvoiceStatus === 'Invalid'">
                      {{ inv.eInvoiceStatus }}
                    </span>
                  </td>
                  <td>
                    @if (inv.status === 'Draft') {
                      <button class="btn btn-sm btn-outline-primary me-1" (click)="submit(inv)">Submit</button>
                    }
                    @if (inv.status === 'Submitted') {
                      <button class="btn btn-sm btn-outline-success me-1" (click)="post(inv)">Post</button>
                    }
                    @if (inv.status === 'Posted') {
                      <button class="btn btn-sm btn-outline-danger" (click)="cancel(inv)">Cancel</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
          @if (!store.hasInvoices()) {
            <p class="text-muted text-center py-4">No invoices yet.</p>
          }
        </div>
      </div>
    </abp-page>
  `,
})
export class InvoiceListComponent implements OnInit {
  readonly store = inject(SalesInvoiceStore);
  private router = inject(Router);

  ngOnInit(): void {
    this.store.loadInvoices({ skipCount: 0, maxResultCount: 50, sorting: '' });
  }

  createInvoice(): void {
    this.router.navigate(['/sales/invoices/new']);
  }

  submit(inv: SalesInvoiceDto): void {
    this.store.submitInvoice(inv.id!);
  }

  post(inv: SalesInvoiceDto): void {
    this.store.postInvoice(inv.id!);
  }

  cancel(inv: SalesInvoiceDto): void {
    this.store.cancelInvoice(inv.id!);
  }
}
