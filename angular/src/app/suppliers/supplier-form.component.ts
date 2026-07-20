import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe , RestService } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { SupplierService } from '../proxy/purchasing/supplier.service';
import { ToasterService } from '@abp/ng.theme.shared';

import { AutoValidationDirective } from '../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-supplier-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule, RouterModule],
  templateUrl: './supplier-form.component.html',
  styleUrls: ['./supplier-form.component.scss'],
})
export class SupplierFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private restService = inject(RestService);
  private service = inject(SupplierService);
  private toaster = inject(ToasterService);

  outstandingInvoices = signal<any[]>([]);
  totalOutstanding = signal(0);
  companies = signal<any[]>([]);

  form = this.fb.group({
    companyId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    supplierCode: [''],
    tin: [''],
    registrationNumber: [''],
    sstRegistrationNumber: [''],
    idType: ['BRN'],
    idValue: [''],
    contactPerson: [''],
    phone: [''],
    email: ['', Validators.email],
    address: [''],
    city: [''],
    state: [''],
    postalCode: [''],
    country: ['MYS'],
    isActive: [true],
  });

  isEditMode = false;
  entityId: string | null = null;

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.restService.request<any, any>({ method: 'GET', url: '/api/app/company', params: { skipCount: '0', maxResultCount: '100', sorting: '' } }, { apiName: 'Default' })
      .subscribe(res => this.companies.set(res.items ?? []));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((supplier) => {
        this.form.patchValue(supplier as any);
      });
      // Load outstanding payables
      this.restService.request<any, any>({ method: 'GET', url: '/api/app/payment-reconciliation/outstanding-invoices', params: { partyType: 'Supplier', partyId: this.entityId! } }, { apiName: 'Default' }).subscribe(invoices => {
        const items = invoices ?? [];
        this.outstandingInvoices.set(items);
        this.totalOutstanding.set(items.reduce((sum: number, i: any) => sum + (i.outstandingAmount ?? 0), 0));
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue() as any;

    const op = this.isEditMode
      ? this.service.update(this.entityId!, value)
      : this.service.create(value);

    op.subscribe({
      next: () => {
        this.toaster.success(this.isEditMode ? 'Supplier updated' : 'Supplier created');
        this.router.navigate(['/suppliers']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}