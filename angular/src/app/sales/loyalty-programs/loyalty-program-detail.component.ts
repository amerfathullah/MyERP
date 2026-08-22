import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LoyaltyProgramService } from '../../proxy/sales/loyalty-program.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import type { LoyaltyBalanceDto, LoyaltyPointEntryDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-loyalty-program-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="program?.name ?? 'Loyalty Program'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (program) {
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'ConversionFactor' | abpLocalization }}</div>
              <div class="fs-4 fw-bold">{{ program.conversionFactor }}</div>
              <div class="text-muted small">amount per point</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'ExpiryDays' | abpLocalization }}</div>
              <div class="fs-4 fw-bold">{{ program.expiryDurationDays > 0 ? program.expiryDurationDays : '∞' }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Tiers' | abpLocalization }}</div>
              <div class="fs-4 fw-bold">{{ program.tiers?.length ?? 0 }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Status' | abpLocalization }}</div>
              <span class="badge fs-6" [class]="program.isEnabled ? 'bg-success' : 'bg-secondary'">
                {{ program.isEnabled ? 'Active' : 'Disabled' }}
              </span>
            </div></div>
          </div>
        </div>

        <div class="card mb-4"><div class="card-header"><h6 class="mb-0">{{ 'Tiers' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'TierName' | abpLocalization }}</th>
                <th class="text-end">Min Spent</th>
                <th class="text-end">Collection Factor</th>
                <th class="text-end">Redemption Factor</th>
              </tr></thead>
              <tbody>
                @for (tier of program.tiers; track tier.name) {
                  <tr>
                    <td><strong>{{ tier.name }}</strong></td>
                    <td class="text-end">{{ tier.minSpent | number:'1.2-2' }}</td>
                    <td class="text-end">{{ tier.collectionFactor }}×</td>
                    <td class="text-end">{{ tier.redemptionFactor }}</td>
                  </tr>
                }
                @empty {
                  <tr><td colspan="4" class="text-center text-muted py-3">{{ '::NoTiersConfigured' | abpLocalization }}</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        @if (program.expenseAccountId) {
          <div class="card mb-4"><div class="card-body">
            <small class="text-muted">Expense Account ID: {{ program.expenseAccountId }}</small>
          </div></div>
        }

        <div class="card">
          <div class="card-header"><h6 class="mb-0">{{ 'CustomerBalanceLookup' | abpLocalization }}</h6></div>
          <div class="card-body">
            <div class="row g-2 align-items-end mb-3">
              <div class="col-md-6">
                <label class="form-label">{{ '::Customer' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="selectedCustomerId">
                  <option value="">{{ '::Select' | abpLocalization }}</option>
                  @for (c of customers; track c.id) {
                    <option [value]="c.id">{{ c.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-3">
                <button class="btn btn-primary btn-sm" [disabled]="!selectedCustomerId || lookupLoading" (click)="lookupBalance()">
                  <i class="fa fa-search me-1"></i>{{ '::Search' | abpLocalization }}
                </button>
              </div>
            </div>

            @if (balance) {
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <div class="card text-center h-100"><div class="card-body">
                    <div class="text-muted small">{{ 'AvailablePoints' | abpLocalization }}</div>
                    <div class="fs-4 fw-bold">{{ balance.availablePoints }}</div>
                  </div></div>
                </div>
                <div class="col-md-4">
                  <div class="card text-center h-100"><div class="card-body">
                    <div class="text-muted small">{{ 'CurrentTier' | abpLocalization }}</div>
                    <div class="fs-4 fw-bold">{{ balance.currentTier ?? '—' }}</div>
                  </div></div>
                </div>
                <div class="col-md-4">
                  <div class="card text-center h-100"><div class="card-body">
                    <div class="text-muted small">{{ 'RedemptionValue' | abpLocalization }}</div>
                    <div class="fs-4 fw-bold">{{ balance.redemptionValue | number:'1.2-2' }}</div>
                  </div></div>
                </div>
              </div>

              <div class="d-flex gap-2 align-items-end mb-3">
                <div>
                  <label class="form-label">{{ 'PointsToRedeem' | abpLocalization }}</label>
                  <input type="number" min="1" [max]="balance.availablePoints ?? null" class="form-control form-control-sm" [(ngModel)]="pointsToRedeem" />
                </div>
                <button class="btn btn-outline-success btn-sm" [disabled]="!pointsToRedeem || pointsToRedeem <= 0 || redeeming" (click)="redeemPoints()">
                  <i class="fa fa-gift me-1"></i>{{ 'RedeemPoints' | abpLocalization }}
                </button>
              </div>

              <h6>{{ 'PointHistory' | abpLocalization }}</h6>
              <div class="table-responsive">
                <table class="table table-sm table-hover mb-0">
                  <thead><tr>
                    <th>{{ '::Date' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Points' | abpLocalization }}</th>
                    <th>{{ 'TierName' | abpLocalization }}</th>
                    <th>{{ '::Status' | abpLocalization }}</th>
                  </tr></thead>
                  <tbody>
                    @for (e of pointHistory; track e.id) {
                      <tr>
                        <td>{{ e.postingDate | date:'dd/MM/yyyy' }}</td>
                        <td class="text-end" [class.text-success]="e.isEarning" [class.text-danger]="!e.isEarning">{{ e.points }}</td>
                        <td>{{ e.tierName ?? '—' }}</td>
                        <td>@if (e.isExpired) { <span class="badge bg-secondary">{{ '::Expired' | abpLocalization }}</span> }</td>
                      </tr>
                    } @empty {
                      <tr><td colspan="4" class="text-center text-muted py-3">{{ '::NoData' | abpLocalization }}</td></tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </div>
        </div>
      }
    </abp-page>
  `
})
export class LoyaltyProgramDetailComponent implements OnInit {
  private service = inject(LoyaltyProgramService);
  private customerService = inject(CustomerService);
  private toaster = inject(ToasterService);
  private route = inject(ActivatedRoute);
  program: any = null;
  isLoading = false;

  customers: any[] = [];
  selectedCustomerId = '';
  balance: LoyaltyBalanceDto | null = null;
  pointHistory: LoyaltyPointEntryDto[] = [];
  lookupLoading = false;
  pointsToRedeem: number | null = null;
  redeeming = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.service.get(id).subscribe({
        next: p => { this.program = p; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' } as any)
      .subscribe({ next: (res: any) => this.customers = res.items ?? [], error: () => {} });
  }

  lookupBalance() {
    if (!this.selectedCustomerId || !this.program?.id) return;
    this.lookupLoading = true;
    this.service.getCustomerBalance(this.selectedCustomerId, this.program.id).subscribe({
      next: (res) => {
        this.balance = res;
        this.lookupLoading = false;
        this.loadPointHistory();
      },
      error: (err: any) => {
        this.lookupLoading = false;
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }

  loadPointHistory() {
    if (!this.selectedCustomerId || !this.program?.id) return;
    this.service.getPointHistory(this.selectedCustomerId, this.program.id).subscribe({
      next: (res) => this.pointHistory = res ?? [],
      error: () => {},
    });
  }

  redeemPoints() {
    if (!this.selectedCustomerId || !this.program?.id || !this.pointsToRedeem) return;
    this.redeeming = true;
    this.service.redeemPoints(this.selectedCustomerId, this.program.id, this.pointsToRedeem, this.program.companyId).subscribe({
      next: (value: number) => {
        this.redeeming = false;
        this.pointsToRedeem = null;
        this.toaster.success(this.formatRedeemSuccess(value));
        this.lookupBalance();
      },
      error: (err: any) => {
        this.redeeming = false;
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }

  private formatRedeemSuccess(value: number): string {
    return `Redeemed for ${value.toFixed(2)}`;
  }
}
