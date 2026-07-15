import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { JobCardService } from '../../proxy/manufacturing/job-card.service';
import type { JobCardDto } from '../../proxy/manufacturing/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-job-card-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'JobCards' | abpLocalization">
      <div class="d-flex justify-content-between mb-3">
        <input type="text" class="form-control form-control-sm" style="width:200px"
          [(ngModel)]="searchTerm" (keyup.enter)="loadData()" placeholder="Search...">
      </div>
      @if (isLoading) { <div class="text-center py-3"><i class="fa fa-spinner fa-spin fa-2x"></i></div> }
      @if (!isLoading && jobCards.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-id-card fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoJobCardsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Operation' | abpLocalization }}</th>
              <th>{{ 'Quantity' | abpLocalization }}</th>
              <th class="text-end">{{ 'CompletedQty' | abpLocalization }}</th>
              <th class="text-end">{{ 'TimeSpent' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (jc of jobCards; track jc.id) {
                <tr>
                  <td>{{ jc.sequenceId }}</td>
                  <td>{{ jc.forQuantity }}</td>
                  <td class="text-end">{{ jc.completedQty }} / {{ jc.forQuantity }}</td>
                  <td class="text-end">{{ jc.totalTimeInMins | number:'1.0-0' }} min</td>
                  <td><span class="badge" [ngClass]="getStatusClass(jc.status)">{{ getStatusLabel(jc.status) }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
      <app-pagination [totalCount]="0" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
  </abp-page>
  `,
})
export class JobCardListComponent implements OnInit {
  private service = inject(JobCardService);
  jobCards: JobCardDto[] = [];
  isLoading = false;

  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  totalCount = 0;

  ngOnInit(): void { this.loadData(); }

  loadData() {
    this.isLoading = true;
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    if (this.searchTerm) params.filter = this.searchTerm;
    this.service.getList(params)
      .subscribe({ next: (r) => { this.jobCards = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  getStatusLabel(s: number | undefined): string {
    return ['Open', 'Work In Progress', 'Material Transferred', 'Completed', 'On Hold', 'Cancelled'][s ?? 0] ?? 'Open';
  }

  getStatusClass(s: number | undefined): string {
    return ['bg-secondary', 'bg-primary', 'bg-info', 'bg-success', 'bg-warning', 'bg-danger'][s ?? 0] ?? 'bg-secondary';
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
