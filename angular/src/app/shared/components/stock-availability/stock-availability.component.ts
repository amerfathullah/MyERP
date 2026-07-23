import { Component, Input, OnChanges, SimpleChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';

/**
 * Inline stock availability indicator for transaction forms.
 * Shows available qty with color coding (red = insufficient, orange = low, green = sufficient).
 * Per ERPNext: shows alongside item selection in SO/DN/SI forms.
 *
 * Usage: <app-stock-availability [itemId]="item.itemId" [requiredQty]="item.qty" [warehouseId]="warehouseId" />
 */
@Component({
  selector: 'app-stock-availability',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (loading()) {
      <span class="badge bg-light text-muted"><i class="fa fa-spinner fa-spin"></i></span>
    } @else if (availability() !== null) {
      <span class="badge" [class]="badgeClass()">
        <i class="fa fa-boxes-stacked me-1"></i>
        {{ availability()!.availableQty | number:'1.0-2' }}
        @if (requiredQty > 0 && availability()!.availableQty < requiredQty) {
          <i class="fa fa-exclamation-triangle ms-1" title="Insufficient stock"></i>
        }
      </span>
    }
  `,
  styles: [`
    :host { display: inline-block; }
    .badge { font-size: 0.75rem; font-weight: 500; }
  `]
})
export class StockAvailabilityComponent implements OnChanges {
  @Input() itemId: string = '';
  @Input() requiredQty: number = 0;
  @Input() warehouseId: string = '';

  loading = signal(false);
  availability = signal<{ availableQty: number; actualQty: number; reservedQty: number; projectedQty: number } | null>(null);

  constructor(private http: HttpClient) {}

  ngOnChanges(changes: SimpleChanges) {
    if ((changes['itemId'] || changes['warehouseId']) && this.itemId) {
      this.fetchAvailability();
    }
  }

  private fetchAvailability() {
    this.loading.set(true);
    const params: any = { itemIds: [this.itemId] };
    if (this.warehouseId) params.warehouseId = this.warehouseId;

    this.http.post<any[]>('/api/app/stock-balance/items-availability', params)
      .subscribe({
        next: (res) => {
          const match = res?.find((r: any) => r.itemId === this.itemId);
          this.availability.set(match || { availableQty: 0, actualQty: 0, reservedQty: 0, projectedQty: 0 });
          this.loading.set(false);
        },
        error: () => {
          this.availability.set(null);
          this.loading.set(false);
        }
      });
  }

  badgeClass(): string {
    const avail = this.availability();
    if (!avail) return 'bg-secondary';
    if (this.requiredQty > 0 && avail.availableQty < this.requiredQty) return 'bg-danger';
    if (avail.availableQty <= 0) return 'bg-warning text-dark';
    return 'bg-success';
  }
}
