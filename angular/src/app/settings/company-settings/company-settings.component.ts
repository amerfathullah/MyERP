import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe , RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../proxy/core/company.service';
import { AccountService } from '../../proxy/accounting/account.service';
import type { CompanyDto } from '../../proxy/core/models';
import type { AccountDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-company-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './company-settings.component.html',
  styleUrls: ['./company-settings.component.scss'],
})
export class CompanySettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private restService = inject(RestService);
  private companyService = inject(CompanyService);
  private accountService = inject(AccountService);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  accounts = signal<AccountDto[]>([]);
  selectedCompany = signal<CompanyDto | null>(null);
  isLoading = signal(false);

  form = this.fb.group({
    defaultCurrency: ['MYR'],
    fiscalYearStartMonth: [1],
    stockFrozenUpto: [''],
    accountsFrozenTillDate: [''],
    enablePerpetualInventory: [true],
    defaultValuationMethod: ['MovingAverage'],
    overDeliveryAllowance: [0],
    overBillingAllowance: [0],
    defaultReceivableAccountId: [''],
    defaultPayableAccountId: [''],
    defaultIncomeAccountId: [''],
    defaultExpenseAccountId: [''],
    defaultBankAccountId: [''],
    defaultInventoryAccountId: [''],
    depreciationExpenseAccountId: [''],
    accumulatedDepreciationAccountId: [''],
    exchangeGainLossAccountId: [''],
  });

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => this.companies.set(res.items ?? []));
  }

  onCompanySelect(companyId: string): void {
    const company = this.companies().find(c => c.id === companyId);
    if (!company) return;
    this.selectedCompany.set(company);
    const c = company as any;
    this.form.patchValue({
      defaultCurrency: c.defaultCurrency ?? 'MYR',
      stockFrozenUpto: c.stockFrozenUpto ?? '',
      accountsFrozenTillDate: c.accountsFrozenTillDate ?? '',
      defaultReceivableAccountId: c.defaultReceivableAccountId ?? '',
      defaultPayableAccountId: c.defaultPayableAccountId ?? '',
      defaultIncomeAccountId: c.defaultIncomeAccountId ?? '',
      defaultExpenseAccountId: c.defaultExpenseAccountId ?? '',
      defaultBankAccountId: c.defaultBankAccountId ?? '',
      defaultInventoryAccountId: c.defaultInventoryAccountId ?? '',
      depreciationExpenseAccountId: c.depreciationExpenseAccountId ?? '',
      accumulatedDepreciationAccountId: c.accumulatedDepreciationAccountId ?? '',
      exchangeGainLossAccountId: c.exchangeGainLossAccountId ?? '',
    });
    // Load accounts for this company
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe(res => this.accounts.set(res.items ?? []));
  }

  save(): void {
    const company = this.selectedCompany();
    if (!company) return;
    this.isLoading.set(true);
    const values = this.form.getRawValue();
    this.restService.request<any, void>({ method: 'PUT', url: `/api/app/company/${company.id}/settings`, body: values }, { apiName: 'Default' }).subscribe({
      next: () => { this.toaster.success('Settings saved'); this.isLoading.set(false); },
      error: () => { this.toaster.error('Failed to save'); this.isLoading.set(false); },
    });
  }
}
