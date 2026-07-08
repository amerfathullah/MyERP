import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';

interface StockLedgerRow {
  date: string;
  itemCode: string;
  itemName: string;
  warehouse: string;
  qtyChange: number;
  balanceQty: number;
  valuationRate: number;
  reference: string;
}

@Component({
  selector: 'app-stock-ledger-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, MatCardModule, MatTableModule, MatFormFieldModule, MatInputModule, MatDatepickerModule, MatNativeDateModule, MatSelectModule],
  templateUrl: './stock-ledger-report.component.html',
  styleUrls: ['./stock-ledger-report.component.scss'],
})
export class StockLedgerReportComponent {
  private fb = new FormBuilder();
  filters = this.fb.group({ fromDate: [new Date(new Date().getFullYear(), 0, 1)], toDate: [new Date()], itemCode: [''], warehouse: [''] });
  displayedColumns = ['date', 'itemCode', 'itemName', 'warehouse', 'qtyChange', 'balanceQty', 'valuationRate', 'reference'];
  data: StockLedgerRow[] = [];

  generate(): void {
    // TODO: Call reporting API
    this.data = [
      { date: '2026-07-01', itemCode: 'ITM-001', itemName: 'Widget A', warehouse: 'Main', qtyChange: 100, balanceQty: 100, valuationRate: 25.00, reference: 'SE-001' },
      { date: '2026-07-03', itemCode: 'ITM-001', itemName: 'Widget A', warehouse: 'Main', qtyChange: -10, balanceQty: 90, valuationRate: 25.00, reference: 'DN-001' },
      { date: '2026-07-05', itemCode: 'ITM-002', itemName: 'Gadget B', warehouse: 'Main', qtyChange: 50, balanceQty: 50, valuationRate: 120.00, reference: 'SE-002' },
    ];
  }
}
