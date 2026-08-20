import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../proxy/core/company.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';
import type { CompanyDto } from '../../proxy/core/models';
import type { AccountDto, CostCenterDto } from '../../proxy/accounting/models';
import { ValuationMethod } from '../../proxy/inventory/valuation-method.enum';

@Component({
  selector: 'app-company-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './company-settings.component.html',
  styleUrls: ['./company-settings.component.scss'],
})
export class CompanySettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private companyService = inject(CompanyService);
  private accountService = inject(AccountService);
  private costCenterService = inject(CostCenterService);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  accounts = signal<AccountDto[]>([]);
  costCenters = signal<CostCenterDto[]>([]);
  selectedCompany = signal<CompanyDto | null>(null);
  isLoading = signal(false);

  form = this.fb.group({
    defaultCurrency: ['MYR'],
    fiscalYearStartMonth: [1],
    stockFrozenUpto: [''],
    accountsFrozenTillDate: [''],
    enablePerpetualInventory: [true],
    defaultValuationMethod: ['FIFO'],
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
    defaultCostCenterId: [''],
  });

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => this.companies.set(res.items ?? []));
  }

  onCompanySelect(companyId: string): void {
    const company = this.companies().find(c => c.id === companyId);
    if (!company) return;
    this.selectedCompany.set(company);
    this.form.patchValue({
      defaultCurrency: company.currencyCode ?? 'MYR',
      stockFrozenUpto: company.stockFrozenUpto?.substring(0, 10) ?? '',
      accountsFrozenTillDate: company.accountsFrozenTillDate?.substring(0, 10) ?? '',
      enablePerpetualInventory: company.enablePerpetualInventory ?? true,
      defaultValuationMethod: company.defaultValuationMethod != null
        ? ValuationMethod[company.defaultValuationMethod]
        : 'FIFO',
      overDeliveryAllowance: company.overDeliveryReceiptAllowance ?? 0,
      overBillingAllowance: company.overBillingAllowance ?? 0,
      defaultReceivableAccountId: company.defaultReceivableAccountId ?? '',
      defaultPayableAccountId: company.defaultPayableAccountId ?? '',
      defaultIncomeAccountId: company.defaultIncomeAccountId ?? '',
      defaultExpenseAccountId: company.defaultExpenseAccountId ?? '',
      defaultBankAccountId: company.defaultBankAccountId ?? '',
      defaultInventoryAccountId: company.defaultInventoryAccountId ?? '',
      depreciationExpenseAccountId: company.depreciationExpenseAccountId ?? '',
      accumulatedDepreciationAccountId: company.accumulatedDepreciationAccountId ?? '',
      exchangeGainLossAccountId: company.exchangeGainLossAccountId ?? '',
      defaultCostCenterId: company.defaultCostCenterId ?? '',
    });
    // Load accounts and cost centers for this company
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe(res => this.accounts.set(res.items ?? []));
    this.costCenterService.getList({ companyId, skipCount: 0, maxResultCount: 200, sorting: 'name asc' } as any)
      .subscribe(res => this.costCenters.set(res.items ?? []));
  }

  save(): void {
    const company = this.selectedCompany();
    if (!company) return;
    this.isLoading.set(true);
    const values = this.form.getRawValue();
    this.companyService.updateSettings(company.id!, values as any).subscribe({
      next: () => { this.toaster.success('::SuccessfullySaved'); this.isLoading.set(false); },
      error: () => { this.toaster.error('::SaveFailed'); this.isLoading.set(false); },
    });
  }
}
