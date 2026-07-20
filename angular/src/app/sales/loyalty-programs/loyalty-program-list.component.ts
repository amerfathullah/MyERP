import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { LoyaltyProgramService } from '../../proxy/sales/loyalty-program.service';

@Component({
  selector: 'app-loyalty-program-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'LoyaltyPrograms' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/sales/loyalty-programs/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewLoyaltyProgram' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && programs.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-gift fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoLoyaltyProgramsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'ConversionFactor' | abpLocalization }}</th>
              <th>{{ 'ExpiryDays' | abpLocalization }}</th>
              <th>{{ 'Tiers' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (p of programs; track p.id) {
                <tr>
                  <td><a [routerLink]="['/sales/loyalty-programs', p.id]">{{ p.name }}</a></td>
                  <td>{{ p.conversionFactor }}</td>
                  <td>{{ p.expiryDurationDays > 0 ? p.expiryDurationDays + ' days' : 'Never' }}</td>
                  <td>{{ p.tiers?.length ?? 0 }}</td>
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
export class LoyaltyProgramListComponent implements OnInit {
  private service = inject(LoyaltyProgramService);
  programs: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize, sorting: '' }).subscribe({
      next: res => { this.programs = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
