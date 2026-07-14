import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { EmployeeService } from '../../proxy/hr/employee.service';
import { CompanyService } from '../../proxy/core/company.service';
import { EmployeeStore } from '../store/employee.store';
import type { CompanyDto } from '../../proxy/core/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-employee-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './employee-form.component.html',
  styleUrls: ['./employee-form.component.scss'],
})
export class EmployeeFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(EmployeeStore);
  private service = inject(EmployeeService);
  private companyService = inject(CompanyService);

  companies = signal<CompanyDto[]>([]);
  isEditMode = false;
  entityId: string | null = null;

  form = this.fb.group({
    companyId: ['', Validators.required],
    firstName: ['', [Validators.required, Validators.maxLength(128)]],
    lastName: [''],
    dateOfBirth: [''],
    dateOfJoining: [''],
    phone: ['', Validators.maxLength(20)],
    email: ['', [Validators.email, Validators.maxLength(200)]],
    designation: [''],
    department: [''],
    epfNumber: [''],
    socsoNumber: [''],
    taxNumber: [''],
  });

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(r => this.companies.set(r.items ?? []));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(emp => {
        this.form.patchValue({
          companyId: emp.companyId,
          firstName: emp.firstName,
          lastName: emp.lastName ?? '',
          dateOfBirth: emp.dateOfBirth ?? '',
          dateOfJoining: emp.dateOfJoining ?? '',
          phone: emp.phone ?? '',
          email: emp.email ?? '',
          designation: emp.designation ?? '',
          department: emp.department ?? '',
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const dto = this.form.getRawValue() as any;

    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe(() => {
        this.router.navigate(['/hr/employees']);
      });
    } else {
      this.store.create(dto);
      this.router.navigate(['/hr/employees']);
    }
  }

  cancel(): void {
    this.router.navigate(['/hr/employees']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}