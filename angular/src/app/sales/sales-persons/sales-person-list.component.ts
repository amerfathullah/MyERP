import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe , RestService } from '@abp/ng.core';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-sales-person-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'SalesPersons' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/sales/sales-persons/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewSalesPerson' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && persons.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-user-tie fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoSalesPersonsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'CommissionRate' | abpLocalization }}</th>
              <th>{{ 'Type' | abpLocalization }}</th>
              <th>{{ 'Targets' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (p of persons; track p.id) {
                <tr>
                  <td>
                    @if (p.parentSalesPersonId) { <i class="fa fa-level-up-alt fa-rotate-90 text-muted me-2"></i> }
                    <a [routerLink]="['/sales/sales-persons', p.id]">{{ p.name }}</a>
                  </td>
                  <td>{{ p.commissionRate }}%</td>
                  <td>
                    <span class="badge" [class]="p.isGroup ? 'bg-info' : 'bg-light text-dark'">
                      {{ p.isGroup ? 'Group' : 'Individual' }}
                    </span>
                  </td>
                  <td>{{ p.targets?.length ?? 0 }}</td>
                  <td>
                    <span class="badge" [class]="p.isEnabled ? 'bg-success' : 'bg-secondary'">
                      {{ p.isEnabled ? 'Active' : 'Disabled' }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `
})
export class SalesPersonListComponent implements OnInit {
  private restService = inject(RestService);
  persons: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 50;
  currentPage = 0;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.restService.request<any, any>({ method: 'GET', url: '/api/app/sales-person', params: { skipCount: String(this.currentPage * this.pageSize), maxResultCount: String(this.pageSize) } }, { apiName: 'Default' }).subscribe({
      next: res => { this.persons = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
