import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-rfq-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'RequestForQuotations' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <span>{{ 'RequestForQuotations' | abpLocalization }}</span>
          <a routerLink="/purchasing/rfq/new" class="btn btn-sm btn-primary">
            <i class="fa fa-plus me-1"></i>{{ 'New' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (rfqs().length === 0) {
            <div class="text-center text-muted py-4">
              <i class="fas fa-file-alt fa-3x mb-3 d-block"></i>
              <p>{{ 'NoRecordsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover">
              <thead>
                <tr>
                  <th>{{ 'RFQ Number' | abpLocalization }}</th>
                  <th>{{ 'Date' | abpLocalization }}</th>
                  <th>{{ 'Suppliers' | abpLocalization }}</th>
                  <th>{{ 'Items' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (rfq of rfqs(); track rfq.id) {
                  <tr>
                    <td>{{ rfq.rfqNumber }}</td>
                    <td>{{ rfq.transactionDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ rfq.suppliers?.length ?? 0 }}</td>
                    <td>{{ rfq.items?.length ?? 0 }}</td>
                    <td><app-status-badge [status]="rfq.status" /></td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </abp-page>
  `,
})
export class RfqListComponent implements OnInit {
  private http = inject(HttpClient);
  rfqs = signal<any[]>([]);

  ngOnInit() {
    this.http.get<any>('/api/app/request-for-quotation', { params: { maxResultCount: '50' } })
      .subscribe({ next: (res) => this.rfqs.set(res.items ?? []), error: () => {} });
  }
}
