import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Stock Entry print layout — professional A4 internal stock transfer/movement document.
 * Used for: Material Receipts, Material Issues, Transfers, Manufacture entries.
 * Includes: company header, purpose/type banner, source/target warehouses,
 * items table with valuation, totals, and authorization signatures.
 * Per ERPNext: Stock Entry is the universal stock movement document.
 */
@Component({
  selector: 'app-se-print-layout',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="se-print-layout" *ngIf="entry">
      <!-- Company Header -->
      <div class="print-header d-flex justify-content-between align-items-start mb-4">
        <div>
          <h3 class="fw-bold mb-1">{{ companyName }}</h3>
          <p class="text-muted mb-0" *ngIf="companyAddress">{{ companyAddress }}</p>
        </div>
        <div class="text-end">
          <h4 class="text-uppercase text-primary fw-bold mb-1">STOCK {{ entryTypeLabel }}</h4>
          <p class="mb-0"><strong>Entry #:</strong> {{ entry.entryNumber }}</p>
          <p class="mb-0"><strong>Date:</strong> {{ entry.postingDate | date:'dd/MM/yyyy' }}</p>
          <p class="mb-0" *ngIf="entry.workOrderId"><strong>Work Order:</strong> {{ woNumber || '—' }}</p>
        </div>
      </div>

      <!-- Purpose Banner -->
      <div class="alert alert-light border text-center mb-4">
        <strong>Purpose:</strong> {{ entryTypeLabel }}
        <span *ngIf="sourceWarehouse"> | <strong>From:</strong> {{ sourceWarehouse }}</span>
        <span *ngIf="targetWarehouse"> | <strong>To:</strong> {{ targetWarehouse }}</span>
      </div>

      <!-- Items Table -->
      <table class="table table-bordered table-sm mb-4">
        <thead class="table-light">
          <tr>
            <th style="width:5%">#</th>
            <th style="width:30%">Item</th>
            <th style="width:15%">Source WH</th>
            <th style="width:15%">Target WH</th>
            <th class="text-center" style="width:10%">Qty</th>
            <th class="text-end" style="width:12%">Rate</th>
            <th class="text-end" style="width:13%">Amount</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let item of entry.items; let i = index">
            <td>{{ i + 1 }}</td>
            <td>{{ item.itemName || item.description || '—' }}</td>
            <td>{{ item.sourceWarehouseName || '—' }}</td>
            <td>{{ item.targetWarehouseName || '—' }}</td>
            <td class="text-center">{{ item.quantity }}</td>
            <td class="text-end">{{ item.valuationRate | number:'1.2-2' }}</td>
            <td class="text-end">{{ (item.quantity * item.valuationRate) | number:'1.2-2' }}</td>
          </tr>
        </tbody>
        <tfoot>
          <tr class="table-light fw-bold">
            <td colspan="4" class="text-end">Total Qty:</td>
            <td class="text-center">{{ totalQty }}</td>
            <td class="text-end">Total Value:</td>
            <td class="text-end">{{ totalValue | number:'1.2-2' }}</td>
          </tr>
        </tfoot>
      </table>

      <!-- Additional Costs (for Manufacture/Repack) -->
      <div *ngIf="entry.additionalCosts && entry.additionalCosts.length > 0" class="mb-4">
        <h6 class="fw-bold">Additional Costs</h6>
        <table class="table table-sm table-bordered">
          <thead class="table-light">
            <tr><th>Description</th><th class="text-end">Amount</th></tr>
          </thead>
          <tbody>
            <tr *ngFor="let cost of entry.additionalCosts">
              <td>{{ cost.description }}</td>
              <td class="text-end">{{ cost.amount | number:'1.2-2' }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Remarks -->
      <div class="mb-4" *ngIf="entry.remarks">
        <h6 class="fw-bold">Remarks</h6>
        <p class="border rounded p-2 bg-light">{{ entry.remarks }}</p>
      </div>

      <!-- Signatures -->
      <div class="row mt-5 pt-4">
        <div class="col-4 text-center">
          <div class="border-bottom border-dark mb-1" style="height:40px;"></div>
          <small class="text-muted">Prepared By</small>
        </div>
        <div class="col-4 text-center">
          <div class="border-bottom border-dark mb-1" style="height:40px;"></div>
          <small class="text-muted">Issued By / Received By</small>
        </div>
        <div class="col-4 text-center">
          <div class="border-bottom border-dark mb-1" style="height:40px;"></div>
          <small class="text-muted">Authorized By</small>
        </div>
      </div>

      <!-- Footer -->
      <div class="text-center mt-4 text-muted small">
        <p class="mb-0">Internal document — not for external distribution.</p>
        <p class="mb-0">Printed on: {{ today | date:'dd/MM/yyyy HH:mm' }}</p>
      </div>
    </div>
  `,
  styles: [`
    .se-print-layout { padding: 20mm; font-size: 12px; }
    @media screen { .se-print-layout { display: none; } }
    @media print {
      .se-print-layout { display: block !important; page-break-after: always; }
      .se-print-layout .table { font-size: 11px; }
      .se-print-layout .alert { border: 1px solid #333 !important; background: #f8f8f8 !important; }
    }
  `]
})
export class StockEntryPrintLayoutComponent {
  @Input() entry: any;
  @Input() companyName = '';
  @Input() companyAddress = '';
  @Input() sourceWarehouse = '';
  @Input() targetWarehouse = '';
  @Input() woNumber = '';

  today = new Date();

  private typeLabels: Record<string, string> = {
    '0': 'RECEIPT', '1': 'ISSUE', '2': 'TRANSFER',
    '3': 'TRANSFER (MANUFACTURE)', '4': 'MANUFACTURE', '5': 'REPACK',
    '6': 'SEND TO SUBCONTRACTOR', '7': 'CONSUMPTION', '8': 'DISASSEMBLE',
    '9': 'SEND TO WAREHOUSE', '10': 'RECEIVE AT WAREHOUSE',
    MaterialReceipt: 'RECEIPT', MaterialIssue: 'ISSUE', MaterialTransfer: 'TRANSFER',
    Manufacture: 'MANUFACTURE', Repack: 'REPACK', Disassemble: 'DISASSEMBLE',
  };

  get entryTypeLabel(): string {
    const t = this.entry?.entryType?.toString() ?? '';
    return this.typeLabels[t] || t.toUpperCase() || 'ENTRY';
  }

  get totalQty(): number {
    return (this.entry?.items ?? []).reduce((s: number, i: any) => s + Math.abs(i.quantity ?? 0), 0);
  }

  get totalValue(): number {
    return (this.entry?.items ?? []).reduce((s: number, i: any) => s + Math.abs((i.quantity ?? 0) * (i.valuationRate ?? 0)), 0);
  }
}
