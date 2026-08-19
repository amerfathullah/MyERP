import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ErpSettingsService } from '../../proxy/settings/erp-settings.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-selling-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
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
            <h6 class="text-muted mb-3">Transaction Requirements</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="soReq"
                    [ngModel]="settings['MyERP.Selling.SoRequired'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.SoRequired'] = $event ? 'true' : 'false'" name="soReq" />
                  <label class="form-check-label" for="soReq">Sales Order Required (before DN)</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="dnReq"
                    [ngModel]="settings['MyERP.Selling.DnRequired'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.DnRequired'] = $event ? 'true' : 'false'" name="dnReq" />
                  <label class="form-check-label" for="dnReq">Delivery Note Required (before SI)</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="multiItems"
                    [ngModel]="settings['MyERP.Selling.AllowMultipleItems'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.AllowMultipleItems'] = $event ? 'true' : 'false'" name="multiItems" />
                  <label class="form-check-label" for="multiItems">Allow Same Item Multiple Times</label>
                </div>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">Pricing & Rates</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="sameRate"
                    [ngModel]="settings['MyERP.Selling.MaintainSameRate'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.MaintainSameRate'] = $event ? 'true' : 'false'" name="sameRate" />
                  <label class="form-check-label" for="sameRate">Maintain Same Rate (from source doc)</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">Rate Enforcement Action</label>
                <select class="form-select form-select-sm" [(ngModel)]="settings['MyERP.Selling.MaintainSameRateAction']" name="rateAction">
                  <option value="Stop">Stop (Hard Block)</option>
                  <option value="Warn">Warn Only</option>
                </select>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="editRate"
                    [ngModel]="settings['MyERP.Selling.EditablePriceListRate'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.EditablePriceListRate'] = $event ? 'true' : 'false'" name="editRate" />
                  <label class="form-check-label" for="editRate">Editable Price List Rate</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">Role Allowed to Override Stop Action</label>
                <input type="text" class="form-control form-control-sm"
                  [(ngModel)]="settings['MyERP.Selling.RoleToOverrideStopAction']" name="roleOverrideRate"
                  placeholder="e.g. Sales Manager" />
              </div>
            </div>

            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="valSell"
                    [ngModel]="settings['MyERP.Selling.ValidateSellingPrice'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.ValidateSellingPrice'] = $event ? 'true' : 'false'" name="valSell" />
                  <label class="form-check-label" for="valSell">Validate Selling Price ≥ Buying</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">Blanket Order Allowance (%)</label>
                <input type="number" class="form-control form-control-sm" min="0" max="100"
                  [(ngModel)]="settings['MyERP.Selling.BlanketOrderAllowance']" name="blanketAllow" />
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="fallback"
                    [ngModel]="settings['MyERP.Selling.FallbackToDefaultPriceList'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.FallbackToDefaultPriceList'] = $event ? 'true' : 'false'" name="fallback" />
                  <label class="form-check-label" for="fallback">Fallback to Default Price List</label>
                </div>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">Accounting Features</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="discAcc"
                    [ngModel]="settings['MyERP.Selling.EnableDiscountAccounting'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.EnableDiscountAccounting'] = $event ? 'true' : 'false'" name="discAcc" />
                  <label class="form-check-label" for="discAcc">Enable Discount Accounting</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="zeroQty"
                    [ngModel]="settings['MyERP.Selling.AllowZeroQtyInQuotation'] === 'true'"
                    (ngModelChange)="settings['MyERP.Selling.AllowZeroQtyInQuotation'] = $event ? 'true' : 'false'" name="zeroQty" />
                  <label class="form-check-label" for="zeroQty">Allow Zero Qty in Quotation</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">Customer Naming By</label>
                <select class="form-select form-select-sm" [(ngModel)]="settings['MyERP.Selling.CustomerNamingBy']" name="custNaming">
                  <option value="Customer Name">Customer Name</option>
                  <option value="Naming Series">Naming Series</option>
                </select>
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
