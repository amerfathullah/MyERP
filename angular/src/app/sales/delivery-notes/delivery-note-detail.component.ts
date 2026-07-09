import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute, Router } from '@angular/router';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { DeliveryNoteStore } from '../store/delivery-note.store';
import type { DeliveryNoteDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-delivery-note-detail',
  standalone: true,
  imports: [
    CommonModule, DocumentWorkflowComponent, LoadingOverlayComponent, StatusBadgeComponent, PageModule, LocalizationPipe],
  templateUrl: './delivery-note-detail.component.html',
  styleUrls: ['./delivery-note-detail.component.scss'],
})
export class DeliveryNoteDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(DeliveryNoteService);
  private conversionService = inject(DocumentConversionService);
  private store = inject(DeliveryNoteStore);
  private confirmation = inject(ConfirmationService);
  private router = inject(Router);

  deliveryNote: DeliveryNoteDto | null = null;
  itemColumns = ['description', 'quantity', 'uom'];

  get workflowActions(): WorkflowAction[] {
    if (!this.deliveryNote) return [];
    const actions: WorkflowAction[] = [];
    switch (this.deliveryNote.status) {
      case 'Draft':
        actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
        break;
      case 'Submitted':
        actions.push({ name: 'invoice', label: 'Make Invoice', icon: 'receipt', color: 'primary' });
        actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
        break;
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.deliveryNote = result;
    });
  }

  onWorkflowAction(action: string): void {
    const id = this.deliveryNote!.id!;
    switch (action) {
      case 'submit':
        this.store.submitNote(id);
        this.reloadAfterAction();
        break;
      case 'invoice':
        this.conversionService.convertDeliveryNoteToSalesInvoice(id).subscribe((inv) => {
          this.router.navigate(['/sales/invoices', inv.id]);
        });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelNote(id);
            this.reloadAfterAction();
          }
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.deliveryNote!.id!).subscribe((result) => {
        this.deliveryNote = result;
      });
    }, 500);
  }
}
