import { Component, Input, OnChanges, SimpleChanges, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardService } from '../../../proxy/core/dashboard.service';
import { LocalizationPipe } from '@abp/ng.core';

/**
 * Shows batch expiry warnings for items being shipped/issued.
 * Per ERPNext DO-NOT: "Implement batch expiry without blocking expired batch consumption"
 * Per Malaysia compliance: prevents shipping expired goods to customers.
 */
@Component({
  selector: 'app-batch-expiry-warning',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  template: `
    @if (warnings().length > 0) {
      <div class="alert alert-danger d-flex align-items-start mb-3" role="alert">
        <i class="fa fa-exclamation-triangle me-2 mt-1"></i>
        <div>
          <strong>{{ '::BatchExpiryWarning' | abpLocalization }}</strong>
          <ul class="mb-0 mt-1 small">
            @for (w of warnings(); track w.itemId) {
              <li>
                <strong>{{ w.itemName }}</strong> — {{ '::Batch' | abpLocalization }}: {{ w.batchNo }}
                @if (w.isExpired) {
                  <span class="badge bg-danger ms-1">{{ '::Expired' | abpLocalization }}</span>
                  <span class="text-muted">({{ w.expiryDate | date:'dd/MM/yyyy' }})</span>
                } @else {
                  <span class="badge bg-warning text-dark ms-1">{{ w.daysUntilExpiry }} {{ '::DaysLeft' | abpLocalization }}</span>
                }
              </li>
            }
          </ul>
        </div>
      </div>
    }
    @if (nearExpiryItems().length > 0 && warnings().length === 0) {
      <div class="alert alert-warning d-flex align-items-start mb-3" role="alert">
        <i class="fa fa-clock me-2 mt-1"></i>
        <div>
          <strong>{{ '::NearExpiryNotice' | abpLocalization }}</strong>
          <ul class="mb-0 mt-1 small">
            @for (w of nearExpiryItems(); track w.itemId) {
              <li>
                <strong>{{ w.itemName }}</strong> — {{ w.daysUntilExpiry }} {{ '::DaysLeft' | abpLocalization }}
                <span class="text-muted">({{ w.expiryDate | date:'dd/MM/yyyy' }})</span>
              </li>
            }
          </ul>
        </div>
      </div>
    }
  `
})
export class BatchExpiryWarningComponent implements OnChanges {
  private dashboardService = inject(DashboardService);

  @Input() itemIds: string[] = [];
  @Input() companyId: string = '';
  @Input() nearExpiryDays: number = 14;

  warnings = signal<BatchWarning[]>([]);
  nearExpiryItems = signal<BatchWarning[]>([]);

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['itemIds'] || changes['companyId']) && this.itemIds.length > 0) {
      this.checkBatchExpiry();
    }
  }

  private checkBatchExpiry(): void {
    const params: any = { daysAhead: this.nearExpiryDays };
    if (this.companyId) params.companyId = this.companyId;

    this.dashboardService.getExpiringBatches(this.companyId || undefined as any, this.nearExpiryDays).subscribe({
      next: (batches) => {
        const today = new Date();
        const expired: BatchWarning[] = [];
        const nearExpiry: BatchWarning[] = [];

        (batches ?? []).forEach((b: any) => {
          if (!this.itemIds.includes(b.itemId)) return;
          const expiry = new Date(b.expiryDate);
          const daysUntil = Math.ceil((expiry.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));

          const warning: BatchWarning = {
            itemId: b.itemId,
            itemName: b.itemName || b.itemCode,
            batchNo: b.batchNo,
            expiryDate: b.expiryDate,
            daysUntilExpiry: daysUntil,
            isExpired: daysUntil <= 0,
          };

          if (daysUntil <= 0) expired.push(warning);
          else if (daysUntil <= this.nearExpiryDays) nearExpiry.push(warning);
        });

        this.warnings.set(expired);
        this.nearExpiryItems.set(nearExpiry);
      },
      error: () => {
        this.warnings.set([]);
        this.nearExpiryItems.set([]);
      }
    });
  }
}

interface BatchWarning {
  itemId: string;
  itemName: string;
  batchNo: string;
  expiryDate: string;
  daysUntilExpiry: number;
  isExpired: boolean;
}
