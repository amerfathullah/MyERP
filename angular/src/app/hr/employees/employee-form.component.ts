import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { CompanyService } from '../../proxy/core/company.service';
import { EmployeeStore } from '../store/employee.store';
import type { CompanyDto } from '../../proxy/core/models';
import type { EmployeeDto } from '../../proxy/human-resources/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { LinkPickerComponent } from '../../shared/components/link-picker/link-picker.component';
import { map, Observable } from 'rxjs';
import { IdentityUserLookupService } from '@abp/ng.identity/proxy';
import type { UserData } from '@abp/ng.identity/proxy';

@Component({
  selector: 'app-employee-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe, LinkPickerComponent],
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
  private toaster = inject(ToasterService);
  private userLookupService = inject(IdentityUserLookupService);

  companies = signal<CompanyDto[]>([]);
  isEditMode = false;
  entityId: string | null = null;

  form = this.fb.group({
    companyId: ['', Validators.required],
    firstName: ['', [Validators.required, Validators.maxLength(128)]],
    lastName: ['', Validators.maxLength(128)],
    dateOfBirth: [''],
    dateOfJoining: [''],
    phone: ['', Validators.maxLength(20)],
    email: ['', [Validators.email, Validators.maxLength(200)]],
    designation: ['', Validators.maxLength(128)],
    department: ['', Validators.maxLength(128)],
    epfNumber: ['', Validators.maxLength(100)],
    socsoNumber: ['', Validators.maxLength(100)],
    taxNumber: ['', Validators.maxLength(100)],
    reportsToEmployeeId: [null as string | null],
    userId: [null as string | null],
  });

  reportsToSearchFn = (filter: string): Observable<EmployeeDto[]> =>
    this.service.getList({ filter, skipCount: 0, maxResultCount: 20, sorting: '' } as any)
      .pipe(map(res => (res.items ?? []).filter(e => e.id !== this.entityId)));
  reportsToGetByIdFn = (id: string) => this.service.get(id);
  reportsToDisplayFn = (e: EmployeeDto | null) => e?.fullName ?? '';

  userIdSearchFn = (filter: string): Observable<UserData[]> =>
    this.userLookupService.search({ filter, skipCount: 0, maxResultCount: 20, sorting: '' } as any)
      .pipe(map(res => res.items ?? []));
  userIdGetByIdFn = (id: string) => this.userLookupService.findById(id);
  userIdDisplayFn = (u: UserData | null) => u?.userName ?? u?.email ?? '';

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
          reportsToEmployeeId: emp.reportsToEmployeeId ?? null,
          userId: emp.userId ?? null,
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
      this.service.update(this.entityId!, dto).subscribe({
        next: () => this.router.navigate(['/hr/employees']),
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::SaveFailed'),
      });
    } else {
      this.service.create(dto).subscribe({
        next: () => this.router.navigate(['/hr/employees']),
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::SaveFailed'),
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/hr/employees']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
