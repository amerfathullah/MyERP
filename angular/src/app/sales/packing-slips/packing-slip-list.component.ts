import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PackingSlipService } from '../../proxy/sales/packing-slip.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

interface PackingSlipDto {
  id?: string;
  deliveryNoteId?: string;
  deliveryNoteNumber?: string;
  fromCaseNo?: number;
  toCaseNo?: number;
  status?: number;
  netWeightKg?: number;
  grossWeightKg?: number;
  creationTime?: string;
}

@Component({
  selector: 'app-packing-slip-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ '::PackingSlips' | abpLocalization }}</h5>
          <a routerLink="/sales/packing-slips/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ '::NewPackingSlip' | abpLocalization }}
          </a>
        </div>

        <div class="card-body p-0">
          @if (slips().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-box fa-3x text-muted mb-3"></i>
              <p class="text-muted mb-3">{{ '::NoPackingSlipsYet' | abpLocalization }}</p>
              <a routerLink="/sales/packing-slips/new" class="btn btn-outline-primary btn-sm">
                <i class="fa fa-plus me-1"></i>{{ '::NewPackingSlip' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::CaseNumbers' | abpLocalization }}</th>
                  <th>{{ '::DeliveryNote' | abpLocalization }}</th>
                  <th>{{ '::NetWeight' | abpLocalization }}</th>
                  <th>{{ '::GrossWeight' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (slip of slips(); track slip.id) {
                  <tr>
                    <td><strong>{{ slip.fromCaseNo }}–{{ slip.toCaseNo }}</strong></td>
                    <td>{{ slip.deliveryNoteNumber || slip.deliveryNoteId?.substring(0, 8) }}</td>
                    <td>{{ slip.netWeightKg | number:'1.2-2' }} kg</td>
                    <td>{{ slip.grossWeightKg | number:'1.2-2' }} kg</td>
                    <td>
                      @switch (slip.status) {
                        @case (0) { <span class="badge bg-secondary">Draft</span> }
                        @case (1) { <span class="badge bg-primary">Submitted</span> }
                        @case (2) { <span class="badge bg-danger">Cancelled</span> }
                      }
                    </td>
                    <td>{{ slip.creationTime | date:'dd/MM/yyyy' }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (slip.status === 0) {
                          <button class="btn btn-outline-success" (click)="submit(slip)"><i class="fa fa-check"></i></button>
                        }
                        @if (slip.status === 1) {
                          <button class="btn btn-outline-danger" (click)="cancel(slip)"><i class="fa fa-times"></i></button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>

        @if (totalCount() > 10) {
          <div class="card-footer">
            <app-pagination [totalCount]="totalCount()" [pageSize]="10" [currentPage]="currentPage()" (pageChange)="onPageChange($event)" />
          </div>
        }
      </div>
    </div>
  `
})
export class PackingSlipListComponent implements OnInit {
  private packingSlipService = inject(PackingSlipService);
  private toaster = inject(ToasterService);

  slips = signal<PackingSlipDto[]>([]);
  totalCount = signal(0);
  currentPage = signal(1);

  ngOnInit() { this.loadData(); }

  loadData() {
    const skip = (this.currentPage() - 1) * 10;
    this.packingSlipService.getList({ skipCount: skip, maxResultCount: 10, sorting: '' } as any).subscribe(res => {
      this.slips.set(res.items ?? []);
      this.totalCount.set(res.totalCount ?? 0);
    });
  }

  submit(slip: PackingSlipDto) {
    this.packingSlipService.submit(slip.id).subscribe({
      next: () => { this.toaster.success('Submitted'); this.loadData(); },
      error: () => {}
    });
  }

  cancel(slip: PackingSlipDto) {
    if (!confirm('Cancel this packing slip?')) return;
    this.packingSlipService.cancel(slip.id).subscribe({
      next: () => { this.toaster.success('Cancelled'); this.loadData(); },
      error: () => {}
    });
  }

  onPageChange(event: any) {
    this.currentPage.set(event.pageIndex);
    this.loadData();
  }
}
