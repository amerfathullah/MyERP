import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormArray, FormGroup } from '@angular/forms';

export interface ColumnDef {
  field: string;
  label: string;
  type?: 'text' | 'number' | 'date';
  width?: string;
}

@Component({
  selector: 'app-child-table',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule],
  templateUrl: './child-table.component.html',
  styleUrls: ['./child-table.component.scss'],
})
export class ChildTableComponent {
  @Input({ required: true }) columns: ColumnDef[] = [];
  @Input({ required: true }) rows!: FormArray;
  @Output() rowAdded = new EventEmitter<void>();
  @Output() rowRemoved = new EventEmitter<number>();

  get displayedColumns(): string[] {
    return [...this.columns.map(c => c.field), 'actions'];
  }

  get dataSource(): FormGroup[] {
    return this.rows.controls as FormGroup[];
  }

  addRow(): void {
    this.rowAdded.emit();
  }

  removeRow(index: number): void {
    this.rowRemoved.emit(index);
  }
}
