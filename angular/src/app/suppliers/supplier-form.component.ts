import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { SupplierService } from '../proxy/purchasing/supplier.service';
import { CompanyService } from '../proxy/core/company.service';
import { PaymentReconciliationService } from '../proxy/accounting/payment-reconciliation.service';
import { HierarchyMasterDataService } from '../proxy/core/hierarchy-master-data.service';
import { MasterDataService } from '../proxy/core/master-data.service';
import { AccountService } from '../proxy/accounting/account.service';
import { ToasterService } from '@abp/ng.theme.shared';
import type { HierarchyNodeDto, PaymentTermsLookupDto } from '../proxy/core/models';
import type { AccountDto } from '../proxy/accounting/models';

import { AutoValidationDirective } from '../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../shared/directives/save-shortcut.directive';
import { CompanyRestrictionComponent } from '../shared/components/company-restriction/company-restriction.component';

@Component({
  selector: 'app-supplier-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, CompanyRestrictionComponent, CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule, RouterModule],
  templateUrl: './supplier-form.component.html',
  styleUrls: ['./supplier-form.component.scss'],
})
export class SupplierFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(SupplierService);
  private companyService = inject(CompanyService);
  private paymentReconciliationService = inject(PaymentReconciliationService);
  private hierarchyService = inject(HierarchyMasterDataService);
  private masterDataService = inject(MasterDataService);
  private accountService = inject(AccountService);
  private toaster = inject(ToasterService);

  outstandingInvoices = signal<any[]>([]);
  totalOutstanding = signal(0);
  companies = signal<any[]>([]);
  supplierGroups = signal<HierarchyNodeDto[]>([]);
  paymentTerms = signal<PaymentTermsLookupDto[]>([]);
  accounts = signal<AccountDto[]>([]);

  form = this.fb.group({
    companyId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    supplierCode: [''],
    supplierGroupId: [null as string | null],
    representsCompanyId: [null as string | null],
    defaultPaymentTermsTemplateId: [null as string | null],
    defaultPayableAccountId: [null as string | null],
    holdType: [0],
    preventPurchaseOrders: [false],
    preventRfqs: [false],
    taxWithholdingCategory: [''],
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
    restrictToCompanies: [false],
  });

  isEditMode = false;
  entityId: string | null = null;

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe({ next: res => this.companies.set(res.items ?? []), error: () => {} });

    this.hierarchyService.getSupplierGroups()
      .subscribe({ next: groups => this.supplierGroups.set(groups ?? []), error: () => {} });

    this.masterDataService.getPaymentTerms()
      .subscribe({ next: terms => this.paymentTerms.set(terms ?? []), error: () => {} });

    this.accountService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' })
      .subscribe({ next: res => this.accounts.set(res.items ?? []), error: () => {} });

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((supplier) => {
        this.form.patchValue(supplier as any);
      });
      // Load outstanding payables
      this.paymentReconciliationService.getOutstandingInvoices('Supplier', this.entityId!).subscribe(invoices => {
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
