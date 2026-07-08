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
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';

@Component({
  selector: 'app-einvoice-status-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, MatCardModule, MatTableModule, MatFormFieldModule, MatInputModule, MatDatepickerModule, MatNativeDateModule, MatButtonModule, MatIconModule, MatSelectModule, LhdnStatusBadgeComponent],
  templateUrl: './einvoice-status-report.component.html',
  styleUrls: ['./einvoice-status-report.component.scss'],
})
export class EinvoiceStatusReportComponent {
  private fb = new FormBuilder();
  filters = this.fb.group({ fromDate: [new Date(new Date().getFullYear(), 0, 1)], toDate: [new Date()], status: [''], documentType: ['sales'] });
  displayedColumns = ['invoiceNumber', 'date', 'party', 'amount', 'lhdnStatus', 'submittedAt'];
  data: any[] = [];

  generate(): void {
    // TODO: Call reporting API
    this.data = [
      { invoiceNumber: 'INV-2026-0001', date: '2026-07-01', party: 'Acme Sdn Bhd', amount: 5300, lhdnStatus: 'Valid', submittedAt: '2026-07-01T10:30:00' },
      { invoiceNumber: 'INV-2026-0002', date: '2026-07-03', party: 'Beta Corp', amount: 8200, lhdnStatus: 'Submitted', submittedAt: '2026-07-03T14:20:00' },
      { invoiceNumber: 'INV-2026-0003', date: '2026-07-05', party: 'Gamma LLC', amount: 3100, lhdnStatus: 'Invalid', submittedAt: '2026-07-05T09:00:00' },
      { invoiceNumber: 'INV-2026-0004', date: '2026-07-06', party: 'Delta Bhd', amount: 15000, lhdnStatus: 'NotSubmitted', submittedAt: null },
    ];
  }
}
