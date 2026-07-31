import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BankAccountService } from '../../proxy/accounting/bank-account.service';
import type { BankAccountDto } from '../../proxy/accounting/models';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-bank-account-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">
          <i class="bi bi-bank me-2"></i>{{ 'MyERP::BankAccounts' | abpLocalization }}
        </h5>
        <a routerLink="new" class="btn btn-primary btn-sm">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::NewBankAccount' | abpLocalization }}
        </a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm"
              [(ngModel)]="searchTerm" (keyup.enter)="loadData()"
              [placeholder]="'MyERP::Search' | abpLocalization" />
          </div>
        </div>

        <div class="table-responsive">
          <table class="table table-hover align-middle">
            <thead class="table-light">
              <tr>
                <th>{{ 'MyERP::AccountName' | abpLocalization }}</th>
                <th>{{ 'MyERP::BankName' | abpLocalization }}</th>
                <th>{{ 'MyERP::BankAccountNo' | abpLocalization }}</th>
                <th>{{ 'MyERP::Currency' | abpLocalization }}</th>
                <th class="text-center">{{ 'MyERP::Default' | abpLocalization }}</th>
                <th class="text-center">{{ 'MyERP::Status' | abpLocalization }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (account of accounts(); track account.id) {
                <tr [class.table-secondary]="account.isDisabled">
                  <td>
                    <a [routerLink]="[account.id]" class="text-decoration-none fw-medium">
                      {{ account.accountName }}
                    </a>
                    @if (account.isCreditCard) {
                      <span class="badge bg-info ms-1">Credit Card</span>
                    }
                  </td>
                  <td>{{ account.bankName }}</td>
                  <td class="font-monospace">{{ account.bankAccountNo || '—' }}</td>
                  <td>{{ account.currencyCode }}</td>
                  <td class="text-center">
                    @if (account.isDefault) {
                      <i class="bi bi-star-fill text-warning"></i>
                    }
                  </td>
                  <td class="text-center">
                    @if (account.isDisabled) {
                      <span class="badge bg-secondary">Disabled</span>
                    } @else {
                      <span class="badge bg-success">Active</span>
                    }
                  </td>
                  <td class="text-end">
                    <div class="btn-group btn-group-sm">
                      @if (!account.isDefault && !account.isDisabled) {
                        <button class="btn btn-outline-warning" title="Set as Default"
                          (click)="setDefault(account.id!)">
                          <i class="bi bi-star"></i>
                        </button>
                      }
                      @if (!account.isDisabled) {
                        <button class="btn btn-outline-danger" title="Disable"
                          (click)="disable(account.id!)">
                          <i class="bi bi-x-circle"></i>
                        </button>
                      }
                      <a [routerLink]="[account.id, 'edit']" class="btn btn-outline-primary">
                        <i class="bi bi-pencil"></i>
                      </a>
                    </div>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="7" class="text-center text-muted py-4">
                    <i class="bi bi-bank fa-2x mb-2 d-block"></i>
                    {{ 'MyERP::NoRecordsFound' | abpLocalization }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <app-pagination
          [totalCount]="totalCount()"
          [pageSize]="pageSize"
          [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      </div>
    </div>
  `,
})
export class BankAccountListComponent implements OnInit {
  private service = inject(BankAccountService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  accounts = signal<BankAccountDto[]>([]);
  totalCount = signal(0);
  searchTerm = '';
  currentPage = 0;
  pageSize = 20;

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.service.getList({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      filter: this.searchTerm || undefined,
      companyId: this.companyContext.currentCompanyId() || undefined,
    }).subscribe({
      next: (res) => {
        this.accounts.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
      },
    });
  }

  setDefault(id: string) {
    this.service.setAsDefault(id).subscribe({
      next: () => {
        this.toaster.success('MyERP::SuccessfullyUpdated');
        this.loadData();
      },
    });
  }

  disable(id: string) {
    this.confirmation.warn('MyERP::AreYouSure', 'MyERP::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.disable(id).subscribe({
          next: () => {
            this.toaster.success('MyERP::SuccessfullyUpdated');
            this.loadData();
          },
        });
      }
    });
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }
}
