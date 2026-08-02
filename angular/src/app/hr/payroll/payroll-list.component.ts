import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PayrollStore } from '../store/payroll.store';
import { PayrollService } from '../../proxy/human-resources/payroll.service';
import { CompanyService } from '../../proxy/core/company.service';
import type { CompanyDto } from '../../proxy/core/models';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-payroll-list',
  standalone: true,
  imports: [
    PaginationComponent, CommonModule, FormsModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe,
    StatusBadgeComponent],
  templateUrl: './payroll-list.component.html',
  styleUrls: ['./payroll-list.component.scss'],
})
export class PayrollListComponent implements OnInit {
  readonly store = inject(PayrollStore);
  private fb = inject(FormBuilder);
  private companyService = inject(CompanyService);
  private payrollService = inject(PayrollService);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  showCreateForm = false;
  isLoadingPreview = false;
  isRunningPayroll = false;
  employeePreview = signal<any>(null);

  months = [
    { value: 1, label: 'January' }, { value: 2, label: 'February' }, { value: 3, label: 'March' },
    { value: 4, label: 'April' }, { value: 5, label: 'May' }, { value: 6, label: 'June' },
    { value: 7, label: 'July' }, { value: 8, label: 'August' }, { value: 9, label: 'September' },
    { value: 10, label: 'October' }, { value: 11, label: 'November' }, { value: 12, label: 'December' },
  ];

  createForm = this.fb.group({
    companyId: ['', Validators.required],
    year: [new Date().getFullYear(), [Validators.required, Validators.min(2020)]],
    month: [new Date().getMonth() + 1, [Validators.required, Validators.min(1), Validators.max(12)]],
  });

  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';

  ngOnInit(): void {
    this.loadData();
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(r => this.companies.set(r.items ?? []));
  }

  loadData(): void {
    this.store.load({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: 'year DESC, month DESC',
      filter: this.searchTerm || undefined,
      status: this.statusFilter || undefined,
    });
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.currentPage = 0;
    this.loadData();
  }

  onStatusChange(status: string): void {
    this.statusFilter = status;
    this.currentPage = 0;
    this.loadData();
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  toggleCreateForm(): void {
    this.showCreateForm = !this.showCreateForm;
    if (!this.showCreateForm) this.employeePreview.set(null);
  }

  previewEmployees(): void {
    if (this.createForm.invalid) return;
    this.isLoadingPreview = true;
    this.payrollService.getEmployeePreview(this.createForm.getRawValue() as any).subscribe({
      next: (result: any) => {
        this.employeePreview.set(result);
        this.isLoadingPreview = false;
      },
      error: () => { this.isLoadingPreview = false; },
    });
  }

  runPayroll(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    this.isRunningPayroll = true;
    this.payrollService.create(this.createForm.getRawValue() as any).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCreated');
        this.showCreateForm = false;
        this.employeePreview.set(null);
        this.isRunningPayroll = false;
        this.loadData();
      },
      error: () => { this.isRunningPayroll = false; }
    });
  }
}
