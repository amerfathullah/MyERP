import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountService } from '../../proxy/accounting/account.service';
import { CompanyService } from '../../proxy/core/company.service';
import type { AccountDto } from '../../proxy/accounting/models';
import type { CompanyDto } from '../../proxy/core/models';
import { AccountType } from '../../proxy/accounting/account-type.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-account-form',
  standalone: true,
  imports: [
    AutoValidationDirective,
    CommonModule,
    ReactiveFormsModule,
    LocalizationPipe,
    PageModule],
  templateUrl: './account-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./account-form.component.scss'],
})
export class AccountFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private accountService = inject(AccountService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  form = this.fb.group({
    companyId: ['', Validators.required],
    accountCode: ['', [Validators.required, Validators.maxLength(20)]],
    accountName: ['', [Validators.required, Validators.maxLength(128)]],
    accountType: [AccountType.Asset, Validators.required],
    parentAccountId: [null as string | null],
    isGroup: [false],
    isFrozen: [false],
    isActive: [true],
    includeInGross: [false],
    currency: ['MYR'],
    description: [''],
  });

  isEditMode = false;
  entityId: string | null = null;
  companies = signal<CompanyDto[]>([]);
  parentAccounts = signal<AccountDto[]>([]);
  accountTypes = Object.entries(AccountType).filter(([, v]) => typeof v === 'number');

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe({ next: (res) => this.companies.set(res.items ?? []), error: () => {} });

    this.form.get('companyId')?.valueChanges.subscribe((companyId) => {
      this.loadParentAccounts(companyId || undefined);
    });

    if (this.isEditMode) {
      this.accountService.get(this.entityId!).subscribe((account) => {
        this.form.patchValue({
          companyId: account.companyId ?? '',
          accountCode: account.accountCode,
          accountName: account.accountName,
          accountType: account.accountType,
          parentAccountId: account.parentAccountId ?? null,
          isGroup: account.isGroup ?? false,
          isFrozen: account.isFrozen ?? false,
          isActive: account.isActive ?? true,
          includeInGross: account.includeInGross ?? false,
          currency: account.currency ?? 'MYR',
          description: account.description ?? '',
        });
        this.loadParentAccounts(account.companyId);
      });
    } else {
      const qCompanyId = this.route.snapshot.queryParams['companyId'] ?? this.companyContext.currentCompanyId() ?? '';
      const qParentId = this.route.snapshot.queryParams['parentAccountId'] ?? null;
      if (qCompanyId || qParentId) {
        this.form.patchValue({
          companyId: qCompanyId,
          parentAccountId: qParentId,
        });
      }
      this.loadParentAccounts(qCompanyId || undefined);
    }
  }

  private loadParentAccounts(companyId?: string): void {
    this.accountService.getList({
      companyId: companyId || undefined,
      isGroup: true,
      skipCount: 0,
      maxResultCount: 500,
      sorting: 'accountCode asc',
    }).subscribe({
      next: (result) => {
        const items = (result.items ?? []).filter(a => a.isGroup && a.id !== this.entityId);
        this.parentAccounts.set(items);
      },
      error: () => {},
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const dto = this.form.getRawValue() as any;
    dto.accountType = Number(dto.accountType);

    if (this.isEditMode) {
      this.accountService.update(this.entityId!, dto).subscribe({
        next: () => { this.form.markAsPristine(); this.router.navigate(['/accounting/accounts']); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::SaveFailed'),
      });
    } else {
      this.accountService.create(dto).subscribe({
        next: () => { this.form.markAsPristine(); this.router.navigate(['/accounting/accounts']); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::SaveFailed'),
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/accounting/accounts']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
