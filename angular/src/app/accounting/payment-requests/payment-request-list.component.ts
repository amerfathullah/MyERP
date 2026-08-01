import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PaymentRequestService } from '../../proxy/accounting/payment-request.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { RouterModule } from '@angular/router';

@Component({
  standalone: true,
  selector: 'app-payment-request-list',
  imports: [CommonModule, FormsModule, LocalizationPipe, RouterModule],
  template: `
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header">
          <h5 class="mb-0"><i class="fas fa-credit-card me-2"></i>{{ '::PaymentRequests' | abpLocalization }}</h5>
        </div>
        <div class="card-body">
          <div class="row mb-3 g-2">
            <div class="col-md-4">
              <input class="form-control form-control-sm" [placeholder]="'::Search' | abpLocalization" [(ngModel)]="searchTerm" (keyup.enter)="load()" />
            </div>
            <div class="col-md-3">
              <select class="form-select form-select-sm" [(ngModel)]="statusFilter" (change)="load()">
                <option value="">{{ '::AllStatuses' | abpLocalization }}</option>
                <option value="Draft">Draft</option>
                <option value="Initiated">Initiated</option>
                <option value="Paid">Paid</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </div>
          </div>
          @if (items().length === 0) {
            <div class="text-center text-muted py-4">
              <i class="fas fa-credit-card fa-2x mb-2"></i>
              <p>{{ '::NoPaymentRequestsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead><tr>
                <th>{{ '::ReferenceNumber' | abpLocalization }}</th>
                <th>{{ '::Party' | abpLocalization }}</th>
                <th>{{ '::Amount' | abpLocalization }}</th>
                <th>{{ '::Status' | abpLocalization }}</th>
                <th>{{ '::Date' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr>
                    <td><a [routerLink]="['/accounting/payment-requests', item.id]" class="text-primary text-decoration-none">{{ item.referenceNumber || '—' }}</a></td>
                    <td>{{ item.partyName || '—' }}</td>
                    <td>{{ item.grandTotal | number:'1.2-2' }}</td>
                    <td><span class="badge" [class]="getStatusClass(item.status)">{{ getStatusLabel(item.status) }}</span></td>
                    <td>{{ item.transactionDate | date:'dd/MM/yyyy' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  `
})
export class PaymentRequestListComponent implements OnInit {
  private paymentRequestService = inject(PaymentRequestService);
  items = signal<any[]>([]);
  searchTerm = '';
  statusFilter = '';

  ngOnInit() { this.load(); }

  load() {
    const params: any = { maxResultCount: 50 };
    if (this.searchTerm) params.filter = this.searchTerm;
    if (this.statusFilter) params.status = this.statusFilter;
    this.paymentRequestService.getList(params as any).subscribe({ next: res => this.items.set(res.items ?? []), error: () => {} });
  }

  getStatusClass(s: number) {
    return s === 0 ? 'bg-secondary' : s === 1 ? 'bg-info' : s === 2 ? 'bg-success' : 'bg-danger';
  }
  getStatusLabel(s: number) {
    return s === 0 ? 'Draft' : s === 1 ? 'Initiated' : s === 2 ? 'Paid' : 'Cancelled';
  }
}
