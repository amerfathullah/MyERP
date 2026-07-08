import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

interface SubmissionLog {
  id: string;
  invoiceNumber: string;
  documentType: string;
  lhdnUuid: string;
  status: string;
  submittedAt: string;
  responseMessage: string;
}

@Component({
  selector: 'app-einvoice-logs',
  standalone: true,
  imports: [CommonModule, PageModule, MatCardModule, MatTableModule, MatIconModule, MatButtonModule, LhdnStatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './einvoice-logs.component.html',
  styleUrls: ['./einvoice-logs.component.scss'],
})
export class EinvoiceLogsComponent implements OnInit {
  logs: SubmissionLog[] = [];
  isLoading = false;
  displayedColumns = ['invoiceNumber', 'documentType', 'lhdnUuid', 'status', 'submittedAt', 'actions'];

  ngOnInit(): void {
    // TODO: Wire to EInvoiceSubmissionAppService proxy
    // Mock data
    this.logs = [
      { id: '1', invoiceNumber: 'INV-2026-0001', documentType: 'Invoice', lhdnUuid: 'abc123-def456', status: 'Valid', submittedAt: '2026-07-01T10:30:00', responseMessage: 'Document validated' },
      { id: '2', invoiceNumber: 'INV-2026-0002', documentType: 'Invoice', lhdnUuid: 'ghi789-jkl012', status: 'Submitted', submittedAt: '2026-07-05T14:20:00', responseMessage: 'Pending validation' },
      { id: '3', invoiceNumber: 'CN-2026-0001', documentType: 'Credit Note', lhdnUuid: 'mno345-pqr678', status: 'Invalid', submittedAt: '2026-07-06T09:15:00', responseMessage: 'Invalid buyer TIN' },
    ];
  }

  refreshStatus(id: string): void {
    // TODO: Call EInvoiceService.getStatus(uuid)
  }
}
