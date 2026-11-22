import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../proxy/core/company.service';
import type { CompanyDto } from '../proxy/core/models';
import { EInvoiceService } from '../proxy/einvoice/einvoice.service';
import { EInvoiceSettingsService } from '../proxy/einvoice/einvoice-settings.service';

import { AutoValidationDirective } from '../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-company-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './company-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./company-form.component.scss'],
})
export class CompanyFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(CompanyService);
  private einvoiceService = inject(EInvoiceService, { optional: true });
  private einvoiceSettingsService = inject(EInvoiceSettingsService, { optional: true });
  private toaster = inject(ToasterService);

  isEditMode = false;
  entityId: string | null = null;
  searchingTaxpayer = signal(false);
  testingLhdnLogin = signal(false);
  groupCompanies = signal<CompanyDto[]>([]);

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
    parentCompanyId: [null as string | null],
    isGroup: [false],
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

    this.service.getList?.({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: res => {
        const candidates = (res.items ?? []).filter(c => c.isGroup && c.id !== this.entityId);
        this.groupCompanies.set(candidates);
      },
      error: () => {},
    });

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(company => {
        this.form.patchValue({
          ...(company as any),
          parentCompanyId: company.parentCompanyId ?? null,
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue() as any;
    const dto = {
      ...raw,
      parentCompanyId: raw.parentCompanyId || null,
    };

    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe({
        next: () => { this.form.markAsPristine(); this.toaster.success('::SuccessfullySaved'); this.router.navigate(['/companies']); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::SaveFailed'),
      });
    } else {
      this.service.create(dto).subscribe({
        next: (created) => {
          this.form.markAsPristine();
          this.service.setupNewCompany(created.id!).subscribe({
            next: () => { this.toaster.success('::SuccessfullySaved'); this.router.navigate(['/companies']); },
            error: () => {
              // Company row was created; only the default CoA/warehouses/rules seeding failed.
              // Don't block navigation on it — Setup can be retried from the company list/settings.
              this.toaster.warn('::CompanySetupIncomplete');
              this.router.navigate(['/companies']);
            },
          });
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::SaveFailed'),
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/companies']);
  }

  verifyTaxpayer(): void {
    if (!this.einvoiceService) return;
    const registrationNumber = (this.form.get('registrationNumber')?.value || '').trim();
    const companyName = (this.form.get('name')?.value || '').trim();
    const companyId = this.isEditMode && this.entityId ? this.entityId : undefined;

    if (!registrationNumber && !companyName && !companyId) {
      this.toaster.warn('Please enter SSM Registration Number or Company Name first.');
      return;
    }

    this.searchingTaxpayer.set(true);
    this.einvoiceService.searchTaxpayer({
      idType: registrationNumber ? 'BRN' : undefined,
      idValue: registrationNumber || undefined,
      taxpayerName: companyName || undefined,
      companyId,
    }).subscribe({
      next: (res) => {
        this.searchingTaxpayer.set(false);
        if (res.isSuccess && res.tin) {
          this.form.patchValue({ taxId: res.tin });
          if (!res.isPersisted) {
            this.form.get('taxId')?.markAsDirty();
          }
          const savedMsg = res.isPersisted ? ' (saved)' : '';
          this.toaster.success(`TIN verified${savedMsg}: ${res.tin}${res.name ? ' (' + res.name + ')' : ''}`);
        } else {
          this.toaster.error(res.errorMessage || 'Taxpayer not found with given company details');
        }
      },
      error: (err: any) => {
        this.searchingTaxpayer.set(false);
        this.toaster.error(err?.error?.error?.message || 'LHDN TIN lookup failed');
      },
    });
  }

  /** Per MyInvois public/js/company.js (custom_taxpayer_login): verifies LHDN Taxpayer OAuth token login. */
  testTaxpayerLogin(): void {
    if (!this.einvoiceSettingsService) return;
    this.testingLhdnLogin.set(true);
    this.einvoiceSettingsService.connect().subscribe({
      next: (res) => {
        this.testingLhdnLogin.set(false);
        if (res.isSuccess) {
          this.toaster.success('Access token fetched successfully!');
        } else {
          this.toaster.error(res.errorMessage || 'Failed to fetch access token.');
        }
      },
      error: (err: any) => {
        this.testingLhdnLogin.set(false);
        this.toaster.error(err?.error?.error?.message || 'Failed to fetch access token.');
      },
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}