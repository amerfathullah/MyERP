import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatDividerModule } from '@angular/material/divider';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';

@Component({
  selector: 'app-sales-invoice-detail',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatDividerModule,
    StatusBadgeComponent,
    LhdnStatusBadgeComponent,
    DocumentWorkflowComponent,
  ],
  templateUrl: './sales-invoice-detail.component.html',
  styleUrls: ['./sales-invoice-detail.component.scss'],
})
export class SalesInvoiceDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  invoice: any = null;
  itemColumns = ['itemName', 'qty', 'rate', 'discountPercent', 'amount'];

  get workflowActions(): WorkflowAction[] {
    if (!this.invoice) return [];
    const actions: WorkflowAction[] = [];
    switch (this.invoice.status) {
      case 'Draft':
        actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
        break;
      case 'Submitted':
        actions.push({ name: 'post', label: 'Post', icon: 'verified', color: 'primary' });
        break;
      case 'Posted':
        actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
        actions.push({ name: 'submitLhdn', label: 'Submit to LHDN', icon: 'cloud_upload', color: '' });
        break;
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    // TODO: Load invoice from store or API proxy
    // For now, mock data
    this.invoice = {
      id,
      invoiceNumber: 'INV-2026-0001',
      issueDate: '2026-07-01',
      customerName: 'Acme Sdn Bhd',
      buyerTin: 'C12345678',
      status: 'Draft',
      eInvoiceStatus: 'NotSubmitted',
      netTotal: 5000,
      totalTax: 300,
      grandTotal: 5300,
      items: [
        { itemName: 'Consulting Services', qty: 10, rate: 500, discountPercent: 0, amount: 5000 },
      ],
    };
  }

  onWorkflowAction(action: string): void {
    // TODO: Call backend API for status transition
    console.log('Workflow action:', action, this.invoice.id);
  }

  edit(): void {
    this.router.navigate(['/sales/invoices', this.invoice.id, 'edit']);
  }

  goBack(): void {
    this.router.navigate(['/sales/invoices']);
  }
}
