import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ErpSettingsService } from '../../proxy/settings/erp-settings.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-selling-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0"><i class="bi bi-graph-up me-2"></i>{{ 'MyERP::SellingSettings' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <form (ngSubmit)="save()">
            <h6 class="text-muted mb-3">{{ 'MyERP::TransactionRequirements' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="soReq"
                    [ngModel]="settings['MyERP.Selling.SoRequired'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.SoRequired'] = $event ? 'true' : 'false'" name="soReq" />
                  <label class="form-check-label" for="soReq">{{ 'MyERP::SalesOrderRequiredBeforeDn' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="dnReq"
                    [ngModel]="settings['MyERP.Selling.DnRequired'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.DnRequired'] = $event ? 'true' : 'false'" name="dnReq" />
                  <label class="form-check-label" for="dnReq">{{ 'MyERP::DeliveryNoteRequiredBeforeSi' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="multiItems"
                    [ngModel]="settings['MyERP.Selling.AllowMultipleItems'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.AllowMultipleItems'] = $event ? 'true' : 'false'" name="multiItems" />
                  <label class="form-check-label" for="multiItems">{{ 'MyERP::AllowSameItemMultipleTimes' | abpLocalization }}</label>
                </div>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ 'MyERP::PricingAndRates' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="sameRate"
                    [ngModel]="settings['MyERP.Selling.MaintainSameRate'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.MaintainSameRate'] = $event ? 'true' : 'false'" name="sameRate" />
                  <label class="form-check-label" for="sameRate">{{ 'MyERP::MaintainSameRateFromSource' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::RateEnforcementAction' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="settings['MyERP.Selling.MaintainSameRateAction']" name="rateAction">
                  <option value="Stop">{{ 'MyERP::StopHardBlock' | abpLocalization }}</option>
                  <option value="Warn">{{ 'MyERP::WarnOnly' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="editRate"
                    [ngModel]="settings['MyERP.Selling.EditablePriceListRate'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.EditablePriceListRate'] = $event ? 'true' : 'false'" name="editRate" />
                  <label class="form-check-label" for="editRate">{{ 'MyERP::EditablePriceListRate' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::RoleAllowedToOverrideStopAction' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm"
                  [(ngModel)]="settings['MyERP.Selling.RoleToOverrideStopAction']" name="roleOverrideRate"
                  [placeholder]="'::Placeholder:SalesManagerRoleExample' | abpLocalization" />
              </div>
            </div>

            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="valSell"
                    [ngModel]="settings['MyERP.Selling.ValidateSellingPrice'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.ValidateSellingPrice'] = $event ? 'true' : 'false'" name="valSell" />
                  <label class="form-check-label" for="valSell">{{ 'MyERP::ValidateSellingPriceGteBuying' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::BlanketOrderAllowance' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" min="0" max="100"
                  [(ngModel)]="settings['MyERP.Selling.BlanketOrderAllowance']" name="blanketAllow" />
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="fallback"
                    [ngModel]="settings['MyERP.Selling.FallbackToDefaultPriceList'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.FallbackToDefaultPriceList'] = $event ? 'true' : 'false'" name="fallback" />
                  <label class="form-check-label" for="fallback">{{ 'MyERP::FallbackToDefaultPriceList' | abpLocalization }}</label>
                </div>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ 'MyERP::AccountingFeatures' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="discAcc"
                    [ngModel]="settings['MyERP.Selling.EnableDiscountAccounting'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.EnableDiscountAccounting'] = $event ? 'true' : 'false'" name="discAcc" />
                  <label class="form-check-label" for="discAcc">{{ 'MyERP::EnableDiscountAccounting' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="zeroQty"
                    [ngModel]="settings['MyERP.Selling.AllowZeroQtyInQuotation'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.AllowZeroQtyInQuotation'] = $event ? 'true' : 'false'" name="zeroQty" />
                  <label class="form-check-label" for="zeroQty">{{ 'MyERP::AllowZeroQtyInQuotation' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::CustomerNamingBy' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="settings['MyERP.Selling.CustomerNamingBy']" name="custNaming">
                  <option value="Customer Name">{{ 'MyERP::CustomerName' | abpLocalization }}</option>
                  <option value="Naming Series">{{ 'MyERP::NamingSeries' | abpLocalization }}</option>
                </select>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ 'MyERP::ProformaInvoicing' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="enableProforma"
                    [ngModel]="settings['MyERP.Selling.EnableProformaInvoice'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.EnableProformaInvoice'] = $event ? 'true' : 'false'" name="enableProforma" />
                  <label class="form-check-label" for="enableProforma">{{ 'MyERP::EnableProformaInvoice' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::DefaultProformaPrintFormat' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm"
                  [(ngModel)]="settings['MyERP.Selling.DefaultProformaPrintFormat']" name="defaultProformaPrintFormat"
                  [placeholder]="'::Placeholder:ProformaPrintFormatExample' | abpLocalization" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::ProformaEmailTemplate' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm"
                  [(ngModel)]="settings['MyERP.Selling.ProformaEmailTemplate']" name="proformaEmailTemplate"
                  [placeholder]="'::Placeholder:ProformaEmailTemplateExample' | abpLocalization" />
                <small class="text-muted d-block mt-1">{{ 'MyERP::ProformaEmailTemplateHelp' | abpLocalization }}</small>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ '::CustomerOverview' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-6">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="loadCustomerOverview"
                    [ngModel]="settings['MyERP.Selling.LoadCustomerOverview'] !== 'false'"
                    (ngModelChange)="settings['MyERP.Selling.LoadCustomerOverview'] = $event ? 'true' : 'false'" name="loadCustomerOverview" />
                  <label class="form-check-label" for="loadCustomerOverview">{{ '::EnableCustomerOverviewDashboard' | abpLocalization }}</label>
                </div>
                <small class="text-muted d-block mt-1">{{ 'MyERP::CustomerOverviewHint' | abpLocalization }}</small>
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
export class SellingSettingsComponent implements OnInit {
  private service = inject(ErpSettingsService);
  private toaster = inject(ToasterService);

  settings: Record<string, string> = {};
  loading = signal(true);
  saving = signal(false);

  ngOnInit() {
    this.service.getGroup('Selling').subscribe({
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
