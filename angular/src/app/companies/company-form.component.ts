import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../proxy/core/company.service';

import { AutoValidationDirective } from '../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-company-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './company-form.component.html',
  styleUrls: ['./company-form.component.scss'],
})
export class CompanyFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(CompanyService);
  private toaster = inject(ToasterService);

  isEditMode = false;
  entityId: string | null = null;

  form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(256)]],
    shortName: [''],
    taxId: ['', Validators.maxLength(20)],
    registrationNumber: [''],
    sstRegistrationNumber: [''],
    msicCode: [''],
    phone: [''],
    email: ['', Validators.email],
    website: [''],
    address: [''],
    city: [''],
    state: [''],
    postalCode: [''],
    country: ['Malaysia'],
    currencyCode: ['MYR', Validators.required],
    fiscalYearStartMonth: [1],
    isActive: [true],
  });

  months = [
    { value: 1, label: 'January' }, { value: 2, label: 'February' },
    { value: 3, label: 'March' }, { value: 4, label: 'April' },
    { value: 5, label: 'May' }, { value: 6, label: 'June' },
    { value: 7, label: 'July' }, { value: 8, label: 'August' },
    { value: 9, label: 'September' }, { value: 10, label: 'October' },
    { value: 11, label: 'November' }, { value: 12, label: 'December' }];

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(company => {
        this.form.patchValue(company as any);
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
        this.toaster.success('Company updated');
        this.router.navigate(['/companies']);
      });
    } else {
      this.service.create(dto).subscribe(() => {
        this.toaster.success('Company created');
        this.router.navigate(['/companies']);
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/companies']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}