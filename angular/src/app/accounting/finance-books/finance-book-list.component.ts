import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface FinanceBookDto {
  id: string;
  companyId: string;
  name: string;
  isDefault: boolean;
  description?: string;
}

@Component({
  selector: 'app-finance-book-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ '::FinanceBooks' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showForm = !showForm">
            <i class="fa fa-plus me-1"></i>{{ '::NewFinanceBook' | abpLocalization }}
          </button>
        </div>

        @if (showForm) {
          <div class="card-body border-bottom bg-light">
            <div class="row g-2">
              <div class="col-md-4">
                <input type="text" class="form-control form-control-sm"
                  [(ngModel)]="newBook.name" [placeholder]="'::BookName' | abpLocalization">
              </div>
              <div class="col-md-4">
                <input type="text" class="form-control form-control-sm"
                  [(ngModel)]="newBook.description" [placeholder]="'::Description' | abpLocalization">
              </div>
              <div class="col-md-2">
                <div class="form-check mt-2">
                  <input type="checkbox" class="form-check-input" id="isDefault" [(ngModel)]="newBook.isDefault">
                  <label class="form-check-label" for="isDefault">{{ '::Default' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-2">
                <button class="btn btn-success btn-sm w-100" (click)="create()" [disabled]="!newBook.name">
                  <i class="fa fa-check me-1"></i>{{ '::Save' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        }

        <div class="card-body p-0">
          @if (books().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-book fa-2x mb-2"></i>
              <p>{{ '::NoFinanceBooksYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th>{{ '::Default' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (book of books(); track book.id) {
                  <tr>
                    <td class="fw-medium">{{ book.name }}</td>
                    <td class="text-muted">{{ book.description || '—' }}</td>
                    <td>
                      @if (book.isDefault) {
                        <span class="badge bg-primary">{{ '::Default' | abpLocalization }}</span>
                      }
                    </td>
                    <td class="text-end">
                      @if (!book.isDefault) {
                        <button class="btn btn-outline-primary btn-sm me-1" (click)="setDefault(book.id)"
                          title="Set as Default">
                          <i class="fa fa-star"></i>
                        </button>
                      }
                      <button class="btn btn-outline-danger btn-sm" (click)="remove(book.id)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
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
export class FinanceBookListComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  books = signal<FinanceBookDto[]>([]);
  showForm = false;
  newBook = { name: '', description: '', isDefault: false };

  ngOnInit() {
    this.load();
  }

  load() {
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { maxResultCount: 100 };
    if (companyId) params.companyId = companyId;

    this.http.get<any>('/api/app/finance-book', { params }).subscribe(res => {
      this.books.set(res.items ?? []);
    });
  }

  create() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.newBook.name) return;

    this.http.post<FinanceBookDto>('/api/app/finance-book', {
      companyId,
      name: this.newBook.name,
      description: this.newBook.description || undefined,
      isDefault: this.newBook.isDefault
    }).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCreated');
        this.newBook = { name: '', description: '', isDefault: false };
        this.showForm = false;
        this.load();
      }
    });
  }

  setDefault(id: string) {
    this.http.post(`/api/app/finance-book/${id}/set-default`, {}).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyUpdated');
        this.load();
      }
    });
  }

  remove(id: string) {
    if (!confirm('Delete this finance book?')) return;
    this.http.delete(`/api/app/finance-book/${id}`).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyDeleted');
        this.load();
      }
    });
  }
}
