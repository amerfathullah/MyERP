import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { AccountsSettingsService } from '../../proxy/accounting/accounts-settings.service';

@Component({
  selector: 'app-accounts-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::AccountsSettings' | abpLocalization }}</h5>
        <button type="button" class="btn btn-primary btn-sm" (click)="save()">{{ '::SaveSettings' | abpLocalization }}</button>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <ul class="nav nav-tabs mb-4" role="tablist">
            <li class="nav-item" role="presentation">
              <button class="nav-link active" id="invoicing-tab" data-bs-toggle="tab" data-bs-target="#invoicing" type="button" role="tab">{{ '::InvoicingAndBilling' | abpLocalization }}</button>
            </li>
            <li class="nav-item" role="presentation">
              <button class="nav-link" id="journals-tab" data-bs-toggle="tab" data-bs-target="#journals" type="button" role="tab">{{ '::JournalsAndTaxes' | abpLocalization }}</button>
            </li>
            <li class="nav-item" role="presentation">
              <button class="nav-link" id="payments-tab" data-bs-toggle="tab" data-bs-target="#payments" type="button" role="tab">{{ '::PaymentsAndBanking' | abpLocalization }}</button>
            </li>
            <li class="nav-item" role="presentation">
              <button class="nav-link" id="assets-tab" data-bs-toggle="tab" data-bs-target="#assets" type="button" role="tab">{{ '::AssetsAndReports' | abpLocalization }}</button>
            </li>
          </ul>

          <div class="tab-content">
            <!-- Invoicing Tab -->
            <div class="tab-pane fade show active" id="invoicing" role="tabpanel">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="unlinkPaymentOnCancellationOfInvoice" formControlName="unlinkPaymentOnCancellationOfInvoice">
                    <label class="form-check-label" for="unlinkPaymentOnCancellationOfInvoice">{{ '::UnlinkPaymentOnCancellationOfInvoice' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="unlinkAdvancePaymentOnCancellationOfOrder" formControlName="unlinkAdvancePaymentOnCancellationOfOrder">
                    <label class="form-check-label" for="unlinkAdvancePaymentOnCancellationOfOrder">{{ '::UnlinkAdvancePaymentOnCancellationOfOrder' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="checkSupplierInvoiceUniqueness" formControlName="checkSupplierInvoiceUniqueness">
                    <label class="form-check-label" for="checkSupplierInvoiceUniqueness">{{ '::CheckSupplierInvoiceUniqueness' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="automaticallyFetchPaymentTerms" formControlName="automaticallyFetchPaymentTerms">
                    <label class="form-check-label" for="automaticallyFetchPaymentTerms">{{ '::AutomaticallyFetchPaymentTerms' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enableSubscription" formControlName="enableSubscription">
                    <label class="form-check-label" for="enableSubscription">{{ '::EnableSubscriptionTracking' | abpLocalization }}</label>
                  </div>
                </div>

                <div class="col-md-6 mb-3">
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enableCommonPartyAccounting" formControlName="enableCommonPartyAccounting">
                    <label class="form-check-label" for="enableCommonPartyAccounting">{{ '::EnableCommonPartyAccounting' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="allowMultiCurrencyInvoicesAgainstSinglePartyAccount" formControlName="allowMultiCurrencyInvoicesAgainstSinglePartyAccount">
                    <label class="form-check-label" for="allowMultiCurrencyInvoicesAgainstSinglePartyAccount">{{ '::AllowMultiCurrencyInvoicesAgainstSinglePartyAccount' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="confirmBeforeResettingPostingDate" formControlName="confirmBeforeResettingPostingDate">
                    <label class="form-check-label" for="confirmBeforeResettingPostingDate">{{ '::ConfirmBeforeResettingPostingDate' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="bookStockExpenseGlEntries" formControlName="bookStockExpenseGlEntries">
                    <label class="form-check-label" for="bookStockExpenseGlEntries">{{ '::BookStockExpenseGlEntries' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enableDiscountsAndMargin" formControlName="enableDiscountsAndMargin">
                    <label class="form-check-label" for="enableDiscountsAndMargin">{{ '::EnableDiscountsAndMargin' | abpLocalization }}</label>
                  </div>
                </div>
              </div>

              <div class="row border-top pt-3 mt-2">
                <div class="col-md-4 mb-3">
                  <label class="form-label">{{ '::OverBillingAllowancePercent' | abpLocalization }}</label>
                  <input type="number" class="form-control" formControlName="overBillingAllowance">
                </div>
                <div class="col-md-4 mb-3">
                  <label class="form-label">{{ '::AccountsCreditControllerRole' | abpLocalization }}</label>
                  <input type="text" class="form-control" formControlName="creditControllerRole" [placeholder]="'::Placeholder:AccountsManager' | abpLocalization">
                </div>
                <div class="col-md-4 mb-3">
                  <div class="form-check form-switch mt-4">
                    <input class="form-check-input" type="checkbox" id="enableOverdueBillingThreshold" formControlName="enableOverdueBillingThreshold">
                    <label class="form-check-label" for="enableOverdueBillingThreshold">{{ '::EnableOverdueBillingThreshold' | abpLocalization }}</label>
                  </div>
                </div>
              </div>
            </div>

            <!-- Journals & Taxes Tab -->
            <div class="tab-pane fade" id="journals" role="tabpanel">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">{{ '::DeferredAccounting' | abpLocalization }}</h6>
                  <div class="mb-3">
                    <label class="form-label">{{ '::BookDeferredEntriesBasedOn' | abpLocalization }}</label>
                    <select class="form-select" formControlName="bookDeferredEntriesBasedOn">
                      <option value="Days">{{ '::Days' | abpLocalization }}</option>
                      <option value="Months">{{ '::MonthsCapitalized' | abpLocalization }}</option>
                    </select>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="automaticallyProcessDeferredAccountingEntry" formControlName="automaticallyProcessDeferredAccountingEntry">
                    <label class="form-check-label" for="automaticallyProcessDeferredAccountingEntry">{{ '::AutomaticallyProcessDeferredAccountingEntry' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="bookDeferredEntriesViaJournalEntry" formControlName="bookDeferredEntriesViaJournalEntry">
                    <label class="form-check-label" for="bookDeferredEntriesViaJournalEntry">{{ '::BookDeferredEntriesViaJournalEntry' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="submitJournalEntries" formControlName="submitJournalEntries">
                    <label class="form-check-label" for="submitJournalEntries">{{ '::SubmitJournalEntries' | abpLocalization }}</label>
                  </div>
                </div>

                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">{{ '::TaxesAndCharges' | abpLocalization }}</h6>
                  <div class="mb-3">
                    <label class="form-label">{{ '::DetermineAddressTaxCategoryFrom' | abpLocalization }}</label>
                    <select class="form-select" formControlName="determineAddressTaxCategoryFrom">
                      <option value="Billing Address">{{ '::BillingAddress' | abpLocalization }}</option>
                      <option value="Shipping Address">{{ '::ShippingAddress' | abpLocalization }}</option>
                    </select>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="addTaxesFromItemTaxTemplate" formControlName="addTaxesFromItemTaxTemplate">
                    <label class="form-check-label" for="addTaxesFromItemTaxTemplate">{{ '::AddTaxesFromItemTaxTemplate' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="addTaxesFromTaxesAndChargesTemplate" formControlName="addTaxesFromTaxesAndChargesTemplate">
                    <label class="form-check-label" for="addTaxesFromTaxesAndChargesTemplate">{{ '::AddTaxesFromTaxesTemplate' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="roundRowWiseTax" formControlName="roundRowWiseTax">
                    <label class="form-check-label" for="roundRowWiseTax">{{ '::RoundRowWiseTax' | abpLocalization }}</label>
                  </div>
                </div>
              </div>
            </div>

            <!-- Payments & Banking Tab -->
            <div class="tab-pane fade" id="payments" role="tabpanel">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">{{ '::CurrencyExchange' | abpLocalization }}</h6>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="allowStaleExchangeRates" formControlName="allowStaleExchangeRates">
                    <label class="form-check-label" for="allowStaleExchangeRates">{{ '::AllowStaleExchangeRates' | abpLocalization }}</label>
                  </div>
                  <div class="mb-3">
                    <label class="form-label">{{ '::StaleDays' | abpLocalization }}</label>
                    <input type="number" class="form-control" formControlName="staleDays">
                  </div>
                </div>

                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">{{ '::BankReconciliationAndMatching' | abpLocalization }}</h6>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enablePartyMatching" formControlName="enablePartyMatching">
                    <label class="form-check-label" for="enablePartyMatching">{{ '::EnablePartyMatching' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="enableFuzzyMatching" formControlName="enableFuzzyMatching">
                    <label class="form-check-label" for="enableFuzzyMatching">{{ '::EnableFuzzyMatching' | abpLocalization }}</label>
                  </div>
                  <div class="mb-3">
                    <label class="form-label">{{ '::TransferMatchDays' | abpLocalization }}</label>
                    <input type="number" class="form-control" formControlName="transferMatchDays">
                  </div>
                </div>
              </div>
            </div>

            <!-- Assets & Reports Tab -->
            <div class="tab-pane fade" id="assets" role="tabpanel">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">{{ '::Assets' | abpLocalization }}</h6>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="bookAssetDepreciationEntryAutomatically" formControlName="bookAssetDepreciationEntryAutomatically">
                    <label class="form-check-label" for="bookAssetDepreciationEntryAutomatically">{{ '::BookAssetDepreciationEntryAutomatically' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="calculateDeprUsingTotalDays" formControlName="calculateDeprUsingTotalDays">
                    <label class="form-check-label" for="calculateDeprUsingTotalDays">{{ '::CalculateDeprUsingTotalDays' | abpLocalization }}</label>
                  </div>
                </div>

                <div class="col-md-6 mb-3">
                  <h6 class="fw-bold mb-3">{{ '::ReportsAndChartOfAccounts' | abpLocalization }}</h6>
                  <div class="mb-3">
                    <label class="form-label">{{ '::DefaultAgeingRange' | abpLocalization }}</label>
                    <input type="text" class="form-control" formControlName="defaultAgeingRange" [placeholder]="'::Placeholder:AgeingRangeSample' | abpLocalization">
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="showBalanceInCoa" formControlName="showBalanceInCoa">
                    <label class="form-check-label" for="showBalanceInCoa">{{ '::ShowBalanceInCoa' | abpLocalization }}</label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" id="createPrInDraftStatus" formControlName="createPrInDraftStatus">
                    <label class="form-check-label" for="createPrInDraftStatus">{{ '::CreatePrInDraftStatus' | abpLocalization }}</label>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="mt-4 border-top pt-3">
            <button type="submit" class="btn btn-primary">{{ '::SaveSettings' | abpLocalization }}</button>
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
