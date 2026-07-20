import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { RequestForQuotationService } from '../../proxy/purchasing/request-for-quotation.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-rfq-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, PaginationComponent, StatusBadgeComponent],
  template: `
    <abp-page [title]="'RequestForQuotations' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div class="d-flex align-items-center gap-2">
            <input type="text" class="form-control form-control-sm" style="width:200px"
              [(ngModel)]="searchTerm" (keyup.enter)="loadData()" placeholder="Search...">
          </div>
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
                    <td><a [routerLink]="['/purchasing/rfq', rfq.id]">{{ rfq.rfqNumber }}</a></td>
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
      <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage"
        (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class RfqListComponent implements OnInit {
  private rfqService = inject(RequestForQuotationService);
  rfqs = signal<any[]>([]);
  searchTerm = '';
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.rfqService.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize, filter: this.searchTerm || undefined, sorting: '' } as any)
      .subscribe({ next: (res) => { this.rfqs.set(res.items ?? []); this.totalCount = res.totalCount ?? 0; }, error: () => {} });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
