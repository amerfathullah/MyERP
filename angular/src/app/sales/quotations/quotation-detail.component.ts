import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { QuotationService } from '../../proxy/sales/quotation.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { QuotationStore } from '../store/quotation.store';
import type { QuotationDto } from '../../proxy/sales/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { QuotationPrintLayoutComponent } from '../../shared/components/quotation-print-layout/quotation-print-layout.component';

@Component({
  selector: 'app-quotation-detail',
  standalone: true,
  imports: [
    BreadcrumbComponent, CommonModule, RouterModule, DocumentWorkflowComponent, LoadingOverlayComponent, StatusBadgeComponent, PageModule, LocalizationPipe, ActivityLogComponent, QuotationPrintLayoutComponent],
  templateUrl: './quotation-detail.component.html',
  styleUrls: ['./quotation-detail.component.scss'],
})
export class QuotationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private service = inject(QuotationService);
  private conversionService = inject(DocumentConversionService);
  private store = inject(QuotationStore);
  private confirmation = inject(ConfirmationService);

  quotation: QuotationDto | null = null;
  companyData = signal<any>(null);
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  get workflowActions(): WorkflowAction[] {
    if (!this.quotation) return [];
    const actions: WorkflowAction[] = [];
    if (this.quotation.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (this.quotation.status === 'Submitted') {
      actions.push({ name: 'convert', label: 'Convert to SO', icon: 'transform', color: 'primary' });
      actions.push({ name: 'lost', label: 'Mark Lost', icon: 'thumb_down', color: 'warn' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
    }
    if (this.quotation.status === 'Cancelled' || this.quotation.status === 'Rejected') {
      actions.push({ name: 'amend', label: 'Amend', icon: 'file-circle-plus', color: 'success' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.quotation = result;
      // Load company data for print layout
      if ((result as any).companyId) {
        this.http.get<any>(`/api/app/company/${(result as any).companyId}`).subscribe({
          next: (company) => this.companyData.set(company),
          error: () => {} // Non-critical — print still works without company header
        });
      }
    });
  }

  onWorkflowAction(action: string): void {
    const id = this.quotation!.id!;
    switch (action) {
      case 'submit':
        this.store.submitQuotation(id);
        this.reloadAfterAction();
        break;
      case 'convert':
        this.conversionService.convertQuotationToSalesOrder(id).subscribe((salesOrder) => {
          this.router.navigate(['/sales/orders', salesOrder.id]);
        });
        break;
      case 'lost':
        this.service.markLost(id).subscribe(() => this.reloadAfterAction());
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelQuotation(id);
            this.reloadAfterAction();
          }
        });
        break;
      case 'amend':
        this.service.amend(id).subscribe({
          next: (amended) => this.router.navigate(['/sales/quotations', amended.id]),
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.quotation!.id!).subscribe((r) => { this.quotation = r; });
    }, 500);
  }

  print(): void {
    window.print();
  }
}
