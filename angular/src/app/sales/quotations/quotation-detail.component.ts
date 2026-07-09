import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute, Router } from '@angular/router';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { QuotationService } from '../../proxy/sales/quotation.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { QuotationStore } from '../store/quotation.store';
import type { QuotationDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-quotation-detail',
  standalone: true,
  imports: [
    CommonModule, DocumentWorkflowComponent, LoadingOverlayComponent, StatusBadgeComponent, PageModule, LocalizationPipe],
  templateUrl: './quotation-detail.component.html',
  styleUrls: ['./quotation-detail.component.scss'],
})
export class QuotationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(QuotationService);
  private conversionService = inject(DocumentConversionService);
  private store = inject(QuotationStore);
  private confirmation = inject(ConfirmationService);

  quotation: QuotationDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  get workflowActions(): WorkflowAction[] {
    if (!this.quotation) return [];
    const actions: WorkflowAction[] = [];
    if (this.quotation.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (this.quotation.status === 'Submitted') {
      actions.push({ name: 'convert', label: 'Convert to SO', icon: 'transform', color: 'primary' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => { this.quotation = result; });
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
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelQuotation(id);
            this.reloadAfterAction();
          }
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.quotation!.id!).subscribe((r) => { this.quotation = r; });
    }, 500);
  }
}
