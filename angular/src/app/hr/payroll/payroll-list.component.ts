import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PayrollStore } from '../store/payroll.store';
import { CompanyService } from '../../proxy/core/company.service';
import type { CompanyDto } from '../../proxy/core/models';

@Component({
  selector: 'app-payroll-list',
  standalone: true,
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationModule,
    MatTableModule, MatPaginatorModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    StatusBadgeComponent,
  ],
  templateUrl: './payroll-list.component.html',
  styleUrls: ['./payroll-list.component.scss'],
})
export class PayrollListComponent implements OnInit {
  readonly store = inject(PayrollStore);
  private fb = inject(FormBuilder);
  private companyService = inject(CompanyService);

  companies = signal<CompanyDto[]>([]);
  displayedColumns = ['payrollNumber', 'periodLabel', 'totalGrossSalary', 'totalDeductions', 'totalNetSalary', 'status', 'actions'];
  showCreateForm = false;

  createForm = this.fb.group({
    companyId: ['', Validators.required],
    year: [new Date().getFullYear(), [Validators.required, Validators.min(2020)]],
    month: [new Date().getMonth() + 1, [Validators.required, Validators.min(1), Validators.max(12)]],
  });

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: 'year DESC, month DESC' });
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(r => this.companies.set(r.items ?? []));
  }

  onPageChange(event: PageEvent): void {
    this.store.load({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: 'year DESC, month DESC',
    });
  }

  toggleCreateForm(): void {
    this.showCreateForm = !this.showCreateForm;
  }

  runPayroll(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    this.store.create(this.createForm.getRawValue() as any);
    this.showCreateForm = false;
  }
}
