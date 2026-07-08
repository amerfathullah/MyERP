import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormArray, FormGroup } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';

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
    ReactiveFormsModule,
    MatTableModule,
    MatIconModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
  ],
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
