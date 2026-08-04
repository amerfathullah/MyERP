import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
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
          <div class="col-md-3">
            <div class="card border-start border-4 border-primary">
              <div class="card-body py-2">
                <small class="text-muted">Total Items</small>
                <h5 class="mb-0">{{ suggestions().length }}</h5>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-4 border-danger">
              <div class="card-body py-2">
                <small class="text-muted">{{ '::Understocked' | abpLocalization }}</small>
                <h5 class="mb-0 text-danger">{{ understockedCount() }}</h5>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-4 border-warning">
              <div class="card-body py-2">
                <small class="text-muted">{{ '::Overstocked' | abpLocalization }}</small>
                <h5 class="mb-0 text-warning">{{ overstockedCount() }}</h5>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-4 border-success">
              <div class="card-body py-2">
                <small class="text-muted">Balanced</small>
                <h5 class="mb-0 text-success">{{ suggestions().length - understockedCount() - overstockedCount() }}</h5>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Loading -->
      @if (loading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary"></div>
        </div>
      }

      <!-- Empty state -->
      @if (!loading() && suggestions().length === 0) {
        <div class="text-center py-5 text-muted">
          <i class="fas fa-chart-line fa-3x mb-3 opacity-25"></i>
          <p>{{ '::NoReorderSuggestionsYet' | abpLocalization }}</p>
        </div>
      }

      <!-- Suggestions table -->
      @if (!loading() && suggestions().length > 0) {
        <div class="card">
          <div class="table-responsive">
            <table class="table table-sm table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th class="text-end">{{ '::AvgDailyConsumption' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CurrentStock' | abpLocalization }}</th>
                  <th class="text-end">{{ '::DaysOfStockRemaining' | abpLocalization }}</th>
                  <th class="text-end">Current Level</th>
                  <th class="text-end">{{ '::SuggestedLevel' | abpLocalization }}</th>
                  <th class="text-end">{{ '::SuggestedReorderQty' | abpLocalization }}</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                @for (s of suggestions(); track s.itemId) {
                  <tr [class.table-danger]="s.isUnderstocked" [class.table-warning]="s.isOverstocked">
                    <td><a [routerLink]="['/inventory/items', s.itemId]" class="text-decoration-none">{{ s.itemCode }}</a></td>
                    <td>{{ s.itemName }}</td>
                    <td class="text-end font-monospace">{{ s.avgDailyConsumption | number:'1.1-1' }}</td>
                    <td class="text-end font-monospace">{{ s.currentStock | number:'1.0-0' }}</td>
                    <td class="text-end">
                      <span [class.text-danger]="s.daysOfStockRemaining <= 7"
                            [class.text-warning]="s.daysOfStockRemaining > 7 && s.daysOfStockRemaining <= 14"
                            [class.fw-bold]="s.daysOfStockRemaining <= 7">
                        {{ s.daysOfStockRemaining }} days
                      </span>
                    </td>
                    <td class="text-end font-monospace">{{ s.currentReorderLevel | number:'1.0-0' }}</td>
                    <td class="text-end font-monospace fw-bold">{{ s.suggestedReorderLevel | number:'1.0-0' }}</td>
                    <td class="text-end font-monospace">{{ s.suggestedReorderQty | number:'1.0-0' }}</td>
                    <td>
                      @if (s.isUnderstocked) {
                        <span class="badge bg-danger"><i class="fa fa-arrow-down me-1"></i>{{ '::Understocked' | abpLocalization }}</span>
                      } @else if (s.isOverstocked) {
                        <span class="badge bg-warning text-dark"><i class="fa fa-arrow-up me-1"></i>{{ '::Overstocked' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-success"><i class="fa fa-check me-1"></i>OK</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </div>
  `,
})
export class ReorderSuggestionsComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);

  suggestions = signal<any[]>([]);
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
    this.http.get<any[]>(`/api/app/item/reorder-suggestions?companyId=${companyId}&lookbackDays=${days}`).subscribe({
      next: (data) => { this.suggestions.set(data ?? []); this.loading.set(false); },
      error: () => { this.suggestions.set([]); this.loading.set(false); },
    });
  }
}
