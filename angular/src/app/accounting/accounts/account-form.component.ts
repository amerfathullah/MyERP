import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { AccountService } from '../../proxy/accounting/account.service';
import type { AccountDto } from '../../proxy/accounting/models';
import { AccountType } from '../../proxy/accounting/account-type.enum';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-account-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule,
    ReactiveFormsModule,
    LocalizationPipe,
    PageModule],
  templateUrl: './account-form.component.html',
  styleUrls: ['./account-form.component.scss'],
})
export class AccountFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private accountService = inject(AccountService);

  form = this.fb.group({
    accountCode: ['', [Validators.required, Validators.maxLength(20)]],
    accountName: ['', [Validators.required, Validators.maxLength(128)]],
    accountType: [AccountType.Asset, Validators.required],
    parentAccountId: [null as string | null],
    isGroup: [false],
    currency: ['MYR'],
    description: [''],
  });

  isEditMode = false;
  entityId: string | null = null;
  parentAccounts: AccountDto[] = [];
  accountTypes = Object.entries(AccountType).filter(([, v]) => typeof v === 'number');

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    // Load parent accounts for dropdown
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe((result) => {
        this.parentAccounts = (result.items ?? []).filter(a => a.isGroup);
      });

    if (this.isEditMode) {
      this.accountService.get(this.entityId!).subscribe((account) => {
        this.form.patchValue({
          accountCode: account.accountCode,
          accountName: account.accountName,
          accountType: account.accountType,
          parentAccountId: account.parentAccountId,
          isGroup: account.isGroup,
          currency: account.currency,
          description: account.description,
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
      this.accountService.update(this.entityId!, dto).subscribe(() => {
        this.router.navigate(['/accounting/accounts']);
      });
    } else {
      this.accountService.create(dto).subscribe(() => {
        this.router.navigate(['/accounting/accounts']);
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/accounting/accounts']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}