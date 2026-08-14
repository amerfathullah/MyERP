import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { QualityGoalStore } from '../store/quality-goal.store';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-quality-goal-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'QualityGoals' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div class="d-flex gap-2">
            <input
              type="text"
              class="form-control form-control-sm"
              style="max-width: 250px"
              [placeholder]="'::Placeholder:Search' | abpLocalization"
              [(ngModel)]="searchTerm"
              (keyup.enter)="onSearch()"
            />
          </div>
          <a routerLink="/inventory/quality-goals/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ 'NewQualityGoal' | abpLocalization }}
          </a>
        </div>
        <div class="card-body p-0">
          <div class="table-responsive">
            <table class="table table-hover table-striped mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::Frequency' | abpLocalization }}</th>
                  <th>{{ '::TargetValue' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @if (store.isLoading()) {
                  <tr>
                    <td colspan="5" class="text-center py-4">
                      <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
                    </td>
                  </tr>
                } @else if (filteredGoals().length === 0) {
                  <tr>
                    <td colspan="5" class="text-center py-4 text-muted">
                      {{ '::NoData' | abpLocalization }}
                    </td>
                  </tr>
                } @else {
                  @for (goal of filteredGoals(); track goal.id) {
                    <tr>
                      <td class="fw-semibold">
                        <a [routerLink]="['/inventory/quality-goals', goal.id]">{{ goal.name }}</a>
                      </td>
                      <td>{{ goal.frequency }}</td>
                      <td>{{ goal.targetValue }} {{ goal.uom }}</td>
                      <td>
                        <span class="badge" [ngClass]="goal.isEnabled ? 'bg-success' : 'bg-secondary'">
                          {{ (goal.isEnabled ? '::Active' : '::Inactive') | abpLocalization }}
                        </span>
                      </td>
                      <td class="text-end">
                        <a [routerLink]="['/inventory/quality-goals', goal.id]" class="btn btn-outline-secondary btn-sm me-1">
                          <i class="fa fa-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger btn-sm" (click)="deleteGoal(goal.id, goal.name)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        </div>
        <div class="card-footer d-flex justify-content-between align-items-center">
          <span class="text-muted small">Total: {{ store.totalCount() }}</span>
          <app-pagination
            [totalCount]="store.totalCount()"
            [currentPage]="currentPage"
            [pageSize]="pageSize"
            (pageChange)="onPageChange($event)"
          ></app-pagination>
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityGoalListComponent implements OnInit {
  protected readonly store = inject(QualityGoalStore);
  private readonly confirmation = inject(ConfirmationService);

  currentPage = 0;
  pageSize = 10;
  searchTerm = '';

  ngOnInit() {
    this.loadGoals();
  }

  loadGoals() {
    this.store.load({
      maxResultCount: this.pageSize,
      skipCount: this.currentPage * this.pageSize,
    });
  }

  onSearch() {
    this.currentPage = 0;
    this.loadGoals();
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadGoals();
  }

  filteredGoals() {
    const term = this.searchTerm.toLowerCase().trim();
    const entities = this.store.entities();
    if (!term) return entities;
    return entities.filter(g => (g.name ?? '').toLowerCase().includes(term));
  }

  deleteGoal(id: string, name?: string) {
    this.confirmation.warn('::AreYouSureToDelete', name ?? 'Goal').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.remove({ id, onSuccess: () => this.loadGoals() });
      }
    });
  }
}
