import { Component, Input, OnChanges, SimpleChanges, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GeneralLedgerService } from '../../../proxy/accounting/general-ledger.service';
import { StockLedgerService } from '../../../proxy/inventory/stock-ledger.service';
import { LocalizationPipe } from '@abp/ng.core';

/**
 * Reusable component that shows GL entries and/or SLE entries posted by a specific document.
 * Per ERPNext: every submitted document shows "Stock Ledger" + "Accounting Ledger" view buttons.
 *
 * Usage:
 *   <app-voucher-ledger
 *     [voucherType]="'SalesInvoice'"
 *     [voucherId]="invoice.id"
 *     [showStock]="true"
 *     [showAccounting]="true" />
 */
@Component({
  selector: 'app-voucher-ledger',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  template: `
    @if (isVisible()) {
      <div class="card mt-3">
        <div class="card-header d-flex align-items-center gap-2">
          <i class="fa fa-book text-muted"></i>
          <span class="fw-semibold">{{ '::LedgerEntries' | abpLocalization }}</span>
          <div class="ms-auto btn-group btn-group-sm">
            @if (showAccounting) {
              <button class="btn" [class.btn-primary]="activeTab() === 'gl'" [class.btn-outline-primary]="activeTab() !== 'gl'"
                (click)="switchTab('gl')">
                <i class="fa fa-calculator me-1"></i>{{ '::AccountingLedger' | abpLocalization }}
              </button>
            }
            @if (showStock) {
              <button class="btn" [class.btn-success]="activeTab() === 'sle'" [class.btn-outline-success]="activeTab() !== 'sle'"
                (click)="switchTab('sle')">
                <i class="fa fa-boxes-stacked me-1"></i>{{ '::StockLedger' | abpLocalization }}
              </button>
            }
          </div>
        </div>
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-4">
              <div class="spinner-border spinner-border-sm text-primary"></div>
            </div>
          } @else if (activeTab() === 'gl' && glEntries().length > 0) {
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Date' | abpLocalization }}</th>
                    <th>{{ '::Account' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Debit' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Credit' | abpLocalization }}</th>
                    <th>{{ '::CostCenter' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (entry of glEntries(); track $index) {
                    <tr>
                      <td class="text-nowrap small">{{ entry.postingDate | date:'dd/MM/yyyy' }}</td>
                      <td>
                        <span class="text-muted small">{{ entry.accountCode }}</span>
                        {{ entry.accountName }}
                      </td>
                      <td class="text-end">{{ entry.debitAmount > 0 ? (entry.debitAmount | number:'1.2-2') : '' }}</td>
                      <td class="text-end">{{ entry.creditAmount > 0 ? (entry.creditAmount | number:'1.2-2') : '' }}</td>
                      <td class="small text-muted">{{ entry.costCenterName || '—' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr>
                    <td colspan="2">{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ glTotalDebit() | number:'1.2-2' }}</td>
                    <td class="text-end">{{ glTotalCredit() | number:'1.2-2' }}</td>
                    <td>
                      @if (glIsBalanced()) {
                        <span class="badge bg-success-subtle text-success"><i class="fa fa-check"></i> Balanced</span>
                      } @else {
                        <span class="badge bg-danger-subtle text-danger"><i class="fa fa-xmark"></i> Imbalanced</span>
                      }
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          } @else if (activeTab() === 'sle' && sleEntries().length > 0) {
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Date' | abpLocalization }}</th>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th>{{ '::Warehouse' | abpLocalization }}</th>
                    <th class="text-end">{{ '::QtyChange' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end">{{ '::BalanceQty' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (entry of sleEntries(); track $index) {
                    <tr>
                      <td class="text-nowrap small">{{ entry.postingDate | date:'dd/MM/yyyy' }}</td>
                      <td>
                        <span class="text-muted small">{{ entry.itemCode }}</span>
                        {{ entry.itemName }}
                      </td>
                      <td>{{ entry.warehouseName }}</td>
                      <td class="text-end" [class.text-success]="entry.quantityChange > 0" [class.text-danger]="entry.quantityChange < 0">
                        {{ entry.quantityChange > 0 ? '+' : '' }}{{ entry.quantityChange | number:'1.0-4' }}
                      </td>
                      <td class="text-end">{{ entry.valuationRate | number:'1.2-4' }}</td>
                      <td class="text-end">{{ entry.balanceQuantity | number:'1.0-4' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr>
                    <td colspan="3">{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end text-success">+{{ sleTotalIn() | number:'1.0-4' }}</td>
                    <td class="text-end text-danger">-{{ sleTotalOut() | number:'1.0-4' }}</td>
                    <td></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          } @else {
            <div class="text-center text-muted py-3">
              <i class="fa fa-info-circle me-1"></i>{{ '::NoLedgerEntries' | abpLocalization }}
            </div>
          }
        </div>
      </div>
    }
    `,
  styles: [`
    :host { display: block; }
    .table th { font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.02em; }
  `]
})
export class VoucherLedgerComponent implements OnChanges {
  private generalLedgerService = inject(GeneralLedgerService);
  private stockLedgerService = inject(StockLedgerService);

  @Input() voucherType!: string;
  @Input() voucherId!: string;
  @Input() showAccounting = true;
  @Input() showStock = false;

  activeTab = signal<'gl' | 'sle'>('gl');
  loading = signal(false);
  glEntries = signal<any[]>([]);
  sleEntries = signal<any[]>([]);
  glTotalDebit = signal(0);
  glTotalCredit = signal(0);
  glIsBalanced = signal(true);
  sleTotalIn = signal(0);
  sleTotalOut = signal(0);

  isVisible(): boolean {
    return !!this.voucherId && !!this.voucherType;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['voucherId'] || changes['voucherType']) && this.voucherId && this.voucherType) {
      this.loadData();
    }
  }

  switchTab(tab: 'gl' | 'sle'): void {
    this.activeTab.set(tab);
    this.loadData();
  }

  private loadData(): void {
    if (this.activeTab() === 'gl' && this.showAccounting) {
      this.loadGlEntries();
    } else if (this.activeTab() === 'sle' && this.showStock) {
      this.loadSleEntries();
    }
  }

  private loadGlEntries(): void {
    this.loading.set(true);
    this.generalLedgerService.getForVoucher(this.voucherType, this.voucherId).subscribe({
      next: (data) => {
        this.glEntries.set(data.entries || []);
        this.glTotalDebit.set(data.totalDebit || 0);
        this.glTotalCredit.set(data.totalCredit || 0);
        this.glIsBalanced.set(data.isBalanced !== false);
        this.loading.set(false);
      },
      error: () => {
        this.glEntries.set([]);
        this.loading.set(false);
      }
    });
  }

  private loadSleEntries(): void {
    this.loading.set(true);
    this.stockLedgerService.getForVoucher(this.voucherType, this.voucherId).subscribe({
      next: (data) => {
        this.sleEntries.set(data.entries || []);
        this.sleTotalIn.set(data.totalQtyIn || 0);
        this.sleTotalOut.set(data.totalQtyOut || 0);
        this.loading.set(false);
      },
      error: () => {
        this.sleEntries.set([]);
        this.loading.set(false);
      }
    });
  }
}
