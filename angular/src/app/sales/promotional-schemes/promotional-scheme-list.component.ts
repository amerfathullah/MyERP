import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { PromotionalSchemeService } from '../../proxy/sales/promotional-scheme.service';
import { pricingRuleApplyOnOptions } from '../../proxy/sales/pricing-rule-apply-on.enum';
import type { PromotionalSchemeDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-promotional-scheme-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent],
  template: `
    <abp-page [title]="'PromotionalSchemes' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/sales/promotional-schemes/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewPromotionalScheme' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) {
        <app-loading-overlay />
      }

      @if (!isLoading && schemes.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-tags fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoPromotionalSchemesYet' | abpLocalization }}</p>
          <button class="btn btn-primary" routerLink="/sales/promotional-schemes/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewPromotionalScheme' | abpLocalization }}
          </button>
        </div>
      } @else if (!isLoading) {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'Title' | abpLocalization }}</th>
                  <th>{{ 'ApplyOn' | abpLocalization }}</th>
                  <th>{{ 'Selling' | abpLocalization }} / {{ 'Buying' | abpLocalization }}</th>
                  <th class="text-end">{{ 'GeneratedRules' | abpLocalization }}</th>
                  <th>{{ 'ValidFrom' | abpLocalization }}</th>
                  <th>{{ 'ValidUpto' | abpLocalization }}</th>
                  <th>{{ 'IsDisabled' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (s of schemes; track s.id) {
                  <tr>
                    <td>{{ s.title }}</td>
                    <td>{{ getApplyOnLabel(s.applyOn) }}</td>
                    <td>{{ s.selling ? 'Selling' : '' }} {{ s.buying ? 'Buying' : '' }}</td>
                    <td class="text-end">{{ s.generatedRuleCount ?? 0 }}</td>
                    <td>{{ s.validFrom ? (s.validFrom | date:'dd/MM/yyyy') : '—' }}</td>
                    <td>{{ s.validUpto ? (s.validUpto | date:'dd/MM/yyyy') : '—' }}</td>
                    <td>@if (s.isDisabled) { <span class="badge bg-secondary">Disabled</span> }</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/sales/promotional-schemes', s.id]">
                          <i class="fa fa-pen"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="remove(s)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <app-pagination
        [totalCount]="totalCount"
        [pageSize]="pageSize"
        [currentPage]="currentPage"
        (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class PromotionalSchemeListComponent implements OnInit {
  private service = inject(PromotionalSchemeService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  schemes: PromotionalSchemeDto[] = [];
  totalCount = 0;
  isLoading = false;

  currentPage = 0;
  pageSize = 20;

  private applyOnLabels = pricingRuleApplyOnOptions.reduce((acc, o) => ({ ...acc, [o.value as number]: o.key }), {} as Record<number, string>);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: (result) => {
        this.schemes = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  getApplyOnLabel(applyOn: number | undefined): string {
    return this.applyOnLabels[applyOn ?? 0] ?? '—';
  }

  remove(s: PromotionalSchemeDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(s.id!).subscribe(() => {
        this.toaster.success('::SuccessfullyDeleted');
        this.load();
      });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}
