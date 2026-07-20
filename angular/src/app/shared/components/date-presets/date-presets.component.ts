import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface DateRange {
  from: string;
  to: string;
}

@Component({
  selector: 'app-date-presets',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="btn-group btn-group-sm">
      <button type="button" class="btn btn-outline-secondary" (click)="setThisMonth()">This Month</button>
      <button type="button" class="btn btn-outline-secondary" (click)="setLastMonth()">Last Month</button>
      <button type="button" class="btn btn-outline-secondary" (click)="setThisQuarter()">This Quarter</button>
      <button type="button" class="btn btn-outline-secondary" (click)="clearDates()">
        <i class="fa fa-times"></i>
      </button>
    </div>
  `,
})
export class DatePresetsComponent {
  @Output() dateRange = new EventEmitter<DateRange>();

  setThisMonth(): void {
    const now = new Date();
    const from = new Date(now.getFullYear(), now.getMonth(), 1);
    const to = new Date(now.getFullYear(), now.getMonth() + 1, 0);
    this.emit(from, to);
  }

  setLastMonth(): void {
    const now = new Date();
    const from = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    const to = new Date(now.getFullYear(), now.getMonth(), 0);
    this.emit(from, to);
  }

  setThisQuarter(): void {
    const now = new Date();
    const quarter = Math.floor(now.getMonth() / 3);
    const from = new Date(now.getFullYear(), quarter * 3, 1);
    const to = new Date(now.getFullYear(), quarter * 3 + 3, 0);
    this.emit(from, to);
  }

  clearDates(): void {
    this.dateRange.emit({ from: '', to: '' });
  }

  private emit(from: Date, to: Date): void {
    this.dateRange.emit({
      from: this.formatDate(from),
      to: this.formatDate(to),
    });
  }

  private formatDate(d: Date): string {
    return d.toISOString().split('T')[0];
  }
}
