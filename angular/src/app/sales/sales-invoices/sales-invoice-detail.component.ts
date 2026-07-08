import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatDividerModule } from '@angular/material/divider';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { SalesInvoiceStore } from '../store/sales-invoice.store';
import type { SalesInvoiceDto } from '../../proxy/sales/models';

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
    LhdnStatusBadgeComponent,
    DocumentWorkflowComponent,
  ],
  templateUrl: './sales-invoice-detail.component.html',
  styleUrls: ['./sales-invoice-detail.component.scss'],
})
export class SalesInvoiceDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(SalesInvoiceService);
  private eInvoiceService = inject(EInvoiceService);
  private store = inject(SalesInvoiceStore);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  invoice: SalesInvoiceDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

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
        if (!this.invoice.eInvoiceStatus || this.invoice.eInvoiceStatus === 'NotSubmitted') {
          actions.push({ name: 'submitLhdn', label: 'Submit to LHDN', icon: 'cloud_upload', color: '' });
        }
        break;
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.invoice = result;
    });
  }

  onWorkflowAction(action: string): void {
    const id = this.invoice!.id!;
    switch (action) {
      case 'submit':
        this.store.submitInvoice(id);
        this.reloadAfterAction();
        break;
      case 'post':
        this.store.postInvoice(id);
        this.reloadAfterAction();
        break;
      case 'cancel':
        this.confirmation.warn('::Sales:CancelInvoiceConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelInvoice(id);
            this.reloadAfterAction();
          }
        });
        break;
      case 'submitLhdn':
        this.submitToLhdn();
        break;
    }
  }

  private submitToLhdn(): void {
    this.eInvoiceService.submit({
      companyId: this.invoice!.companyId!,
      sourceDocumentType: 'SalesInvoice',
      sourceDocumentId: this.invoice!.id!,
    }).subscribe({
      next: (submission) => {
        this.toaster.success('Submitted to LHDN successfully. UUID: ' + (submission.documentUuid ?? 'pending'));
        this.reloadAfterAction();
      },
      error: (err) => {
        this.toaster.error(err?.error?.error?.message ?? 'LHDN submission failed');
      },
    });
  }

  edit(): void {
    this.router.navigate(['/sales/invoices', this.invoice!.id, 'edit']);
  }

  goBack(): void {
    this.router.navigate(['/sales/invoices']);
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.invoice!.id!).subscribe((result) => {
        this.invoice = result;
      });
    }, 500);
  }
}
