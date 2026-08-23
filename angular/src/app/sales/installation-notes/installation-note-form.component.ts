import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { InstallationNoteService } from '../../proxy/sales/installation-note.service';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import type { CreateInstallationNoteDto, InstallationNoteItemDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-installation-note-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    @if (isLoading) { <app-loading-overlay /> }
    @if (!isLoading) {
      <abp-page [title]="'InstallationNotes' | abpLocalization">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'Customer' | abpLocalization }}</label>
              <input class="form-control" [value]="deliveryNote?.customerName || deliveryNote?.customerId" disabled />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'DeliveryNote' | abpLocalization }}</label>
              <input class="form-control" [value]="deliveryNote?.deliveryNumber" disabled />
            </div>
            <div class="col-md-4">
              <label class="form-label">Installation Date</label>
              <input type="date" class="form-control" [(ngModel)]="installationDate" [min]="minDate" />
            </div>
            <div class="col-12">
              <label class="form-label">{{ 'Remarks' | abpLocalization }}</label>
              <input class="form-control" [(ngModel)]="remarks" />
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <h6 class="card-title">Installed Items</h6>
          <table class="table table-sm mb-0">
            <thead><tr>
              <th>{{ 'Item' | abpLocalization }}</th>
              <th class="text-end" style="width:150px">{{ 'Quantity' | abpLocalization }}</th>
              <th style="width:200px">Serial No</th>
            </tr></thead>
            <tbody>
              @for (row of rows; track row.itemId) {
                <tr>
                  <td>{{ row.itemId }}</td>
                  <td class="text-end"><input type="number" class="form-control form-control-sm text-end" [(ngModel)]="row.qty" min="0.0001" /></td>
                  <td><input class="form-control form-control-sm" [(ngModel)]="row.serialNo" /></td>
                </tr>
              }
              @if (rows.length === 0) {
                <tr><td colspan="3" class="text-center text-muted py-3">No items available from this Delivery Note.</td></tr>
              }
            </tbody>
          </table>
        </div></div>

        <div class="d-flex gap-2">
          <button class="btn btn-primary" [disabled]="isSaving || rows.length === 0" (click)="save()">
            <i class="fa fa-check me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
          <button class="btn btn-outline-secondary" (click)="router.navigate(['/sales/delivery-notes', deliveryNoteId])">
            {{ 'Cancel' | abpLocalization }}
          </button>
        </div>
      </abp-page>
    }
  `,
})
export class InstallationNoteFormComponent implements OnInit {
  private route = inject(ActivatedRoute);
  router = inject(Router);
  private service = inject(InstallationNoteService);
  private deliveryNoteService = inject(DeliveryNoteService);
  private toaster = inject(ToasterService);
  private localization = inject(LocalizationService);

  deliveryNoteId = '';
  deliveryNote: any = null;
  rows: InstallationNoteItemDto[] = [];
  installationDate = new Date().toISOString().split('T')[0];
  minDate = '';
  remarks = '';
  isLoading = false;
  isSaving = false;

  ngOnInit() {
    this.deliveryNoteId = this.route.snapshot.queryParamMap.get('deliveryNoteId') ?? '';
    if (!this.deliveryNoteId) {
      this.toaster.error('::ValidationFailed');
      this.router.navigate(['/sales/delivery-notes']);
      return;
    }
    this.isLoading = true;
    this.deliveryNoteService.get(this.deliveryNoteId).subscribe({
      next: dn => {
        this.deliveryNote = dn;
        this.minDate = (dn.postingDate ?? '').split('T')[0];
        if (this.minDate && this.installationDate < this.minDate) this.installationDate = this.minDate;
        this.rows = (dn.items ?? [])
          .filter(i => !!i.itemId)
          .map(i => ({ itemId: i.itemId!, qty: i.quantity ?? 0, serialNo: '' }));
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  save() {
    if (!this.deliveryNote) return;
    this.isSaving = true;
    const input: CreateInstallationNoteDto = {
      companyId: this.deliveryNote.companyId,
      customerId: this.deliveryNote.customerId,
      deliveryNoteId: this.deliveryNoteId,
      installationDate: this.installationDate,
      items: this.rows.filter(r => (r.qty ?? 0) > 0),
    };
    this.service.create(input).subscribe({
      next: note => {
        this.toaster.success(this.l('::SuccessfullyCreated'));
        this.router.navigate(['/sales/installation-notes', note.id]);
      },
      error: () => { this.isSaving = false; },
    });
  }

  private l(key: string): string { return this.localization.instant(key); }
}
