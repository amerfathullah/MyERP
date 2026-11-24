import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ErpSettingsService } from '../../proxy/settings/erp-settings.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-stock-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0"><i class="bi bi-boxes me-2"></i>{{ 'MyERP::StockSettings' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <form (ngSubmit)="save()">
            <h6 class="text-muted mb-3">{{ 'MyERP::ValuationAndStockControl' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::DefaultValuationMethod' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.DefaultValuationMethod']" name="valMethod">
                  <option value="FIFO">FIFO</option>
                  <option value="Moving Average">{{ 'MyERP::MovingAverage' | abpLocalization }}</option>
                  <option value="LIFO">LIFO</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::OverDeliveryReceiptAllowance' | abpLocalization }}</label>
                <input type="number" class="form-control" min="0" max="100" step="0.1"
                  [(ngModel)]="settings['MyERP.Stock.OverDeliveryReceiptAllowance']" name="overAllowance" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::PickSerialBatchBasedOn' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.PickSerialBatchBasedOn']" name="pickBasis">
                  <option value="FIFO">{{ 'MyERP::FifoOldestFirst' | abpLocalization }}</option>
                  <option value="LIFO">{{ 'MyERP::LifoNewestFirst' | abpLocalization }}</option>
                  <option value="Expiry">{{ 'MyERP::ExpiryEarliestExpiry' | abpLocalization }}</option>
                </select>
              </div>
            </div>

            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::ItemNamingBy' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.ItemNamingBy']" name="itemNaming">
                  <option value="Item Code">{{ 'MyERP::ItemCode' | abpLocalization }}</option>
                  <option value="Naming Series">{{ 'MyERP::NamingSeries' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::StockFrozenUpToDays' | abpLocalization }}</label>
                <input type="number" class="form-control" min="0"
                  [(ngModel)]="settings['MyERP.Stock.StockFrozenUpToDays']" name="frozenDays" />
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ 'MyERP::FeatureToggles' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="allowNeg"
                    [ngModel]="settings['MyERP.Stock.AllowNegativeStock'] === 'true'"
                    (ngModelChange)="settings['MyERP.Stock.AllowNegativeStock'] = $event ? 'true' : 'false'" name="allowNeg" />
                  <label class="form-check-label" for="allowNeg">{{ 'MyERP::AllowNegativeStock' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="autoInsert"
                    [ngModel]="settings['MyERP.Stock.AutoInsertPriceListRate'] === 'true'"
                    (ngModelChange)="settings['MyERP.Stock.AutoInsertPriceListRate'] = $event ? 'true' : 'false'" name="autoInsert" />
                  <label class="form-check-label" for="autoInsert">{{ 'MyERP::AutoInsertPriceListRate' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="enableReserv"
                    [ngModel]="settings['MyERP.Stock.EnableStockReservation'] === 'true'"
                    (ngModelChange)="settings['MyERP.Stock.EnableStockReservation'] = $event ? 'true' : 'false'" name="enableReserv" />
                  <label class="form-check-label" for="enableReserv">{{ 'MyERP::EnableStockReservation' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4 mt-3">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="allowUomConv"
                    [ngModel]="settings['MyERP.Stock.AllowUomWithConversionRateDefinedInItem'] === 'true'"
                    (ngModelChange)="settings['MyERP.Stock.AllowUomWithConversionRateDefinedInItem'] = $event ? 'true' : 'false'" name="allowUomConv" />
                  <label class="form-check-label" for="allowUomConv">{{ 'MyERP::AllowUomWithConversionRateDefinedInItem' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-8 mt-3">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="autoRepostAudit"
                    [ngModel]="settings['MyERP.Stock.AutoRepostIncorrectValuationEntries'] === 'true'"
                    (ngModelChange)="settings['MyERP.Stock.AutoRepostIncorrectValuationEntries'] = $event ? 'true' : 'false'" name="autoRepostAudit" />
                  <label class="form-check-label" for="autoRepostAudit">{{ 'MyERP::AutoRepostIncorrectValuationEntries' | abpLocalization }}</label>
                </div>
                <small class="text-muted">{{ 'MyERP::AutoRepostIncorrectValuationEntriesHint' | abpLocalization }}</small>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ 'MyERP::QualityInspection' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::ActionIfQiNotSubmitted' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.ActionIfQINotSubmitted']" name="qiAction">
                  <option value="Stop">{{ 'MyERP::StopBlockSubmission' | abpLocalization }}</option>
                  <option value="Warn">{{ 'MyERP::WarnAllowWithWarning' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::ActionIfQiRejected' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.ActionIfQIRejected']" name="qiReject">
                  <option value="Stop">{{ 'MyERP::StopBlockSubmission' | abpLocalization }}</option>
                  <option value="Warn">{{ 'MyERP::WarnAllowWithWarning' | abpLocalization }}</option>
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
export class StockSettingsComponent implements OnInit {
  private service = inject(ErpSettingsService);
  private toaster = inject(ToasterService);

  settings: Record<string, string> = {};
  loading = signal(true);
  saving = signal(false);

  ngOnInit() {
    this.service.getGroup('Stock').subscribe({
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
