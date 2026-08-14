import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ErpSettingsService } from '../../proxy/settings/erp-settings.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-stock-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
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
            <h6 class="text-muted mb-3">Valuation & Stock Control</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">Default Valuation Method</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.DefaultValuationMethod']" name="valMethod">
                  <option value="FIFO">FIFO</option>
                  <option value="Moving Average">Moving Average</option>
                  <option value="LIFO">LIFO</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">Over Delivery/Receipt Allowance (%)</label>
                <input type="number" class="form-control" min="0" max="100" step="0.1"
                  [(ngModel)]="settings['MyERP.Stock.OverDeliveryReceiptAllowance']" name="overAllowance" />
              </div>
              <div class="col-md-4">
                <label class="form-label">Pick Serial/Batch Based On</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.PickSerialBatchBasedOn']" name="pickBasis">
                  <option value="FIFO">FIFO (Oldest First)</option>
                  <option value="LIFO">LIFO (Newest First)</option>
                  <option value="Expiry">Expiry (Earliest Expiry)</option>
                </select>
              </div>
            </div>

            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">Item Naming By</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.ItemNamingBy']" name="itemNaming">
                  <option value="Item Code">Item Code</option>
                  <option value="Naming Series">Naming Series</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">Stock Frozen Up To (Days)</label>
                <input type="number" class="form-control" min="0"
                  [(ngModel)]="settings['MyERP.Stock.StockFrozenUpToDays']" name="frozenDays" />
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">Feature Toggles</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="allowNeg"
                    [ngModel]="settings['MyERP.Stock.AllowNegativeStock'] === 'true'"
                    (ngModelChange)="settings['MyERP.Stock.AllowNegativeStock'] = $event ? 'true' : 'false'" name="allowNeg" />
                  <label class="form-check-label" for="allowNeg">Allow Negative Stock</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="autoInsert"
                    [ngModel]="settings['MyERP.Stock.AutoInsertPriceListRate'] === 'true'"
                    (ngModelChange)="settings['MyERP.Stock.AutoInsertPriceListRate'] = $event ? 'true' : 'false'" name="autoInsert" />
                  <label class="form-check-label" for="autoInsert">Auto Insert Price List Rate</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="enableReserv"
                    [ngModel]="settings['MyERP.Stock.EnableStockReservation'] === 'true'"
                    (ngModelChange)="settings['MyERP.Stock.EnableStockReservation'] = $event ? 'true' : 'false'" name="enableReserv" />
                  <label class="form-check-label" for="enableReserv">Enable Stock Reservation</label>
                </div>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">Quality Inspection</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">Action if QI Not Submitted</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.ActionIfQINotSubmitted']" name="qiAction">
                  <option value="Stop">Stop (Block Submission)</option>
                  <option value="Warn">Warn (Allow with Warning)</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">Action if QI Rejected</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Stock.ActionIfQIRejected']" name="qiReject">
                  <option value="Stop">Stop (Block Submission)</option>
                  <option value="Warn">Warn (Allow with Warning)</option>
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
