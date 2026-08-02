import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { PickListService } from '../../proxy/inventory/pick-list.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-pick-list-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, LocalizationPipe, FormsModule, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid py-3">
      @if (!pickList()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h4 class="mb-0">
            <i class="fa fa-clipboard-list me-2 text-primary"></i>
            {{ pickList()!.pickListNumber || 'Pick List' }}
          </h4>
          <div class="btn-group">
            @if (pickList()!.status === 'Draft') {
              <a [routerLink]="['/inventory/pick-lists', pickListId, 'edit']" class="btn btn-outline-primary btn-sm">
                <i class="fa fa-edit me-1"></i>{{ '::Edit' | abpLocalization }}
              </a>
              <button class="btn btn-outline-success btn-sm" (click)="submit()">
                <i class="fa fa-paper-plane me-1"></i>{{ '::Submit' | abpLocalization }}
              </button>
            }
            @if (pickList()!.status === 'Submitted') {
              @if (!pickList()!.isFullyTransferred) {
                <button class="btn btn-outline-primary btn-sm" (click)="createDeliveryNote()">
                  <i class="fa fa-truck me-1"></i>{{ '::CreateDeliveryNote' | abpLocalization }}
                </button>
              }
              <button class="btn btn-outline-danger btn-sm" (click)="cancel()">
                <i class="fa fa-ban me-1"></i>{{ '::Cancel' | abpLocalization }}
              </button>
            }
            <button class="btn btn-outline-secondary btn-sm" (click)="goBack()">
              <i class="fa fa-arrow-left me-1"></i>{{ '::Back' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- KPIs -->
        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::Status' | abpLocalization }}</div>
              <div class="mt-1"><app-status-badge [status]="pickList()!.status || 'Draft'" /></div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::Purpose' | abpLocalization }}</div>
              <div class="fw-bold mt-1">{{ pickList()!.purpose || 'Delivery' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::Items' | abpLocalization }}</div>
              <div class="fw-bold mt-1">{{ pickList()!.items?.length || 0 }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::TransferStatus' | abpLocalization }}</div>
              <div class="mt-1">
                @if (pickList()!.isFullyTransferred) {
                  <span class="badge bg-success fs-6">{{ '::FullyTransferred' | abpLocalization }}</span>
                } @else if (pickList()!.isPartiallyTransferred) {
                  <span class="badge bg-warning fs-6">{{ '::PartiallyTransferred' | abpLocalization }}</span>
                } @else {
                  <span class="badge bg-light text-dark fs-6">{{ '::Pending' | abpLocalization }}</span>
                }
              </div>
            </div>
          </div>
        </div>

        <!-- Barcode Scan Mode (for Submitted pick lists with pending items) -->
        @if (pickList()!.status === 'Submitted' && !pickList()!.isFullyTransferred) {
          <div class="card shadow-sm mb-3 border-primary">
            <div class="card-header bg-primary bg-opacity-10 d-flex justify-content-between align-items-center">
              <span class="fw-bold"><i class="fa fa-barcode me-2"></i>{{ '::ScanToPick' | abpLocalization }}</span>
              <div>
                @if (pickedProgress() > 0) {
                  <span class="badge bg-success me-2">{{ pickedProgress() }}% {{ '::Picked' | abpLocalization }}</span>
                }
                <button class="btn btn-sm" [class.btn-primary]="!scanMode()" [class.btn-outline-primary]="scanMode()" (click)="toggleScanMode()">
                  @if (scanMode()) { <i class="fa fa-times me-1"></i>{{ '::ExitScanMode' | abpLocalization }} }
                  @else { <i class="fa fa-qrcode me-1"></i>{{ '::EnterScanMode' | abpLocalization }} }
                </button>
              </div>
            </div>
            @if (scanMode()) {
              <div class="card-body">
                <div class="input-group input-group-lg mb-2">
                  <span class="input-group-text"><i class="fa fa-barcode"></i></span>
                  <input type="text" class="form-control" [(ngModel)]="scanInput" (keydown)="onScanInput($event)"
                    [placeholder]="'::ScanBarcode' | abpLocalization" autofocus />
                </div>
                <small class="text-muted">{{ '::ScanBarcodeHint' | abpLocalization }}</small>
                @if (pickedProgress() === 100) {
                  <div class="alert alert-success mt-2 mb-0"><i class="fa fa-check-circle me-1"></i>{{ '::AllItemsPicked' | abpLocalization }}</div>
                }
              </div>
            }
          </div>
        }

        <!-- Pick List Items -->
        <div class="card shadow-sm mb-3">
          <div class="card-header"><h6 class="mb-0">{{ '::PickListItems' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>#</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th class="text-end">{{ '::PickedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TransferredQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::PendingQty' | abpLocalization }}</th>
                  <th>{{ '::Progress' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of pickList()!.items || []; track $index; let i = $index) {
                  <tr [class.table-success]="isItemScanned(item.itemId)">
                    <td class="text-muted">
                      @if (isItemScanned(item.itemId)) { <i class="fa fa-check-circle text-success"></i> }
                      @else { {{ i + 1 }} }
                    </td>
                    <td>{{ item.itemName || item.itemId || '—' }}</td>
                    <td>{{ item.warehouseName || item.warehouseId || '—' }}</td>
                    <td class="text-end">{{ item.quantity | number:'1.2-2' }}</td>
                    <td class="text-end">{{ item.transferredQty | number:'1.2-2' }}</td>
                    <td class="text-end" [class.text-warning]="item.pendingQty > 0">
                      {{ item.pendingQty | number:'1.2-2' }}
                    </td>
                    <td>
                      @if (item.quantity > 0) {
                        <div class="progress" style="height: 8px;">
                          <div class="progress-bar" [class.bg-success]="item.transferredQty >= item.quantity"
                            [class.bg-warning]="item.transferredQty > 0 && item.transferredQty < item.quantity"
                            [style.width.%]="(item.transferredQty / item.quantity) * 100"></div>
                        </div>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="7" class="text-center text-muted py-3">{{ '::NoItems' | abpLocalization }}</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        @if (pickList()!.customerId) {
          <div class="card shadow-sm mb-3">
            <div class="card-body">
              <small class="text-muted">{{ '::Customer' | abpLocalization }}</small>
              <div class="fw-bold">{{ pickList()!.customerName || pickList()!.customerId || '—' }}</div>
            </div>
          </div>
        }

        <app-activity-log documentType="PickList" [documentId]="pickListId" />
      }
    </div>
  `,
})
export class PickListDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private router = inject(Router);
  private service = inject(PickListService);
  private toaster = inject(ToasterService);
  private localization = inject(LocalizationService);

  pickList = signal<any>(null);
  pickListId = '';
  scanInput = '';
  scanMode = signal(false);
  scannedItems = signal<Set<string>>(new Set());

  pickedProgress = computed(() => {
    const items = this.pickList()?.items ?? [];
    if (items.length === 0) return 0;
    const picked = items.filter((i: any) => this.scannedItems().has(i.itemId)).length;
    return Math.round((picked / items.length) * 100);
  });

  ngOnInit() {
    this.pickListId = this.route.snapshot.params['id'];
    this.loadData();
  }

  loadData() {
    this.service.get(this.pickListId).subscribe({
      next: (data) => this.pickList.set(data),
      error: () => this.toaster.error(this.l('::FailedToLoad')),
    });
  }

  submit() {
    this.service.submit(this.pickListId).subscribe({
      next: () => { this.toaster.success(this.l('::SuccessfullySubmitted')); this.loadData(); },
      error: () => {},
    });
  }

  createDeliveryNote() {
    this.service.createDeliveryNoteFromPickList(this.pickListId).subscribe({
      next: () => { this.toaster.success(this.l('::SuccessfullyCreated')); this.loadData(); },
      error: () => {},
    });
  }

  cancel() {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(this.pickListId).subscribe({
        next: () => { this.toaster.success(this.l('::SuccessfullyCancelled')); this.loadData(); },
        error: () => {},
      });
    });
  }

  goBack() { this.router.navigate(['/inventory/pick-lists']); }

  toggleScanMode() { this.scanMode.set(!this.scanMode()); }

  onScanInput(event: KeyboardEvent) {
    if (event.key !== 'Enter' || !this.scanInput.trim()) return;
    const barcode = this.scanInput.trim();
    this.scanInput = '';
    const items = this.pickList()?.items ?? [];
    const match = items.find((i: any) => i.itemCode === barcode || i.itemId === barcode || i.itemName?.toLowerCase().includes(barcode.toLowerCase()));
    if (match) {
      const s = new Set(this.scannedItems());
      s.add(match.itemId);
      this.scannedItems.set(s);
      this.toaster.success(this.l('::ItemScanned') + ': ' + (match.itemName || match.itemCode));
    } else {
      this.toaster.warn(this.l('::ItemNotFoundInPickList') + ': ' + barcode);
    }
  }

  isItemScanned(itemId: string): boolean { return this.scannedItems().has(itemId); }

  private l(key: string): string { return this.localization.instant(key); }
}
