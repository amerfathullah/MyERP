import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { HttpClient } from '@angular/common/http';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

interface ComparisonSupplier {
  supplierId: string;
  supplierName: string;
  quotationId: string;
  quotationNumber: string;
  currency: string;
  validTill: string | null;
  grandTotal: number;
}

interface ComparisonPrice {
  supplierId: string;
  quotationId: string;
  rate: number;
  quantity: number;
  amount: number;
  leadTimeDays: number | null;
  isQuoted: boolean;
  isLowestPrice: boolean;
}

interface ComparisonItem {
  itemId: string;
  itemDescription: string;
  supplierPrices: ComparisonPrice[];
  lowestRate: number;
}

interface ComparisonResult {
  rfqId: string | null;
  suppliers: ComparisonSupplier[];
  items: ComparisonItem[];
  lowestTotalAmount: number;
}

@Component({
  selector: 'app-sq-comparison',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent, LoadingOverlayComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">
            <i class="fas fa-balance-scale me-2"></i>{{ 'Purchasing::SupplierQuotationComparison' | abpLocalization }}
          </h5>
          @if (!comparison()) {
            <button class="btn btn-primary btn-sm" (click)="loadComparison()" [disabled]="isLoading()">
              <i class="fas fa-search me-1"></i>{{ '::Compare' | abpLocalization }}
            </button>
          }
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <app-loading-overlay />
          } @else if (!comparison()) {
            <!-- Selection UI -->
            <div class="mb-3">
              <label class="form-label fw-bold">{{ '::SelectQuotations' | abpLocalization }}</label>
              <p class="text-muted small">Select at least 2 supplier quotations to compare side-by-side.</p>
            </div>
            @if (availableQuotations().length > 0) {
              <div class="table-responsive">
                <table class="table table-sm table-hover">
                  <thead class="table-light">
                    <tr>
                      <th style="width:40px"><input type="checkbox" class="form-check-input" (change)="toggleAll($event)" /></th>
                      <th>{{ '::QuotationNumber' | abpLocalization }}</th>
                      <th>{{ '::Supplier' | abpLocalization }}</th>
                      <th>{{ '::Date' | abpLocalization }}</th>
                      <th class="text-end">{{ '::GrandTotal' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (sq of availableQuotations(); track sq.id) {
                      <tr>
                        <td><input type="checkbox" class="form-check-input" [checked]="selectedIds().has(sq.id)" (change)="toggleSelection(sq.id, $event)" /></td>
                        <td>{{ sq.quotationNumber || sq.id.substring(0, 8) }}</td>
                        <td>{{ sq.supplierName }}</td>
                        <td>{{ sq.transactionDate | date:'dd/MM/yyyy' }}</td>
                        <td class="text-end">{{ sq.grandTotal | number:'1.2-2' }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              <button class="btn btn-primary" (click)="loadComparison()" [disabled]="selectedIds().size < 2">
                <i class="fas fa-balance-scale me-1"></i>Compare Selected ({{ selectedIds().size }})
              </button>
            } @else {
              <div class="text-center py-4 text-muted">
                <i class="fas fa-file-alt fa-3x mb-2 d-block"></i>
                No supplier quotations available. Create quotations from Request for Quotation first.
              </div>
            }
          } @else {
            <!-- Comparison Matrix -->
            <div class="mb-3 d-flex justify-content-between align-items-center">
              <span class="text-muted">Comparing {{ comparison()!.suppliers.length }} suppliers across {{ comparison()!.items.length }} items</span>
              <button class="btn btn-outline-secondary btn-sm" (click)="comparison.set(null)">
                <i class="fas fa-arrow-left me-1"></i>Back to Selection
              </button>
            </div>

            <!-- Supplier Summary Cards -->
            <div class="row mb-4 g-3">
              @for (supplier of comparison()!.suppliers; track supplier.supplierId) {
                <div class="col-md-4 col-lg-3">
                  <div class="card h-100" [class.border-success]="supplier.grandTotal === comparison()!.lowestTotalAmount">
                    <div class="card-body text-center">
                      @if (supplier.grandTotal === comparison()!.lowestTotalAmount) {
                        <span class="badge bg-success mb-2">Lowest Total</span>
                      }
                      <h6 class="card-title mb-1">{{ supplier.supplierName }}</h6>
                      <p class="text-muted small mb-1">{{ supplier.quotationNumber }}</p>
                      <div class="fs-5 fw-bold">{{ supplier.currency }} {{ supplier.grandTotal | number:'1.2-2' }}</div>
                      @if (supplier.validTill) {
                        <small class="text-muted">Valid till {{ supplier.validTill | date:'dd/MM/yyyy' }}</small>
                      }
                    </div>
                  </div>
                </div>
              }
            </div>

            <!-- Item-wise Comparison Table -->
            <div class="table-responsive">
              <table class="table table-bordered table-sm">
                <thead class="table-dark">
                  <tr>
                    <th class="bg-dark text-white" style="min-width:200px">Item</th>
                    @for (supplier of comparison()!.suppliers; track supplier.supplierId) {
                      <th class="text-center bg-dark text-white" style="min-width:140px">{{ supplier.supplierName }}</th>
                    }
                    <th class="text-center bg-dark text-white" style="width:100px">Lowest</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of comparison()!.items; track item.itemId) {
                    <tr>
                      <td class="fw-medium">{{ item.itemDescription }}</td>
                      @for (price of item.supplierPrices; track price.supplierId) {
                        <td class="text-center" [class.table-success]="price.isLowestPrice" [class.text-muted]="!price.isQuoted">
                          @if (price.isQuoted) {
                            <div class="fw-bold" [class.text-success]="price.isLowestPrice">
                              {{ price.rate | number:'1.2-4' }}
                              @if (price.isLowestPrice) { <i class="fas fa-check-circle ms-1"></i> }
                            </div>
                            <small class="text-muted">Qty: {{ price.quantity | number:'1.0-2' }}</small>
                            @if (price.leadTimeDays) {
                              <br /><small class="text-info">{{ price.leadTimeDays }}d lead</small>
                            }
                          } @else {
                            <span class="text-muted">—</span>
                          }
                        </td>
                      }
                      <td class="text-center fw-bold text-success">{{ item.lowestRate | number:'1.2-4' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <!-- Create PO from lowest supplier -->
            <div class="mt-3">
              <button class="btn btn-success" (click)="createPoFromLowest()">
                <i class="fas fa-shopping-cart me-1"></i>Create Purchase Order from Best Supplier
              </button>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .table-success { background-color: rgba(25, 135, 84, 0.1) !important; }
  `],
})
export class SupplierQuotationComparisonComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);

  isLoading = signal(false);
  comparison = signal<ComparisonResult | null>(null);
  availableQuotations = signal<any[]>([]);
  selectedIds = signal<Set<string>>(new Set());
  rfqId: string | null = null;

  ngOnInit(): void {
    this.rfqId = this.route.snapshot.queryParamMap.get('rfqId');
    if (this.rfqId) {
      this.loadComparison();
    } else {
      this.loadAvailableQuotations();
    }
  }

  loadAvailableQuotations(): void {
    this.http.get<any>('/api/app/supplier-quotation', {
      params: { skipCount: '0', maxResultCount: '100', sorting: 'transactionDate desc' }
    }).subscribe({
      next: res => this.availableQuotations.set(res.items ?? []),
      error: () => this.toaster.error('Failed to load quotations'),
    });
  }

  toggleSelection(id: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const ids = new Set(this.selectedIds());
    if (checked) ids.add(id); else ids.delete(id);
    this.selectedIds.set(ids);
  }

  toggleAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      this.selectedIds.set(new Set(this.availableQuotations().map(sq => sq.id)));
    } else {
      this.selectedIds.set(new Set());
    }
  }

  loadComparison(): void {
    this.isLoading.set(true);

    if (this.rfqId) {
      this.http.get<ComparisonResult>(`/api/app/supplier-quotation-comparison/by-rfq/${this.rfqId}`)
        .subscribe({
          next: result => { this.comparison.set(result); this.isLoading.set(false); },
          error: () => { this.toaster.error('Failed to load comparison'); this.isLoading.set(false); },
        });
    } else {
      const ids = Array.from(this.selectedIds());
      this.http.post<ComparisonResult>('/api/app/supplier-quotation-comparison/by-ids', ids)
        .subscribe({
          next: result => { this.comparison.set(result); this.isLoading.set(false); },
          error: () => { this.toaster.error('Failed to load comparison'); this.isLoading.set(false); },
        });
    }
  }

  createPoFromLowest(): void {
    if (!this.comparison()) return;
    // Find the supplier with the lowest grand total
    const lowestSupplier = this.comparison()!.suppliers
      .reduce((a, b) => a.grandTotal <= b.grandTotal ? a : b);
    // Navigate to PO creation with pre-fill from this quotation
    window.location.href = `/purchasing/purchase-orders/new?fromSQ=${lowestSupplier.quotationId}`;
  }
}
