import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ExchangeRateRevaluationService } from '../../proxy/accounting/exchange-rate-revaluation.service';

@Component({
  selector: 'app-exchange-rate-revaluation',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'ExchangeRateRevaluation' | abpLocalization">
      <div class="card mb-3"><div class="card-body">
        <h6 class="card-title">{{ 'CreateRevaluation' | abpLocalization }}</h6>
        <div class="row g-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="postingDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">Rounding Loss Allowance</label>
            <input type="number" class="form-control" [(ngModel)]="roundingAllowance" step="0.01" min="0" max="0.99" />
          </div>
          <div class="col-md-4 d-flex align-items-end">
            <button class="btn btn-primary btn-sm" (click)="getEligibleAccounts()" [disabled]="isLoading">
              <i class="fa fa-search me-1"></i>Get Eligible Accounts
            </button>
          </div>
        </div>
      </div></div>

      @if (isLoading) { <app-loading-overlay /> }

      @if (eligibleAccounts.length > 0) {
        <div class="card mb-3"><div class="card-body">
          <h6 class="card-title">Eligible Accounts ({{ eligibleAccounts.length }})</h6>
          <table class="table table-sm mb-3">
            <thead><tr>
              <th>Account</th>
              <th>Currency</th>
              <th class="text-end">Balance (Account Currency)</th>
              <th class="text-end">Current Rate</th>
              <th class="text-end">New Rate</th>
              <th class="text-end">Gain/Loss</th>
            </tr></thead>
            <tbody>
              @for (acc of eligibleAccounts; track acc.accountId) {
                <tr>
                  <td>{{ acc.accountName }}</td>
                  <td><span class="badge bg-info">{{ acc.accountCurrency }}</span></td>
                  <td class="text-end">{{ acc.balanceInAccountCurrency | number:'1.2-2' }}</td>
                  <td class="text-end">{{ acc.currentExchangeRate | number:'1.4-4' }}</td>
                  <td class="text-end">{{ acc.newExchangeRate | number:'1.4-4' }}</td>
                  <td class="text-end" [class.text-success]="acc.gainLoss > 0" [class.text-danger]="acc.gainLoss < 0">
                    {{ acc.gainLoss | number:'1.2-2' }}
                  </td>
                </tr>
              }
            </tbody>
            <tfoot><tr>
              <td colspan="5" class="text-end fw-bold">Total Gain/Loss:</td>
              <td class="text-end fw-bold" [class.text-success]="totalGainLoss > 0" [class.text-danger]="totalGainLoss < 0">
                {{ totalGainLoss | number:'1.2-2' }}
              </td>
            </tr></tfoot>
          </table>
          <button class="btn btn-success btn-sm" (click)="createRevaluation()" [disabled]="isCreating">
            <i class="fa fa-check me-1"></i>Create Revaluation Entry
          </button>
        </div></div>
      }

      @if (revaluations.length > 0) {
        <div class="card"><div class="card-body">
          <h6 class="card-title">Previous Revaluations</h6>
          <table class="table table-sm mb-0">
            <thead><tr>
              <th>{{ 'PostingDate' | abpLocalization }}</th>
              <th class="text-end">Total Gain/Loss</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (r of revaluations; track r.id) {
                <tr>
                  <td>{{ r.postingDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end" [class.text-success]="r.totalGainLoss > 0" [class.text-danger]="r.totalGainLoss < 0">
                    {{ r.totalGainLoss | number:'1.2-2' }}
                  </td>
                  <td>
                    @if (r.status === 1) { <span class="badge bg-success">Submitted</span> }
                    @else if (r.status === 2) { <span class="badge bg-secondary">Cancelled</span> }
                    @else { <span class="badge bg-warning text-dark">Draft</span> }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `
})
export class ExchangeRateRevaluationComponent implements OnInit {
  private revaluationService = inject(ExchangeRateRevaluationService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  postingDate = new Date().toISOString().slice(0, 10);
  roundingAllowance = 0.05;
  eligibleAccounts: any[] = [];
  revaluations: any[] = [];
  isLoading = false;
  isCreating = false;
  totalGainLoss = 0;

  ngOnInit() { this.loadHistory(); }

  loadHistory() {
    const cid = this.companyContext.currentCompanyId();
    if (!cid) return;
    this.revaluationService.getList(cid, 20).subscribe({
      next: res => { this.revaluations = res.items ?? []; }
    });
  }

  getEligibleAccounts() {
    const cid = this.companyContext.currentCompanyId();
    if (!cid) { this.toaster.warn('Select a company first'); return; }
    this.isLoading = true;
    this.revaluationService.getEligibleAccounts(cid, '', this.postingDate).subscribe({
      next: accounts => {
        this.eligibleAccounts = accounts ?? [];
        this.totalGainLoss = this.eligibleAccounts.reduce((sum: number, a: any) => sum + (a.gainLoss ?? 0), 0);
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  createRevaluation() {
    const cid = this.companyContext.currentCompanyId();
    if (!cid) return;
    this.isCreating = true;
    this.revaluationService.createRevaluation({
      companyId: cid, postingDate: this.postingDate, roundingLossAllowance: this.roundingAllowance
    } as any).subscribe({
      next: () => {
        this.toaster.success('Revaluation entry created');
        this.isCreating = false;
        this.eligibleAccounts = [];
        this.loadHistory();
      },
      error: () => { this.isCreating = false; }
    });
  }
}
