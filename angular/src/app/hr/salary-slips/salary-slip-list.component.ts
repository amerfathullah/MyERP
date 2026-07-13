import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';

interface SalarySlipDto {
  id: string;
  employeeName?: string;
  postingDate: string;
  startDate: string;
  endDate: string;
  grossAmount: number;
  totalDeductions: number;
  netAmount: number;
  status: number;
}

@Component({
  selector: 'app-salary-slip-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'SalarySlips' | abpLocalization">
      @if (isLoading()) {
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
      } @else if (slips().length === 0) {
        <div class="text-center py-5 text-muted">
          <i class="fa fa-file-invoice-dollar fa-3x mb-3"></i>
          <p>{{ 'NoSalarySlipsYet' | abpLocalization }}</p>
        </div>
      } @else {
        <div class="card">
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th class="ps-3">{{ 'Employee' | abpLocalization }}</th>
                  <th>{{ 'Period' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Gross' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Deductions' | abpLocalization }}</th>
                  <th class="text-end">{{ 'NetPay' | abpLocalization }}</th>
                  <th class="text-center">{{ 'Status' | abpLocalization }}</th>
                  <th class="pe-3"></th>
                </tr>
              </thead>
              <tbody>
                @for (slip of slips(); track slip.id) {
                  <tr>
                    <td class="ps-3">{{ slip.employeeName ?? '—' }}</td>
                    <td>{{ slip.startDate | date:'MMM yyyy' }}</td>
                    <td class="text-end font-monospace">{{ slip.grossAmount | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace text-danger">{{ slip.totalDeductions | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace fw-bold">{{ slip.netAmount | number:'1.2-2' }}</td>
                    <td class="text-center">
                      <span class="badge" [ngClass]="slip.status === 1 ? 'bg-success' : 'bg-secondary'">
                        {{ slip.status === 1 ? 'Submitted' : 'Draft' }}
                      </span>
                    </td>
                    <td class="pe-3 text-end">
                      <a [routerLink]="['/hr/salary-slips', slip.id]" class="btn btn-sm btn-outline-primary">
                        <i class="fa fa-eye"></i>
                      </a>
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
export class SalarySlipListComponent implements OnInit {
  private http = inject(HttpClient);
  slips = signal<SalarySlipDto[]>([]);
  isLoading = signal(true);

  ngOnInit(): void {
    this.http.get<any>('/api/app/salary-slip', { params: { skipCount: '0', maxResultCount: '100' } })
      .subscribe({
        next: res => { this.slips.set(res.items ?? []); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
  }
}
