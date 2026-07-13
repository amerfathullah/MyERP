import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface PageEvent {
  pageIndex: number;
  pageSize: number;
}

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (totalCount > pageSize) {
      <nav class="d-flex justify-content-between align-items-center mt-3" aria-label="Pagination">
        <small class="text-muted">
          Showing {{ startItem }}–{{ endItem }} of {{ totalCount }}
        </small>
        <ul class="pagination pagination-sm mb-0">
          <li class="page-item" [class.disabled]="currentPage === 0">
            <button class="page-link" (click)="goToPage(currentPage - 1)" [disabled]="currentPage === 0">
              <i class="fa fa-chevron-left"></i>
            </button>
          </li>
          @for (page of visiblePages; track page) {
            <li class="page-item" [class.active]="page === currentPage">
              <button class="page-link" (click)="goToPage(page)">{{ page + 1 }}</button>
            </li>
          }
          <li class="page-item" [class.disabled]="currentPage >= totalPages - 1">
            <button class="page-link" (click)="goToPage(currentPage + 1)" [disabled]="currentPage >= totalPages - 1">
              <i class="fa fa-chevron-right"></i>
            </button>
          </li>
        </ul>
      </nav>
    }
  `,
})
export class PaginationComponent {
  @Input() totalCount = 0;
  @Input() pageSize = 20;
  @Input() currentPage = 0;
  @Output() pageChange = new EventEmitter<PageEvent>();

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize); }
  get startItem(): number { return this.currentPage * this.pageSize + 1; }
  get endItem(): number { return Math.min((this.currentPage + 1) * this.pageSize, this.totalCount); }

  get visiblePages(): number[] {
    const pages: number[] = [];
    const start = Math.max(0, this.currentPage - 2);
    const end = Math.min(this.totalPages, start + 5);
    for (let i = start; i < end; i++) pages.push(i);
    return pages;
  }

  goToPage(page: number): void {
    if (page < 0 || page >= this.totalPages) return;
    this.currentPage = page;
    this.pageChange.emit({ pageIndex: page, pageSize: this.pageSize });
  }
}
