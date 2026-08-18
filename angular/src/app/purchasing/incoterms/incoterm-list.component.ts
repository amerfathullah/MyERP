import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { IncotermService } from '../../proxy/purchasing/incoterm.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import type { IncotermDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-incoterm-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'Incoterms' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'Incoterms' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/purchasing/incoterms/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewIncoterm' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-ship fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoIncotermsYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/purchasing/incoterms/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewIncoterm' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'Code' | abpLocalization }}</th>
                <th>{{ 'Title' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (i of items(); track i.id) {
                  <tr>
                    <td class="fw-semibold">{{ i.code }}</td>
                    <td>{{ i.title }}</td>
                    <td>
                      @if (i.isActive) {
                        <span class="badge bg-success">{{ 'Active' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-secondary">{{ 'Disabled' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/purchasing/incoterms', i.id, 'edit']">
                          <i class="fa fa-edit"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(i)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
      <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize"
        [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class IncotermListComponent implements OnInit {
  private service = inject(IncotermService);
  private confirmation = inject(ConfirmationService);

  items = signal<IncotermDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize } as any).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  delete(i: IncotermDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(i.id!).subscribe({ next: () => this.loadData() });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
