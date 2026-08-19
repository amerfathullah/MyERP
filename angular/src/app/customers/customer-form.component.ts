import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { CustomerService } from '../proxy/sales/customer.service';
import { CompanyService } from '../proxy/core/company.service';
import { PaymentReconciliationService } from '../proxy/accounting/payment-reconciliation.service';
import { HierarchyMasterDataService } from '../proxy/core/hierarchy-master-data.service';
import { MasterDataService } from '../proxy/core/master-data.service';
import { AccountService } from '../proxy/accounting/account.service';
import { EInvoiceService } from '../proxy/einvoice/einvoice.service';
import { ToasterService } from '@abp/ng.theme.shared';
import type { HierarchyNodeDto, PaymentTermsLookupDto } from '../proxy/core/models';
import type { AccountDto } from '../proxy/accounting/models';

import { AutoValidationDirective } from '../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../shared/directives/save-shortcut.directive';
import { CompanyRestrictionComponent } from '../shared/components/company-restriction/company-restriction.component';

@Component({
  selector: 'app-customer-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, CompanyRestrictionComponent, CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule, RouterModule],
  templateUrl: './customer-form.component.html',
  styleUrls: ['./customer-form.component.scss'],
})
export class CustomerFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private companyService = inject(CompanyService);
  private reconciliationService = inject(PaymentReconciliationService);
  private hierarchyService = inject(HierarchyMasterDataService);
  private masterDataService = inject(MasterDataService);
  private accountService = inject(AccountService);
  private einvoiceService = inject(EInvoiceService);
  private service = inject(CustomerService);
  private toaster = inject(ToasterService);

  outstandingInvoices = signal<any[]>([]);
  totalOutstanding = signal(0);
  companies = signal<any[]>([]);
  customerGroups = signal<HierarchyNodeDto[]>([]);
  territories = signal<HierarchyNodeDto[]>([]);
  paymentTerms = signal<PaymentTermsLookupDto[]>([]);
  accounts = signal<AccountDto[]>([]);
  searchingTaxpayer = signal(false);

  form = this.fb.group({
    companyId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    customerCode: [''],
    customerGroupId: [null as string | null],
    territoryId: [null as string | null],
    representsCompanyId: [null as string | null],
    creditLimit: [0, [Validators.min(0)]],
    defaultPaymentTermsTemplateId: [null as string | null],
    defaultReceivableAccountId: [null as string | null],
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

    this.hierarchyService.getCustomerGroups()
      .subscribe({ next: groups => this.customerGroups.set(groups ?? []), error: () => {} });

    this.hierarchyService.getTerritories()
      .subscribe({ next: t => this.territories.set(t ?? []), error: () => {} });

    this.masterDataService.getPaymentTerms()
      .subscribe({ next: terms => this.paymentTerms.set(terms ?? []), error: () => {} });

    this.accountService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' })
      .subscribe({ next: res => this.accounts.set(res.items ?? []), error: () => {} });

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((customer) => {
        this.form.patchValue(customer as any);
      });
      // Load outstanding invoices for this customer
      this.reconciliationService.getOutstandingInvoices('Customer', this.entityId!)
        .subscribe(invoices => {
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
        this.toaster.success(this.isEditMode ? 'Customer updated' : 'Customer created');
        this.router.navigate(['/customers']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }

  verifyTaxpayer(): void {
    const idType = this.form.get('idType')?.value;
    const idValue = this.form.get('idValue')?.value;
    if (!idType || !idValue) {
      this.toaster.warn('Please enter ID Type and ID Value first.');
      return;
    }
    this.searchingTaxpayer.set(true);
    this.einvoiceService.searchTaxpayer({ idType, idValue }).subscribe({
      next: (res) => {
        this.searchingTaxpayer.set(false);
        if (res.isSuccess && res.tin) {
          this.form.patchValue({ tin: res.tin });
          if (res.name && !this.form.get('name')?.value) {
            this.form.patchValue({ name: res.name });
          }
          this.toaster.success(`TIN verified: ${res.tin}${res.name ? ' (' + res.name + ')' : ''}`);
        } else {
          this.toaster.error(res.errorMessage || 'Taxpayer not found with given ID');
        }
      },
      error: (err: any) => {
        this.searchingTaxpayer.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Taxpayer verification failed');
      }
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
