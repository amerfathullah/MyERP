import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe , RestService } from '@abp/ng.core';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-pos-closing-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent],
  template: `
    <abp-page [title]="'POSClosing' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <span>{{ 'POSClosing' | abpLocalization }}</span>
          <a routerLink="/sales/pos-closing/new" class="btn btn-sm btn-primary">
            <i class="fa fa-plus me-1"></i>{{ 'New' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (entries().length === 0) {
            <div class="text-center text-muted py-4">
              <i class="fas fa-cash-register fa-3x mb-3 d-block"></i>
              <p>{{ 'NoRecordsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover">
              <thead>
                <tr>
                  <th>{{ 'Date' | abpLocalization }}</th>
                  <th>{{ 'Invoices' | abpLocalization }}</th>
                  <th>{{ 'GrandTotal' | abpLocalization }}</th>
                  <th>{{ 'Difference' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (e of entries(); track e.id) {
                  <tr [routerLink]="['/sales/pos-closing', e.id]" class="cursor-pointer">
                    <td>{{ e.postingDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ e.invoices?.length ?? 0 }}</td>
                    <td>{{ e.grandTotal | number:'1.2-2' }}</td>
                    <td [class.text-danger]="e.totalDifference > 0" [class.text-success]="e.totalDifference < 0">
                      {{ e.totalDifference | number:'1.2-2' }}
                    </td>
                    <td><app-status-badge [status]="e.status" /></td>
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
export class PosClosingListComponent implements OnInit {
  private restService = inject(RestService);
  entries = signal<any[]>([]);

  ngOnInit() {
    this.restService.request<any, any>({ method: 'GET', url: '/api/app/pos-closing', params: { maxResultCount: '50' } }, { apiName: 'Default' })
      .subscribe({ next: (res) => this.entries.set(res.items ?? []), error: () => {} });
  }
}
