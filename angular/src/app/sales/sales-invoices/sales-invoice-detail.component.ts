import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { PageModule } from '@abp/ng.components/page';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { SalesInvoiceStore } from '../store/sales-invoice.store';
import type { SalesInvoiceDto } from '../../proxy/sales/models';

export interface DetailWorkflowAction {
  name: string;
  label: string;
  icon: string;
  btnClass: string;
}

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-sales-invoice-detail',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    StatusBadgeComponent,
    LhdnStatusBadgeComponent,
    ActivityLogComponent,
    BreadcrumbComponent,
    LocalizationPipe],
  templateUrl: './sales-invoice-detail.component.html',
  styleUrls: ['./sales-invoice-detail.component.scss'],
})
export class SalesInvoiceDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private service = inject(SalesInvoiceService);
  private eInvoiceService = inject(EInvoiceService);
  private store = inject(SalesInvoiceStore);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  invoice: SalesInvoiceDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];
  paymentSchedule = signal<any[]>([]);

  get workflowActions(): DetailWorkflowAction[] {
    if (!this.invoice) return [];
    const actions: DetailWorkflowAction[] = [];
    switch (this.invoice.status) {
      case 'Draft':
        actions.push({ name: 'submit', label: 'Submit', icon: 'fa fa-paper-plane', btnClass: 'btn-primary' });
        break;
      case 'Submitted':
        actions.push({ name: 'post', label: 'Post', icon: 'fa fa-check-double', btnClass: 'btn-success' });
        break;
      case 'Posted':
        actions.push({ name: 'payment', label: 'Make Payment', icon: 'fa fa-money-bill', btnClass: 'btn-success' });
        actions.push({ name: 'return', label: 'Create Return', icon: 'fa fa-rotate-left', btnClass: 'btn-outline-warning' });
        actions.push({ name: 'cancel', label: 'Cancel', icon: 'fa fa-ban', btnClass: 'btn-outline-danger' });
        if (!this.invoice.eInvoiceStatus || this.invoice.eInvoiceStatus === 'NotSubmitted') {
          actions.push({ name: 'submitLhdn', label: 'Submit to LHDN', icon: 'fa fa-cloud-arrow-up', btnClass: 'btn-outline-primary' });
        }
        break;
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.invoice = result;
      // Load payment schedule
      this.http.get<any[]>(`/api/app/sales-invoice/${id}/payment-schedule`)
        .subscribe(schedule => this.paymentSchedule.set(schedule ?? []));
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
      case 'payment':
        this.router.navigate(['/accounting/payments/new'], {
          queryParams: { partyType: 'Customer', againstInvoiceType: 'SalesInvoice', againstInvoiceId: id }
        });
        break;
      case 'return':
        this.router.navigate(['/sales/invoices/new'], {
          queryParams: { returnAgainst: id }
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

  printInvoice(): void {
    window.print();
  }

  duplicate(): void {
    this.router.navigate(['/sales/invoices/new'], {
      queryParams: { duplicateFrom: this.invoice!.id }
    });
  }

  amend(): void {
    this.http.post<any>(`/api/app/sales-invoice/${this.invoice!.id}/amend`, {}).subscribe({
      next: (amended) => {
        this.router.navigate(['/sales/invoices', amended.id]);
      },
    });
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.invoice!.id!).subscribe((result) => {
        this.invoice = result;
      });
    }, 500);
  }
}
