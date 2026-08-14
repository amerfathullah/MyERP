import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BankGuaranteeStore } from '../store/bank-guarantee.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { BankGuaranteeType } from '../../proxy/accounting/bank-guarantee-type.enum';

@Component({
  selector: 'app-bank-guarantee-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, StatusBadgeComponent, PaginationComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">
          <i class="bi bi-shield-check me-2"></i>{{ 'MyERP::BankGuarantees' | abpLocalization }}
        </h5>
        <a routerLink="new" class="btn btn-primary btn-sm">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::NewBankGuarantee' | abpLocalization }}
        </a>
      </div>
      <div class="card-body">
        @if (store.isLoading()) {
          <div class="text-center py-4">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ 'MyERP::GuaranteeNumber' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Type' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Party' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Bank' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::Amount' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Validity' | abpLocalization }}</th>
                  <th class="text-center">{{ 'MyERP::Status' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (bg of store.entities(); track bg.id) {
                  <tr>
                    <td>
                      <a [routerLink]="[bg.id, 'edit']" class="fw-semibold text-decoration-none">
                        {{ bg.bankGuaranteeNumber || ('MyERP::Draft' | abpLocalization) }}
                      </a>
                    </td>
                    <td>
                      <span class="badge" [ngClass]="bg.bgType === BankGuaranteeType.Receiving ? 'bg-info' : 'bg-primary'">
                        {{ bg.bgType === BankGuaranteeType.Receiving ? 'Receiving' : 'Providing' }}
                      </span>
                    </td>
                    <td>{{ bg.customerName || bg.supplierName || '-' }}</td>
                    <td>{{ bg.bank || '-' }}</td>
                    <td class="text-end fw-medium">{{ bg.amount | number:'1.2-2' }}</td>
                    <td>
                      <div>{{ bg.startDate | date:'mediumDate' }}</div>
                      @if (bg.endDate) {
                        <small class="text-muted">to {{ bg.endDate | date:'mediumDate' }}</small>
                      }
                    </td>
                    <td class="text-center">
                      <app-status-badge [status]="getStatusString(bg.status)" />
                    </td>
                    <td class="text-end">
                      <div class="btn-group btn-group-sm">
                        @if (bg.status === 0) {
                          <button class="btn btn-outline-success" (click)="submit(bg.id)" title="Submit">
                            <i class="bi bi-check-lg"></i>
                          </button>
                        }
                        @if (bg.status === 1) {
                          <button class="btn btn-outline-warning" (click)="cancel(bg.id)" title="Cancel">
                            <i class="bi bi-x-circle"></i>
                          </button>
                        }
                        <a [routerLink]="[bg.id, 'edit']" class="btn btn-outline-primary" title="Edit">
                          <i class="bi bi-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(bg.id)" title="Delete">
                          <i class="bi bi-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="8" class="text-center text-muted py-4">
                      {{ 'MyERP::NoDataAvailable' | abpLocalization }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <app-pagination
            [totalCount]="store.totalCount()"
            [pageSize]="pageSize"
            [currentPage]="pageIndex"
            (pageChange)="onPageChange($event)"
          />
        }
      </div>
    </div>
  `
})
export class BankGuaranteeListComponent implements OnInit {
  protected readonly store = inject(BankGuaranteeStore);
  private readonly confirmation = inject(ConfirmationService);
  protected readonly BankGuaranteeType = BankGuaranteeType;

  pageIndex = 0;
  pageSize = 10;

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.store.load({
      skipCount: this.pageIndex * this.pageSize,
      maxResultCount: this.pageSize,
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  submit(id: string): void {
    this.confirmation.warn('MyERP::SubmitConfirmationMessage', 'MyERP::Submit').subscribe((status: Confirmation.Status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.submit(id);
      }
    });
  }

  cancel(id: string): void {
    this.confirmation.warn('MyERP::CancelConfirmationMessage', 'MyERP::Cancel').subscribe((status: Confirmation.Status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.cancel(id);
      }
    });
  }

  delete(id: string): void {
    this.confirmation.warn('MyERP::DeleteConfirmationMessage', 'MyERP::Delete').subscribe((status: Confirmation.Status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.delete(id);
      }
    });
  }

  getStatusString(status: number): string {
    switch (status) {
      case 0: return 'Draft';
      case 1: return 'Submitted';
      case 4: return 'Cancelled';
      default: return 'Draft';
    }
  }
}
