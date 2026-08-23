import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { PackingSlipService } from '../../proxy/sales/packing-slip.service';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import type { CreatePackingSlipDto, CreatePackingSlipItemDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-packing-slip-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    @if (isLoading) {
      <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
    } @else {
      <abp-page [title]="'PackingSlip' | abpLocalization">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'DeliveryNote' | abpLocalization }}</label>
              <input class="form-control" [value]="deliveryNote?.deliveryNumber" disabled />
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'CaseNumbers' | abpLocalization }} From</label>
              <input type="number" class="form-control" [(ngModel)]="fromCaseNo" min="1" />
            </div>
            <div class="col-md-2">
              <label class="form-label">To</label>
              <input type="number" class="form-control" [(ngModel)]="toCaseNo" min="1" />
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'GrossWeight' | abpLocalization }}</label>
              <input type="number" class="form-control" [(ngModel)]="grossWeightKg" min="0" step="0.01" />
            </div>
            <div class="col-md-2">
              <label class="form-label">UOM</label>
              <input class="form-control" [(ngModel)]="weightUom" />
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'Items' | abpLocalization }}</h6>
          <table class="table table-sm mb-0">
            <thead><tr>
              <th>{{ 'Item' | abpLocalization }}</th>
              <th>{{ 'Description' | abpLocalization }}</th>
              <th class="text-end" style="width:130px">{{ 'Quantity' | abpLocalization }}</th>
              <th class="text-end" style="width:150px">{{ 'NetWeight' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (row of rows; track row.itemId) {
                <tr>
                  <td>{{ row.itemId }}</td>
                  <td><input class="form-control form-control-sm" [(ngModel)]="row.description" /></td>
                  <td class="text-end"><input type="number" class="form-control form-control-sm text-end" [(ngModel)]="row.qty" min="0.0001" /></td>
                  <td class="text-end"><input type="number" class="form-control form-control-sm text-end" [(ngModel)]="row.netWeight" min="0" step="0.01" /></td>
                </tr>
              }
              @if (rows.length === 0) {
                <tr><td colspan="4" class="text-center text-muted py-3">No un-packed items remaining on this Delivery Note.</td></tr>
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
export class PackingSlipFormComponent implements OnInit {
  private route = inject(ActivatedRoute);
  router = inject(Router);
  private service = inject(PackingSlipService);
  private deliveryNoteService = inject(DeliveryNoteService);
  private toaster = inject(ToasterService);
  private localization = inject(LocalizationService);

  deliveryNoteId = '';
  deliveryNote: any = null;
  rows: CreatePackingSlipItemDto[] = [];
  fromCaseNo = 1;
  toCaseNo = 1;
  grossWeightKg = 0;
  weightUom = 'Kg';
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
        this.rows = (dn.items ?? [])
          .filter(i => !!i.itemId && (i.quantity ?? 0) - (i.packedQty ?? 0) > 0)
          .map(i => ({
            itemId: i.itemId!,
            qty: (i.quantity ?? 0) - (i.packedQty ?? 0),
            netWeight: 0,
            description: i.description ?? '',
            deliveryNoteItemId: i.id ?? null,
          }));
        this.service.getNextCaseNo(this.deliveryNoteId).subscribe(n => { this.fromCaseNo = n; this.toCaseNo = n; });
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  save() {
    if (!this.deliveryNote) return;
    this.isSaving = true;
    const input: CreatePackingSlipDto = {
      companyId: this.deliveryNote.companyId,
      deliveryNoteId: this.deliveryNoteId,
      fromCaseNo: this.fromCaseNo,
      toCaseNo: this.toCaseNo,
      grossWeightKg: this.grossWeightKg,
      weightUom: this.weightUom,
      items: this.rows.filter(r => (r.qty ?? 0) > 0),
    };
    this.service.create(input).subscribe({
      next: slip => {
        this.toaster.success(this.l('::SuccessfullyCreated'));
        this.router.navigate(['/sales/packing-slips', slip.id]);
      },
      error: () => { this.isSaving = false; },
    });
  }

  private l(key: string): string { return this.localization.instant(key); }
}
