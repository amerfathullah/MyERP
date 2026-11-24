import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ErpSettingsService } from '../../proxy/settings/erp-settings.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-buying-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0"><i class="bi bi-cart4 me-2"></i>{{ 'MyERP::BuyingSettings' | abpLocalization }}</h5>
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
                  <input type="checkbox" class="form-check-input" id="poReq"
                    [ngModel]="settings['MyERP.Buying.PoRequired'] === 'true'"
                    (ngModelChange)="settings['MyERP.Buying.PoRequired'] = $event ? 'true' : 'false'" name="poReq" />
                  <label class="form-check-label" for="poReq">{{ 'MyERP::PoRequiredBeforePrPi' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="prReq"
                    [ngModel]="settings['MyERP.Buying.PrRequired'] === 'true'"
                    (ngModelChange)="settings['MyERP.Buying.PrRequired'] = $event ? 'true' : 'false'" name="prReq" />
                  <label class="form-check-label" for="prReq">{{ 'MyERP::PrRequiredBeforePi' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="billReject"
                    [ngModel]="settings['MyERP.Buying.BillForRejectedQty'] === 'true'"
                    (ngModelChange)="settings['MyERP.Buying.BillForRejectedQty'] = $event ? 'true' : 'false'" name="billReject" />
                  <label class="form-check-label" for="billReject">{{ 'MyERP::BillForRejectedQty' | abpLocalization }}</label>
                </div>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ 'MyERP::RateAndAllowance' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="bSameRate"
                    [ngModel]="settings['MyERP.Buying.MaintainSameRate'] === 'true'"
                    (ngModelChange)="settings['MyERP.Buying.MaintainSameRate'] = $event ? 'true' : 'false'" name="bSameRate" />
                  <label class="form-check-label" for="bSameRate">{{ 'MyERP::MaintainSameRateFromPo' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="useTxDateExRate"
                    [ngModel]="settings['MyERP.Buying.UseTransactionDateExchangeRate'] === 'true'"
                    (ngModelChange)="settings['MyERP.Buying.UseTransactionDateExchangeRate'] = $event ? 'true' : 'false'" name="useTxDateExRate" />
                  <label class="form-check-label" for="useTxDateExRate">{{ 'MyERP::UseTransactionDateExchangeRate' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::RateEnforcementAction' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="settings['MyERP.Buying.MaintainSameRateAction']" name="bRateAction">
                  <option value="Stop">{{ 'MyERP::Stop' | abpLocalization }}</option>
                  <option value="Warn">{{ 'MyERP::Warn' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::OverOrderAllowance' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" min="0" max="100"
                  [(ngModel)]="settings['MyERP.Buying.OverOrderAllowance']" name="overOrder" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::RoleAllowedToOverrideStopAction' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm"
                  [(ngModel)]="settings['MyERP.Buying.RoleToOverrideStopAction']" name="bRoleOverrideRate"
                  [placeholder]="'::Placeholder:PurchaseManagerRoleExample' | abpLocalization" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::BlanketOrderAllowance' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" min="0" max="100"
                  [(ngModel)]="settings['MyERP.Buying.BlanketOrderAllowance']" name="blanketAllow" />
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ 'MyERP::Subcontracting' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::BackflushBasedOn' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="settings['MyERP.Buying.BackflushSubcontractBasedOn']" name="backflush">
                  <option value="BOM">{{ 'MyERP::Bom' | abpLocalization }}</option>
                  <option value="Material Transferred">{{ 'MyERP::MaterialTransferred' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::OverTransferAllowance' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" min="0" max="100"
                  [(ngModel)]="settings['MyERP.Buying.OverTransferAllowance']" name="overTransfer" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::SupplierNamingBy' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="settings['MyERP.Buying.SupplierNamingBy']" name="suppNaming">
                  <option value="Supplier Name">{{ 'MyERP::SupplierName' | abpLocalization }}</option>
                  <option value="Naming Series">{{ 'MyERP::NamingSeries' | abpLocalization }}</option>
                </select>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ '::SupplierOverview' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-6">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="loadSupplierOverview"
                    [ngModel]="settings['MyERP.Buying.LoadSupplierOverview'] !== 'false'"
                    (ngModelChange)="settings['MyERP.Buying.LoadSupplierOverview'] = $event ? 'true' : 'false'" name="loadSupplierOverview" />
                  <label class="form-check-label" for="loadSupplierOverview">{{ '::EnableSupplierOverviewDashboard' | abpLocalization }}</label>
                </div>
                <small class="text-muted d-block mt-1">{{ 'MyERP::SupplierOverviewHint' | abpLocalization }}</small>
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
export class BuyingSettingsComponent implements OnInit {
  private service = inject(ErpSettingsService);
  private toaster = inject(ToasterService);

  settings: Record<string, string> = {};
  loading = signal(true);
  saving = signal(false);

  ngOnInit() {
    this.service.getGroup('Buying').subscribe({
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
