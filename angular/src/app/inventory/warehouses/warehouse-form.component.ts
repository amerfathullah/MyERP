import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, switchMap } from 'rxjs';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { WarehouseAccountService } from '../../proxy/inventory/warehouse-account.service';
import { CompanyService } from '../../proxy/core/company.service';
import type { CompanyDto } from '../../proxy/core/models';
import { AccountService } from '../../proxy/accounting/account.service';
import type { AccountDto } from '../../proxy/accounting/models';
import { AccountSubType } from '../../proxy/accounting/account-sub-type.enum';
import { WarehouseType, warehouseTypeOptions } from '../../proxy/inventory/warehouse-type.enum';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-warehouse-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './warehouse-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./warehouse-form.component.scss'],
})
export class WarehouseFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(WarehouseService);
  private warehouseAccountService = inject(WarehouseAccountService);
  private companyService = inject(CompanyService);
  private accountService = inject(AccountService);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  accounts = signal<AccountDto[]>([]);
  allLeafAccounts = signal<AccountDto[]>([]);
  warehouseAccountId = signal<string | null>(null);
  isEditMode = false;
  entityId: string | null = null;
  warehouseTypeOptions = warehouseTypeOptions;

  form = this.fb.group({
    companyId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(256)]],
    warehouseCode: ['', Validators.maxLength(20)],
    address: ['', Validators.maxLength(500)],
    city: ['', Validators.maxLength(100)],
    state: ['', Validators.maxLength(100)],
    postalCode: ['', Validators.maxLength(10)],
    country: ['Malaysia', Validators.maxLength(100)],
    isGroup: [false],
    isActive: [true],
    warehouseType: [WarehouseType.Standard],
    defaultAccountId: [null as string | null],
    perpetualAccountId: [null as string | null],
    stockReceivedButNotBilledAccountId: [null as string | null],
    stockDeliveredButNotBilledAccountId: [null as string | null],
    stockAdjustmentAccountId: [null as string | null],
  });

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(r => this.companies.set(r.items ?? []));

    // Per ERPNext PR #59191 / warehouse.js: clear account if company changes
    this.form.get('companyId')?.valueChanges.subscribe(companyId => {
      this.form.patchValue({
        defaultAccountId: null,
        perpetualAccountId: null,
        stockReceivedButNotBilledAccountId: null,
        stockDeliveredButNotBilledAccountId: null,
        stockAdjustmentAccountId: null,
      }, { emitEvent: false });
      this.warehouseAccountId.set(null);
      if (companyId) {
        this.loadAccounts(companyId);
      } else {
        this.accounts.set([]);
        this.allLeafAccounts.set([]);
      }
    });

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(w => {
        this.form.patchValue(w as any, { emitEvent: false });
        if (w.companyId) {
          this.loadAccounts(w.companyId);
          this.loadWarehouseAccount(w.companyId, w.id!);
        }
      });
    }
  }

  private loadAccounts(companyId: string): void {
    this.accountService.getList({
      companyId,
      accountSubType: AccountSubType.Stock,
      isGroup: false,
      skipCount: 0,
      maxResultCount: 200,
      sorting: 'accountCode',
    }).subscribe(r => this.accounts.set(r.items ?? []));

    this.accountService.getList({
      companyId,
      isGroup: false,
      skipCount: 0,
      maxResultCount: 500,
      sorting: 'accountCode',
    }).subscribe(r => this.allLeafAccounts.set(r.items ?? []));
  }

  private loadWarehouseAccount(companyId: string, warehouseId: string): void {
    this.warehouseAccountService.getList(companyId).subscribe({
      next: res => {
        const match = (res.items ?? []).find(x => x.warehouseId === warehouseId);
        if (match) {
          this.warehouseAccountId.set(match.id ?? null);
          this.form.patchValue({
            perpetualAccountId: match.accountId ?? null,
            stockReceivedButNotBilledAccountId: match.stockReceivedButNotBilledAccountId ?? null,
            stockDeliveredButNotBilledAccountId: match.stockDeliveredButNotBilledAccountId ?? null,
            stockAdjustmentAccountId: match.stockAdjustmentAccountId ?? null,
          }, { emitEvent: false });
        }
      },
      error: () => {},
    });
  }

  clearPerpetualAccounts(): void {
    const waId = this.warehouseAccountId();
    const clearFormFields = () => {
      this.warehouseAccountId.set(null);
      this.form.patchValue({
        perpetualAccountId: null,
        stockReceivedButNotBilledAccountId: null,
        stockDeliveredButNotBilledAccountId: null,
        stockAdjustmentAccountId: null,
      });
    };

    if (!waId) {
      clearFormFields();
      return;
    }

    this.warehouseAccountService.delete(waId).subscribe({
      next: () => {
        clearFormFields();
        this.toaster.success('::SuccessfullyDeleted');
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.data?.detail || err?.error?.error?.message || '::OperationFailed'),
    });
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const raw = this.form.getRawValue();
    const {
      perpetualAccountId,
      stockReceivedButNotBilledAccountId,
      stockDeliveredButNotBilledAccountId,
      stockAdjustmentAccountId,
      ...warehouseDto
    } = raw as any;

    const save$ = this.isEditMode
      ? this.service.update(this.entityId!, warehouseDto)
      : this.service.create(warehouseDto);

    save$.pipe(
      switchMap(saved => {
        if (perpetualAccountId && saved.id && saved.companyId) {
          return this.warehouseAccountService.save({
            warehouseId: saved.id,
            companyId: saved.companyId,
            accountId: perpetualAccountId,
            stockReceivedButNotBilledAccountId,
            stockDeliveredButNotBilledAccountId,
            stockAdjustmentAccountId,
          });
        }
        const existingWaId = this.warehouseAccountId();
        if (!perpetualAccountId && existingWaId) {
          return this.warehouseAccountService.delete(existingWaId);
        }
        return of(null);
      })
    ).subscribe({
      next: () => {
        this.form.markAsPristine();
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/inventory/warehouses']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.data?.detail || err?.error?.error?.message || '::SaveFailed'),
    });
  }

  cancel(): void { this.router.navigate(['/inventory/warehouses']); }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}