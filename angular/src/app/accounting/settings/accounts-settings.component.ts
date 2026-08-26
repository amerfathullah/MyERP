import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountsSettingsService } from '../../proxy/accounting/accounts-settings.service';

@Component({
  selector: 'app-accounts-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Accounts Settings</h5>
        <button type="button" class="btn btn-primary btn-sm" (click)="save()">Save Settings</button>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <ul class="nav nav-tabs mb-4" role="tablist">
            <li class="nav-item" role="presentation">
              <button class="nav-link active" id="invoicing-tab" data-bs-toggle="tab" data-bs-target="#invoicing" type="button" role="tab">Invoicing & Billing</button>
            </li>
            <li class="nav-item" role="presentation">
              <button class="nav-link" id="journals-tab" data-bs-toggle="tab" data-bs-target="#journals" type="button" role="tab">Journals & Taxes</button>
            </li>
            <li class="nav-item" role="presentation">
              <button class="nav-link" id="payments-tab" data-bs-toggle="tab" data-bs-target="#payments" type="button" role="tab">Payments & Banking</button>
            </li>
            <li class="nav-item" role="presentation">
              <button class="nav-link" id="assets-tab" data-bs-toggle="tab" data-bs-target="#assets" type="button" role="tab">Assets & Reports</button>
            </li>
          </ul>

          <div class="tab-content">
            <!-- Invoicing Tab -->
            <div class="tab-pane fade show active" id="invoicing" role="tabpanel">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="unlinkPaymentOnCancellationOfInvoice" formControlName="unlinkPaymentOnCancellationOfInvoice">
                    <label class="form-check-label" for="unlinkPaymentOnCancellationOfInvoice">Unlink Payment on Cancellation of Invoice</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="unlinkAdvancePaymentOnCancellationOfOrder" formControlName="unlinkAdvancePaymentOnCancellationOfOrder">
                    <label class="form-check-label" for="unlinkAdvancePaymentOnCancellationOfOrder">Unlink Advance Payment on Cancellation of Order</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="checkSupplierInvoiceUniqueness" formControlName="checkSupplierInvoiceUniqueness">
                    <label class="form-check-label" for="checkSupplierInvoiceUniqueness">Check Supplier Invoice Number Uniqueness</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="automaticallyFetchPaymentTerms" formControlName="automaticallyFetchPaymentTerms">
                    <label class="form-check-label" for="automaticallyFetchPaymentTerms">Automatically Fetch Payment Terms from Order</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enableSubscription" formControlName="enableSubscription">
                    <label class="form-check-label" for="enableSubscription">Enable Subscription Tracking</label>
                  </div>
                </div>

                <div class="col-md-6 mb-3">
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enableCommonPartyAccounting" formControlName="enableCommonPartyAccounting">
                    <label class="form-check-label" for="enableCommonPartyAccounting">Enable Common Party Accounting</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="allowMultiCurrencyInvoicesAgainstSinglePartyAccount" formControlName="allowMultiCurrencyInvoicesAgainstSinglePartyAccount">
                    <label class="form-check-label" for="allowMultiCurrencyInvoicesAgainstSinglePartyAccount">Allow Multi-Currency Invoices</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="confirmBeforeResettingPostingDate" formControlName="confirmBeforeResettingPostingDate">
                    <label class="form-check-label" for="confirmBeforeResettingPostingDate">Confirm Before Resetting Posting Date</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="bookStockExpenseGlEntries" formControlName="bookStockExpenseGlEntries">
                    <label class="form-check-label" for="bookStockExpenseGlEntries">Book Stock Expense GL Entries</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enableDiscountsAndMargin" formControlName="enableDiscountsAndMargin">
                    <label class="form-check-label" for="enableDiscountsAndMargin">Enable Discounts and Margin</label>
                  </div>
                </div>
              </div>

              <div class="row border-top pt-3 mt-2">
                <div class="col-md-4 mb-3">
                  <label class="form-label">Over-Billing Allowance (%)</label>
                  <input type="number" class="form-control" formControlName="overBillingAllowance">
                </div>
                <div class="col-md-4 mb-3">
                  <label class="form-label">Credit Controller Role</label>
                  <input type="text" class="form-control" formControlName="creditControllerRole" placeholder="Accounts Manager">
                </div>
                <div class="col-md-4 mb-3">
                  <div class="form-check form-switch mt-4">
                    <input class="form-check-input" type="checkbox" id="enableOverdueBillingThreshold" formControlName="enableOverdueBillingThreshold">
                    <label class="form-check-label" for="enableOverdueBillingThreshold">Block Overdue Customers</label>
                  </div>
                </div>
              </div>
            </div>

            <!-- Journals & Taxes Tab -->
            <div class="tab-pane fade" id="journals" role="tabpanel">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">Deferred Accounting</h6>
                  <div class="mb-3">
                    <label class="form-label">Book Deferred Entries Based On</label>
                    <select class="form-select" formControlName="bookDeferredEntriesBasedOn">
                      <option value="Days">Days</option>
                      <option value="Months">Months</option>
                    </select>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="automaticallyProcessDeferredAccountingEntry" formControlName="automaticallyProcessDeferredAccountingEntry">
                    <label class="form-check-label" for="automaticallyProcessDeferredAccountingEntry">Auto Process Deferred Entries</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="bookDeferredEntriesViaJournalEntry" formControlName="bookDeferredEntriesViaJournalEntry">
                    <label class="form-check-label" for="bookDeferredEntriesViaJournalEntry">Book via Journal Entry</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="submitJournalEntries" formControlName="submitJournalEntries">
                    <label class="form-check-label" for="submitJournalEntries">Auto-Submit Generated Journal Entries</label>
                  </div>
                </div>

                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">Taxes & Charges</h6>
                  <div class="mb-3">
                    <label class="form-label">Determine Address Tax Category From</label>
                    <select class="form-select" formControlName="determineAddressTaxCategoryFrom">
                      <option value="Billing Address">Billing Address</option>
                      <option value="Shipping Address">Shipping Address</option>
                    </select>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="addTaxesFromItemTaxTemplate" formControlName="addTaxesFromItemTaxTemplate">
                    <label class="form-check-label" for="addTaxesFromItemTaxTemplate">Add Taxes from Item Tax Template</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="addTaxesFromTaxesAndChargesTemplate" formControlName="addTaxesFromTaxesAndChargesTemplate">
                    <label class="form-check-label" for="addTaxesFromTaxesAndChargesTemplate">Add Taxes from Taxes Template</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="roundRowWiseTax" formControlName="roundRowWiseTax">
                    <label class="form-check-label" for="roundRowWiseTax">Round Tax Amount Row-Wise</label>
                  </div>
                </div>
              </div>
            </div>

            <!-- Payments & Banking Tab -->
            <div class="tab-pane fade" id="payments" role="tabpanel">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">Currency Exchange</h6>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="allowStaleExchangeRates" formControlName="allowStaleExchangeRates">
                    <label class="form-check-label" for="allowStaleExchangeRates">Allow Stale Exchange Rates</label>
                  </div>
                  <div class="mb-3">
                    <label class="form-label">Stale Days</label>
                    <input type="number" class="form-control" formControlName="staleDays">
                  </div>
                </div>

                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">Bank Reconciliation & Matching</h6>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enablePartyMatching" formControlName="enablePartyMatching">
                    <label class="form-check-label" for="enablePartyMatching">Enable Automatic Party Matching</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enableFuzzyMatching" formControlName="enableFuzzyMatching">
                    <label class="form-check-label" for="enableFuzzyMatching">Enable Fuzzy Matching</label>
                  </div>
                  <div class="mb-3">
                    <label class="form-label">Match Transfers Within (Days)</label>
                    <input type="number" class="form-control" formControlName="transferMatchDays">
                  </div>
                </div>
              </div>
            </div>

            <!-- Assets & Reports Tab -->
            <div class="tab-pane fade" id="assets" role="tabpanel">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">Assets</h6>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="bookAssetDepreciationEntryAutomatically" formControlName="bookAssetDepreciationEntryAutomatically">
                    <label class="form-check-label" for="bookAssetDepreciationEntryAutomatically">Book Depreciation Automatically</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="calculateDeprUsingTotalDays" formControlName="calculateDeprUsingTotalDays">
                    <label class="form-check-label" for="calculateDeprUsingTotalDays">Calculate Daily Depreciation Using Total Days</label>
                  </div>
                </div>

                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">Reports & Chart of Accounts</h6>
                  <div class="mb-3">
                    <label class="form-label">Default Ageing Range</label>
                    <input type="text" class="form-control" formControlName="defaultAgeingRange" placeholder="30, 60, 90, 120">
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="showBalanceInCoa" formControlName="showBalanceInCoa">
                    <label class="form-check-label" for="showBalanceInCoa">Show Balances in Chart of Accounts</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="createPrInDraftStatus" formControlName="createPrInDraftStatus">
                    <label class="form-check-label" for="createPrInDraftStatus">Create Payment Requests in Draft Status</label>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="mt-4 border-top pt-3">
            <button type="submit" class="btn btn-primary">Save Settings</button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class AccountsSettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(AccountsSettingsService);
  private toaster = inject(ToasterService);

  form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      unlinkPaymentOnCancellationOfInvoice: [true],
      unlinkAdvancePaymentOnCancellationOfOrder: [true],
      deleteLinkedLedgerEntries: [false],
      enableImmutableLedger: [false],
      checkSupplierInvoiceUniqueness: [false],
      automaticallyFetchPaymentTerms: [false],
      enableSubscription: [true],
      enableCommonPartyAccounting: [false],
      allowMultiCurrencyInvoicesAgainstSinglePartyAccount: [false],
      confirmBeforeResettingPostingDate: [true],
      bookStockExpenseGlEntries: [false],
      enableDiscountsAndMargin: [false],
      enableAccountingDimensions: [false],
      mergeSimilarAccountHeads: [false],
      bookDeferredEntriesBasedOn: ['Days'],
      automaticallyProcessDeferredAccountingEntry: [true],
      bookDeferredEntriesViaJournalEntry: [false],
      submitJournalEntries: [false],
      determineAddressTaxCategoryFrom: ['Billing Address'],
      addTaxesFromItemTaxTemplate: [true],
      addTaxesFromTaxesAndChargesTemplate: [false],
      bookTaxDiscountLoss: [false],
      roundRowWiseTax: [false],
      allowStaleExchangeRates: [true],
      staleDays: [1],
      autoReconcilePayments: [false],
      autoReconciliationJobTrigger: [15],
      reconciliationQueueSize: [5],
      overBillingAllowance: [0],
      creditControllerRole: [''],
      enableOverdueBillingThreshold: [false],
      roleAllowedToBypassOverdueBilling: [''],
      bookAssetDepreciationEntryAutomatically: [true],
      calculateDeprUsingTotalDays: [false],
      defaultAgeingRange: ['30, 60, 90, 120'],
      showBalanceInCoa: [true],
      enablePartyMatching: [false],
      enableFuzzyMatching: [false],
      transferMatchDays: [3],
      createPrInDraftStatus: [true],
    });
  }

  ngOnInit() {
    this.service.get().subscribe(res => {
      this.form.patchValue(res);
    });
  }

  save() {
    this.service.update(this.form.value).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
