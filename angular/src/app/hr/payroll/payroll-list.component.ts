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
  }

  runPayroll(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    this.payrollService.create(this.createForm.getRawValue() as any).subscribe({
      next: () => {
        this.toaster.success('Payroll created successfully');
        this.showCreateForm = false;
        this.loadData();
      },
      error: () => {}
    });
  }
}
