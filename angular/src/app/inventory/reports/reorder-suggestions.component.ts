import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemService } from '../../proxy/inventory/item.service';
import type { ReorderSuggestionDto } from '../../proxy/inventory/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-reorder-suggestions',
  standalone: true,
  imports: [CommonModule, LocalizationPipe, RouterLink],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4><i class="fas fa-chart-line me-2"></i>{{ '::ReorderSuggestions' | abpLocalization }}</h4>
        <div class="btn-group">
          <button class="btn btn-sm btn-outline-primary" (click)="loadSuggestions(90)" [class.active]="lookback() === 90">90 Days</button>
          <button class="btn btn-sm btn-outline-primary" (click)="loadSuggestions(180)" [class.active]="lookback() === 180">180 Days</button>
          <button class="btn btn-sm btn-outline-primary" (click)="loadSuggestions(365)" [class.active]="lookback() === 365">365 Days</button>
        </div>
      </div>

      <!-- KPI Summary -->
      @if (suggestions().length > 0) {
        <div class="row g-3 mb-3">
          <div class="col-md-4">
            <div class="card text-center border-0 shadow-sm">
              <div class="card-body">
                <div class="text-muted small">{{ '::AnalyzedItems' | abpLocalization }}</div>
                <div class="h3 fw-bold">{{ suggestions().length }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card text-center border-0 shadow-sm border-start border-danger border-3">
              <div class="card-body">
                <div class="text-muted small">{{ '::UnderstockedItems' | abpLocalization }}</div>
                <div class="h3 fw-bold text-danger">{{ understockedCount() }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card text-center border-0 shadow-sm border-start border-warning border-3">
              <div class="card-body">
                <div class="text-muted small">{{ '::OverstockedItems' | abpLocalization }}</div>
                <div class="h3 fw-bold text-warning">{{ overstockedCount() }}</div>
              </div>
            </div>
          </div>
        </div>
      }

      @if (loading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status"></div>
        </div>
      } @else if (suggestions().length > 0) {
        <div class="card border-0 shadow-sm">
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::CurrentStock' | abpLocalization }}</th>
                    <th class="text-end">{{ '::DailyUsage' | abpLocalization }}</th>
                    <th class="text-end">{{ '::LeadTimeDays' | abpLocalization }}</th>
                    <th class="text-end">{{ '::CurrentReorderLevel' | abpLocalization }}</th>
                    <th class="text-end">{{ '::SuggestedReorderLevel' | abpLocalization }}</th>
                    <th class="text-end">{{ '::SuggestedReorderQty' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of suggestions(); track item.itemId) {
                    <tr>
                      <td>
                        @if (item.itemId) {
                          <a [routerLink]="['/inventory/items', item.itemId]" class="fw-bold text-decoration-none">
                            {{ item.itemCode }}
                          </a>
                        } @else {
                          <span class="fw-bold">{{ item.itemCode }}</span>
                        }
                        <div class="text-muted small">{{ item.itemName }}</div>
                      </td>
                      <td class="text-end font-monospace">{{ item.currentStock | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace">{{ item.avgDailyConsumption | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace">{{ item.leadTimeDays }}</td>
                      <td class="text-end font-monospace">{{ item.currentReorderLevel | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace fw-bold"
                          [class.text-danger]="item.isUnderstocked"
                          [class.text-warning]="item.isOverstocked">
                        {{ item.suggestedReorderLevel | number:'1.2-2' }}
                      </td>
                      <td class="text-end font-monospace">{{ item.suggestedReorderQty | number:'1.2-2' }}</td>
                      <td class="text-center">
                        @if (item.isUnderstocked) {
                          <span class="badge bg-danger-subtle text-danger">{{ '::Understocked' | abpLocalization }}</span>
                        } @else if (item.isOverstocked) {
                          <span class="badge bg-warning-subtle text-warning">{{ '::Overstocked' | abpLocalization }}</span>
                        } @else {
                          <span class="badge bg-success-subtle text-success">{{ '::Optimal' | abpLocalization }}</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      } @else {
        <div class="card border-0 shadow-sm text-center py-5 text-muted">
          <i class="fas fa-box-open fa-3x mb-3"></i>
          <p>{{ '::NoReorderSuggestionsFound' | abpLocalization }}</p>
        </div>
      }
    </div>
  `,
})
export class ReorderSuggestionsComponent implements OnInit {
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);

  suggestions = signal<ReorderSuggestionDto[]>([]);
  loading = signal(false);
  lookback = signal(90);

  understockedCount = () => this.suggestions().filter(s => s.isUnderstocked).length;
  overstockedCount = () => this.suggestions().filter(s => s.isOverstocked).length;

  ngOnInit() {
    this.loadSuggestions(90);
  }

  loadSuggestions(days: number) {
    this.lookback.set(days);
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.itemService.getReorderSuggestions(companyId, days).subscribe({
      next: (data) => { this.suggestions.set(data ?? []); this.loading.set(false); },
      error: () => { this.suggestions.set([]); this.loading.set(false); },
    });
  }
}
