import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface SortEvent {
  field: string;
  direction: 'asc' | 'desc';
}

@Component({
  selector: 'app-sortable-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="d-inline-flex align-items-center gap-1 cursor-pointer user-select-none"
      (click)="toggleSort()" [style.cursor]="'pointer'">
      <ng-content></ng-content>
      @if (currentField === field) {
        <i class="fa fa-sort-{{ currentDirection === 'asc' ? 'up' : 'down' }} text-primary"></i>
      } @else {
        <i class="fa fa-sort text-muted opacity-50"></i>
      }
    </span>
  `,
})
export class SortableHeaderComponent {
  @Input({ required: true }) field!: string;
  @Input() currentField: string | null = null;
  @Input() currentDirection: 'asc' | 'desc' = 'asc';
  @Output() sort = new EventEmitter<SortEvent>();

  toggleSort(): void {
    if (this.currentField === this.field) {
      this.sort.emit({
        field: this.field,
        direction: this.currentDirection === 'asc' ? 'desc' : 'asc',
      });
    } else {
      this.sort.emit({ field: this.field, direction: 'desc' });
    }
  }
}
