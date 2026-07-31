import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-material-shortage-summary',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  template: `
    <div class="card border-warning">
      <div class="card-header d-flex justify-content-between align-items-center bg-warning bg-opacity-10">
        <span class="fw-bold"><i class="fa fa-triangle-exclamation me-2 text-warning"></i>{{ '::MaterialShortages' | abpLocalization }}</span>
        @if (shortage()?.totalItemsShort) {
          <span class="badge bg-warning text-dark">{{ shortage()!.totalItemsShort }} {{ '::Items' | abpLocalization }}</span>
        }
      </div>
      <div class="card-body">
        @if (isLoading()) {
          <div class="text-center py-3"><span class="spinner-border spinner-border-sm"></span></div>
        } @else if (shortage()?.items?.length) {
          <div class="table-responsive">
            <table class="table table-sm table-hover mb-0">
              <thead>
                <tr class="table-light">
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Required' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Available' | abpLocalization }}</th>
                  <th class="text-end text-danger">{{ '::Shortage' | abpLocalization }}</th>
                  <th class="text-center">{{ '::AffectedWOs' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of shortage()!.items; track item.itemId) {
                  <tr>
                    <td>
                      <span class="fw-medium">{{ item.itemName }}</span>
                      <br><small class="text-muted">{{ item.itemCode }}</small>
                    </td>
                    <td class="text-end font-monospace">{{ item.totalRequired | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ item.totalAvailable | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace text-danger fw-bold">{{ item.shortageQty | number:'1.2-2' }}</td>
                    <td class="text-center"><span class="badge bg-secondary">{{ item.affectedWorkOrders }}</span></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (shortage()!.totalAffectedOrders > 0) {
            <div class="mt-2 text-end">
              <small class="text-muted">
                {{ shortage()!.totalAffectedOrders }} {{ '::OrdersAffected' | abpLocalization }}
              </small>
            </div>
          }
        } @else {
          <div class="text-center text-muted py-3">
            <i class="fa fa-check-circle fa-2x text-success mb-2"></i>
            <p class="mb-0">{{ '::NoMaterialShortages' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>
  `
})
export class MaterialShortageSummaryComponent implements OnInit {
  private service = inject(ManufacturingService);
  private companyContext = inject(CompanyContextService);

  shortage = signal<any>(null);
  isLoading = signal(false);

  ngOnInit() {
    const companyId = this.companyContext.currentCompanyId();
    if (companyId) {
      this.loadShortage(companyId);
    }
  }

  private loadShortage(companyId: string) {
    this.isLoading.set(true);
    this.service.getMaterialShortageAcrossOrders(companyId).subscribe({
      next: data => { this.shortage.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false)
    });
  }
}
