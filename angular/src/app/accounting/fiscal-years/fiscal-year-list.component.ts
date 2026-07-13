import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../proxy/core/company.service';
import type { CompanyDto } from '../../proxy/core/models';

interface FiscalYearDto {
  id: string;
  name: string;
  companyId: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
}

@Component({
  selector: 'app-fiscal-year-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'FiscalYears' | abpLocalization">
      <!-- Create form -->
      <div class="card mb-4">
        <div class="card-header"><h6 class="card-title mb-0"><i class="fa fa-plus me-2"></i>{{ 'CreateFiscalYear' | abpLocalization }}</h6></div>
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="create()" class="row g-3 align-items-end">
            <div class="col-md-3">
              <label class="form-label">{{ 'Company' | abpLocalization }}</label>
              <select class="form-select" formControlName="companyId">
                <option value="" disabled>{{ 'SelectCompany' | abpLocalization }}</option>
                @for (c of companies(); track c.id) { <option [value]="c.id">{{ c.name }}</option> }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'Name' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="name" placeholder="FY 2026-27" />
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'StartDate' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="startDate" />
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'EndDate' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="endDate" />
            </div>
            <div class="col-md-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
                <i class="fa fa-plus me-1"></i>{{ 'Create' | abpLocalization }}
              </button>
            </div>
          </form>
        </div>
      </div>

      <!-- List -->
      @if (isLoading()) {
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
      } @else if (fiscalYears().length === 0) {
        <div class="text-center py-5 text-muted">
          <i class="fa fa-calendar-days fa-3x mb-3"></i>
          <p>{{ 'NoFiscalYearsYet' | abpLocalization }}</p>
        </div>
      } @else {
        <div class="card">
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th class="ps-3">{{ 'Name' | abpLocalization }}</th>
                  <th>{{ 'StartDate' | abpLocalization }}</th>
                  <th>{{ 'EndDate' | abpLocalization }}</th>
                  <th class="text-center">{{ 'Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (fy of fiscalYears(); track fy.id) {
                  <tr>
                    <td class="ps-3 fw-bold">{{ fy.name }}</td>
                    <td>{{ fy.startDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ fy.endDate | date:'dd/MM/yyyy' }}</td>
                    <td class="text-center">
                      @if (fy.isClosed) { <span class="badge bg-secondary">Closed</span> }
                      @else { <span class="badge bg-success">Open</span> }
                    </td>
                    <td class="text-end">
                      @if (!fy.isClosed) {
                        <button class="btn btn-outline-warning btn-sm" (click)="closeFy(fy.id)">
                          <i class="fa fa-lock me-1"></i>Close
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class FiscalYearListComponent implements OnInit {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  fiscalYears = signal<FiscalYearDto[]>([]);
  isLoading = signal(true);

  form = this.fb.group({
    companyId: ['', Validators.required],
    name: ['', Validators.required],
    startDate: ['', Validators.required],
    endDate: ['', Validators.required],
  });

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => this.companies.set(res.items ?? []));
    this.loadFiscalYears();
  }

  loadFiscalYears(): void {
    this.http.get<any>('/api/app/fiscal-year', { params: { skipCount: '0', maxResultCount: '50' } })
      .subscribe({
        next: res => { this.fiscalYears.set(res.items ?? res ?? []); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
  }

  create(): void {
    if (this.form.invalid) return;
    this.http.post('/api/app/fiscal-year', this.form.getRawValue()).subscribe({
      next: () => { this.toaster.success('Fiscal Year created'); this.loadFiscalYears(); this.form.reset(); },
      error: () => this.toaster.error('Failed to create'),
    });
  }

  closeFy(id: string): void {
    if (!confirm('Close this fiscal year? This cannot be undone.')) return;
    this.http.post(`/api/app/fiscal-year/${id}/close`, {}).subscribe({
      next: () => { this.toaster.success('Fiscal Year closed'); this.loadFiscalYears(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed to close fiscal year'),
    });
  }
}
