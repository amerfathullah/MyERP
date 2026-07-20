import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SalarySlipService } from '../../proxy/human-resources/salary-slip.service';
import type { SalarySlipDto } from '../../proxy/human-resources/models';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-salary-slip-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
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
      <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
  </abp-page>
  `,
})
export class SalarySlipListComponent implements OnInit {
  private salarySlipService = inject(SalarySlipService);
  slips = signal<SalarySlipDto[]>([]);
  isLoading = signal(true);
  totalCount = 0;

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.salarySlipService.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize, sorting: '' } as any)
      .subscribe({
        next: res => { this.slips.set(res.items ?? []); this.totalCount = res.totalCount ?? 0; this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}