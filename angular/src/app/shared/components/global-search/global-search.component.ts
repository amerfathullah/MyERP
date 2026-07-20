import { Component, inject, signal, HostListener, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../services/company-context.service';
import { GlobalSearchService } from '../../../proxy/core/global-search.service';
import type { SearchResultDto } from '../../../proxy/core/models';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';

interface SearchResult {
  id: string;
  documentType: string;
  documentNumber: string;
  date: string;
  amount: number;
  status: string;
  route: string;
}

@Component({
  selector: 'app-global-search',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="position-relative" style="width: 320px;">
      <div class="input-group input-group-sm">
        <span class="input-group-text bg-white border-end-0"><i class="fa fa-search text-muted"></i></span>
        <input #searchInput type="text"
          class="form-control border-start-0"
          [placeholder]="'SearchDocuments' | abpLocalization"
          [(ngModel)]="searchTerm"
          (ngModelChange)="onSearch($event)"
          (focus)="showResults = true"
          (blur)="onBlur()"
          (keydown.escape)="clearSearch()" />
        @if (loading()) {
          <span class="input-group-text bg-white"><i class="fa fa-spinner fa-spin text-muted"></i></span>
        }
      </div>

      @if (showResults && results().length > 0) {
        <div class="dropdown-menu show w-100 mt-1 shadow-lg" style="max-height: 400px; overflow-y: auto;">
          @for (result of results(); track result.id) {
            <a class="dropdown-item d-flex align-items-center py-2" (mousedown)="navigateTo(result)">
              <span class="me-2">
                @switch (result.documentType) {
                  @case ('SalesInvoice') { <i class="fa fa-file-invoice text-primary"></i> }
                  @case ('PurchaseInvoice') { <i class="fa fa-file-invoice text-danger"></i> }
                  @case ('SalesOrder') { <i class="fa fa-cart-shopping text-success"></i> }
                  @case ('PurchaseOrder') { <i class="fa fa-basket-shopping text-warning"></i> }
                  @case ('PaymentEntry') { <i class="fa fa-money-bill-transfer text-info"></i> }
                  @case ('JournalEntry') { <i class="fa fa-book text-secondary"></i> }
                  @case ('Customer') { <i class="fa fa-user text-primary"></i> }
                  @case ('Supplier') { <i class="fa fa-truck text-warning"></i> }
                  @default { <i class="fa fa-file"></i> }
                }
              </span>
              <div class="flex-grow-1 overflow-hidden">
                <div class="d-flex justify-content-between">
                  <strong class="text-truncate">{{ result.documentNumber }}</strong>
                  @if (result.amount > 0) {
                    <small class="text-muted ms-2">{{ result.amount | number:'1.2-2' }}</small>
                  }
                </div>
                <small class="text-muted">
                  {{ getTypeLabel(result.documentType) }} · {{ result.date | date:'dd/MM/yyyy' }}
                  · <span [class]="getStatusClass(result.status)">{{ result.status }}</span>
                </small>
              </div>
            </a>
          }
        </div>
      }

      @if (showResults && searchTerm.length >= 2 && results().length === 0 && !loading()) {
        <div class="dropdown-menu show w-100 mt-1">
          <div class="dropdown-item text-muted text-center py-3">
            <i class="fa fa-search me-1"></i> {{ 'NoResultsFound' | abpLocalization }}
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .dropdown-item { cursor: pointer; }
    .dropdown-item:hover { background-color: #f8f9fa; }
  `]
})
export class GlobalSearchComponent {
  private globalSearchService = inject(GlobalSearchService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);

  @ViewChild('searchInput') searchInput!: ElementRef<HTMLInputElement>;

  searchTerm = '';
  showResults = false;
  results = signal<SearchResult[]>([]);
  loading = signal(false);

  private searchSubject = new Subject<string>();

  /** Ctrl+K / Cmd+K to focus the search bar */
  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if ((event.ctrlKey || event.metaKey) && event.key === 'k') {
      event.preventDefault();
      this.searchInput?.nativeElement?.focus();
    }
  }

  constructor() {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => {
        if (term.length < 2) {
          this.results.set([]);
          return of(null);
        }
        this.loading.set(true);
        const companyId = this.companyContext.currentCompanyId();
        return this.globalSearchService.search({ query: term, companyId, maxResults: 15 });
      })
    ).subscribe({
      next: (res) => {
        if (res) this.results.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onSearch(term: string) {
    this.searchSubject.next(term);
  }

  navigateTo(result: SearchResult) {
    this.router.navigateByUrl(result.route);
    this.searchTerm = '';
    this.results.set([]);
    this.showResults = false;
  }

  onBlur() {
    // Delay to allow click on results
    setTimeout(() => { this.showResults = false; }, 200);
  }

  clearSearch() {
    this.searchTerm = '';
    this.results.set([]);
    this.showResults = false;
  }

  getTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      SalesInvoice: 'Invoice',
      PurchaseInvoice: 'Bill',
      SalesOrder: 'SO',
      PurchaseOrder: 'PO',
      PaymentEntry: 'Payment',
      JournalEntry: 'JE',
      Customer: 'Customer',
      Supplier: 'Supplier'
    };
    return labels[type] ?? type;
  }

  getStatusClass(status: string): string {
    if (status === 'Posted' || status === 'Completed' || status === 'Active') return 'text-success';
    if (status === 'Cancelled' || status === 'Inactive') return 'text-danger';
    if (status === 'Draft') return 'text-secondary';
    return 'text-warning';
  }
}
