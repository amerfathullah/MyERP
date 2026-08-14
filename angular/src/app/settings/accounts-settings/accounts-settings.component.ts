import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ErpSettingsService } from '../../proxy/settings/erp-settings.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-accounts-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0"><i class="bi bi-calculator me-2"></i>{{ 'MyERP::AccountsSettings' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <form (ngSubmit)="save()">
            <h6 class="text-muted mb-3">Billing & Tolerance</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">Over-Billing Allowance (%)</label>
                <input type="number" class="form-control" min="0" max="100" step="0.1"
                  [(ngModel)]="settings['MyERP.Accounts.OverBillingAllowance']" name="overBilling" />
              </div>
              <div class="col-md-4">
                <label class="form-label">Repost Limit (per run)</label>
                <input type="number" class="form-control" min="1"
                  [(ngModel)]="settings['MyERP.Accounts.RepostLimit']" name="repostLimit" />
              </div>
              <div class="col-md-4">
                <label class="form-label">Deferred Entries Based On</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Accounts.DeferredBasedOn']" name="deferredBasis">
                  <option value="Days">Days (daily proration)</option>
                  <option value="Months">Months (monthly proration)</option>
                </select>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">Feature Toggles</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="discMargin"
                    [ngModel]="settings['MyERP.Accounts.EnableDiscountsAndMargin'] === 'true'"
                    (ngModelChange)="settings['MyERP.Accounts.EnableDiscountsAndMargin'] = $event ? 'true' : 'false'" name="discMargin" />
                  <label class="form-check-label" for="discMargin">Enable Discounts & Margin</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="roundRow"
                    [ngModel]="settings['MyERP.Accounts.RoundRowWiseTax'] === 'true'"
                    (ngModelChange)="settings['MyERP.Accounts.RoundRowWiseTax'] = $event ? 'true' : 'false'" name="roundRow" />
                  <label class="form-check-label" for="roundRow">Round Row-Wise Tax</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="autoReversal"
                    [ngModel]="settings['MyERP.Accounts.AutoCreateReversalEntry'] === 'true'"
                    (ngModelChange)="settings['MyERP.Accounts.AutoCreateReversalEntry'] = $event ? 'true' : 'false'" name="autoReversal" />
                  <label class="form-check-label" for="autoReversal">Auto-Create Reversal Entry</label>
                </div>
              </div>
            </div>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="stockExpense"
                    [ngModel]="settings['MyERP.Accounts.BookStockExpenseGL'] === 'true'"
                    (ngModelChange)="settings['MyERP.Accounts.BookStockExpenseGL'] = $event ? 'true' : 'false'" name="stockExpense" />
                  <label class="form-check-label" for="stockExpense">Book Stock Expense GL Entries</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="overdueBilling"
                    [ngModel]="settings['MyERP.Accounts.EnableOverdueBilling'] === 'true'"
                    (ngModelChange)="settings['MyERP.Accounts.EnableOverdueBilling'] = $event ? 'true' : 'false'" name="overdueBilling" />
                  <label class="form-check-label" for="overdueBilling">Enable Overdue Billing Threshold</label>
                </div>
              </div>
            </div>

            <div class="d-flex justify-content-end mt-4">
              <button type="submit" class="btn btn-primary" [disabled]="saving()">
                <i class="bi bi-check-lg me-1"></i>{{ 'MyERP::Save' | abpLocalization }}
              </button>
            </div>
          </form>
        }
      </div>
    </div>
  `,
})
export class AccountsSettingsComponent implements OnInit {
  private service = inject(ErpSettingsService);
  private toaster = inject(ToasterService);

  settings: Record<string, string> = {};
  loading = signal(true);
  saving = signal(false);

  ngOnInit() {
    this.service.getGroup('Accounts').subscribe({
      next: (data) => { this.settings = data; this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  save() {
    this.saving.set(true);
    this.service.update(this.settings).subscribe({
      next: () => { this.saving.set(false); this.toaster.success('MyERP::SuccessfullySaved'); },
      error: () => this.saving.set(false),
    });
  }
}
