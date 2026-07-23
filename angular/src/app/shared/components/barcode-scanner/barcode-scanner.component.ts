import { Component, EventEmitter, Input, Output, signal, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export interface BarcodeScanResult {
  success: boolean;
  scanType: number;
  scanTypeName: string;
  barcode: string;
  message?: string;
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  hasSerialNo: boolean;
  hasBatchNo: boolean;
  uom?: string;
  maintainStock: boolean;
  serialNoId?: string;
  serialNumber?: string;
  batchId?: string;
  batchNo?: string;
  warehouseId?: string;
  warehouseName?: string;
  action: number;
  actionName: string;
}

export interface ScanEvent {
  result: BarcodeScanResult;
  warehouseContext?: string; // sticky warehouse from context scan
}

/**
 * Reusable barcode scanner component for warehouse operations.
 * Per ERPNext gotcha #126: warehouse context mode (scan WH → all subsequent items use that WH).
 * Per ERPNext gotcha #127: repeat scan increments qty, serial items always new row.
 *
 * Usage:
 * <app-barcode-scanner
 *   (scanned)="onBarcodeScan($event)"
 *   [placeholder]="'Scan barcode...'"
 * />
 */
@Component({
  selector: 'app-barcode-scanner',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="barcode-scanner d-flex align-items-center gap-2">
      <div class="input-group">
        <span class="input-group-text">
          <i class="fa fa-barcode"></i>
        </span>
        <input
          #scanInput
          type="text"
          class="form-control"
          [(ngModel)]="barcodeValue"
          [placeholder]="placeholder"
          (keydown.enter)="onScan()"
          [disabled]="isScanning()"
          autocomplete="off"
        />
        @if (isScanning()) {
          <span class="input-group-text">
            <i class="fa fa-spinner fa-spin"></i>
          </span>
        }
      </div>

      @if (warehouseContext()) {
        <span class="badge bg-info d-flex align-items-center gap-1">
          <i class="fa fa-warehouse"></i>
          {{ warehouseContext() }}
          <button type="button" class="btn-close btn-close-white ms-1" style="font-size: 0.6rem"
            (click)="clearWarehouseContext()" title="Clear Location"></button>
        </span>
      }
    </div>

    @if (lastResult() && !lastResult()!.success) {
      <div class="text-danger small mt-1">
        <i class="fa fa-exclamation-circle"></i> {{ lastResult()!.message }}
      </div>
    }

    @if (lastResult()?.success && showFeedback()) {
      <div class="text-success small mt-1">
        <i class="fa fa-check-circle"></i>
        @switch (lastResult()!.scanType) {
          @case (1) { {{ lastResult()!.itemCode }} — {{ lastResult()!.itemName }} }
          @case (2) { Serial: {{ lastResult()!.serialNumber }} }
          @case (3) { Batch: {{ lastResult()!.batchNo }} }
          @case (4) { Location: {{ lastResult()!.warehouseName }} }
        }
      </div>
    }
  `,
  styles: [`
    .barcode-scanner { max-width: 500px; }
    .barcode-scanner input { font-family: monospace; }
  `]
})
export class BarcodeScannerComponent {
  @ViewChild('scanInput') scanInput!: ElementRef<HTMLInputElement>;

  @Input() placeholder = 'Scan barcode or enter code...';
  @Output() scanned = new EventEmitter<ScanEvent>();

  barcodeValue = '';
  isScanning = signal(false);
  lastResult = signal<BarcodeScanResult | null>(null);
  showFeedback = signal(false);
  warehouseContext = signal<string | null>(null);

  private warehouseContextId: string | null = null;
  private feedbackTimeout: any;

  constructor(private http: HttpClient) {}

  onScan(): void {
    const barcode = this.barcodeValue.trim();
    if (!barcode || this.isScanning()) return;

    this.isScanning.set(true);
    this.showFeedback.set(false);

    this.http.get<BarcodeScanResult>(`/api/app/barcode-scan/scan`, {
      params: { barcode }
    }).subscribe({
      next: (result) => {
        this.isScanning.set(false);
        this.lastResult.set(result);
        this.barcodeValue = '';

        if (result.success) {
          // Per gotcha #126: warehouse scan sets sticky context
          if (result.scanType === 4) {
            this.warehouseContext.set(result.warehouseName ?? null);
            this.warehouseContextId = result.warehouseId ?? null;
          }

          this.scanned.emit({
            result,
            warehouseContext: this.warehouseContextId ?? undefined
          });

          this.showFeedback.set(true);
          clearTimeout(this.feedbackTimeout);
          this.feedbackTimeout = setTimeout(() => this.showFeedback.set(false), 3000);
        }

        // Auto-focus back to input for next scan
        setTimeout(() => this.scanInput?.nativeElement?.focus(), 50);
      },
      error: () => {
        this.isScanning.set(false);
        this.lastResult.set({
          success: false,
          scanType: 0,
          scanTypeName: 'None',
          barcode,
          message: 'Scanner service unavailable',
          hasSerialNo: false,
          hasBatchNo: false,
          maintainStock: false,
          action: 0,
          actionName: 'NoMatch'
        });
        this.barcodeValue = '';
        setTimeout(() => this.scanInput?.nativeElement?.focus(), 50);
      }
    });
  }

  clearWarehouseContext(): void {
    this.warehouseContext.set(null);
    this.warehouseContextId = null;
  }

  /** Programmatically focus the scanner input (e.g., after page load). */
  focus(): void {
    this.scanInput?.nativeElement?.focus();
  }
}
