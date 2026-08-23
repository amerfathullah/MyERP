import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { OpportunityService } from '../../proxy/crm/opportunity.service';
import { CompetitorService } from '../../proxy/crm/competitor.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import type { CompetitorDto, OpportunityDto } from '../../proxy/crm/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-opportunity-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  template: `
    <abp-page [title]="opp()?.title ?? ('Opportunity' | abpLocalization)">
  <app-breadcrumb />
      @if (isLoading()) { <app-loading-overlay /> }
      @if (opp(); as o) {
        <div class="d-flex justify-content-end gap-2 mb-3">
          @if (o.status === 0 || o.status === 1) {
            <a class="btn btn-outline-primary btn-sm" [routerLink]="'/crm/opportunities/' + o.id + '/edit'">
              <i class="fa fa-edit me-1"></i>{{ 'Edit' | abpLocalization }}
            </a>
            <button class="btn btn-primary btn-sm" [disabled]="isConverting()" (click)="convertToQuotation()">
              <i class="fa fa-file-invoice me-1"></i>{{ '::ConvertToQuotation' | abpLocalization }}
            </button>
          }
          @if (o.status === 0 || o.status === 1 || o.status === 2) {
            <button class="btn btn-success btn-sm" [disabled]="isUpdatingStatus()" (click)="markWon()">
              <i class="fa fa-trophy me-1"></i>{{ '::MarkWon' | abpLocalization }}
            </button>
            <button class="btn btn-outline-danger btn-sm" [disabled]="isUpdatingStatus()" (click)="showLostReasonPrompt.set(true)">
              <i class="fa fa-times-circle me-1"></i>{{ '::MarkLost' | abpLocalization }}
            </button>
            <button class="btn btn-outline-secondary btn-sm" [disabled]="isUpdatingStatus()" (click)="closeOpportunity()">
              <i class="fa fa-box-archive me-1"></i>{{ '::Close' | abpLocalization }}
            </button>
          }
          @if (o.status === 4 || o.status === 5) {
            <button class="btn btn-outline-primary btn-sm" [disabled]="isUpdatingStatus()" (click)="reopen()">
              <i class="fa fa-rotate-left me-1"></i>{{ '::Reopen' | abpLocalization }}
            </button>
          }
        </div>

        @if (showLostReasonPrompt()) {
          <div class="card mb-3 border-danger"><div class="card-body">
            <h6>{{ '::LostReason' | abpLocalization }}</h6>
            <textarea class="form-control mb-2" rows="2" #lostReasonInput
              [placeholder]="'::LostReason' | abpLocalization"></textarea>
            <button class="btn btn-danger btn-sm me-2" [disabled]="isUpdatingStatus()" (click)="markLost(lostReasonInput.value)">
              {{ 'Confirm' | abpLocalization }}
            </button>
            <button class="btn btn-outline-secondary btn-sm" (click)="showLostReasonPrompt.set(false)">
              {{ 'Cancel' | abpLocalization }}
            </button>
          </div></div>
        }
        <div class="card mb-3">
          <div class="card-body">
            <div class="row">
              <div class="col-md-4 mb-3">
                <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
                <app-status-badge [status]="getStatus(o.status)" />
              </div>
              <div class="col-md-4 mb-3">
                <small class="text-muted d-block">{{ '::SalesStage' | abpLocalization }}</small>
                <span class="fw-bold">{{ o.salesStage }}</span>
              </div>
              <div class="col-md-4 mb-3">
                <small class="text-muted d-block">{{ '::Amount' | abpLocalization }}</small>
                <span class="fw-bold">{{ o.opportunityAmount | number:'1.2-2' }} {{ o.currencyCode }}</span>
              </div>
            </div>
            @if (o.probability) {
              <div class="row">
                <div class="col-md-4">
                  <small class="text-muted d-block">{{ '::Probability' | abpLocalization }}</small>
                  <div class="d-flex align-items-center gap-2">
                    <div class="progress flex-grow-1" style="height: 8px;">
                      <div class="progress-bar" [style.width.%]="o.probability"></div>
                    </div>
                    <small>{{ o.probability }}%</small>
                  </div>
                </div>
              </div>
            }
          </div>
        </div>
        @if (o.items && o.items.length > 0) {
          <div class="card">
            <div class="card-header fw-bold">{{ '::Items' | abpLocalization }}</div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Qty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of o.items; track item.id) {
                    <tr>
                      <td>{{ item.description }}</td>
                      <td class="text-end">{{ item.quantity }}</td>
                      <td class="text-end">{{ item.unitPrice | number:'1.2-2' }}</td>
                      <td class="text-end">{{ item.amount | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- Competitors -->
        <div class="card mt-3">
          <div class="card-header fw-bold">{{ '::Competitors' | abpLocalization }}</div>
          <div class="card-body">
            @if (o.competitors && o.competitors.length > 0) {
              <ul class="list-group mb-3">
                @for (c of o.competitors; track c.id) {
                  <li class="list-group-item d-flex justify-content-between align-items-center">
                    {{ competitorName(c.competitorId) }}
                    <button class="btn btn-sm btn-outline-danger" (click)="removeCompetitor(c.id)">
                      <i class="fa fa-trash"></i>
                    </button>
                  </li>
                }
              </ul>
            } @else {
              <p class="text-muted mb-3">{{ '::NoCompetitorsYet' | abpLocalization }}</p>
            }
            <div class="d-flex gap-2">
              <select class="form-select form-select-sm" style="max-width: 320px;" [(ngModel)]="selectedCompetitorId">
                <option value="">{{ '::SelectCompetitor' | abpLocalization }}</option>
                @for (comp of competitors(); track comp.id) {
                  <option [value]="comp.id">{{ comp.name }}</option>
                }
              </select>
              <button class="btn btn-sm btn-outline-primary" (click)="addCompetitor()" [disabled]="!selectedCompetitorId">
                <i class="fa fa-plus me-1"></i>{{ '::Add' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class OpportunityDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(OpportunityService);
  private competitorService = inject(CompetitorService);
  private conversionService = inject(DocumentConversionService);
  private toaster = inject(ToasterService);

  opp = signal<OpportunityDto | null>(null);
  isLoading = signal(false);
  isConverting = signal(false);
  isUpdatingStatus = signal(false);
  showLostReasonPrompt = signal(false);
  competitors = signal<CompetitorDto[]>([]);
  selectedCompetitorId = '';

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading.set(true);
      this.service.get(id).subscribe({
        next: o => { this.opp.set(o); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
    }
    this.competitorService.getList({ maxResultCount: 1000, skipCount: 0 }).subscribe({
      next: (res: any) => this.competitors.set(res?.items ?? []),
      error: () => {}
    });
  }

  getStatus(s: number | undefined): string {
    return ['Open', 'Replied', 'Quotation', 'Converted', 'Lost', 'Closed'][s ?? 0] ?? 'Open';
  }

  competitorName(competitorId: string | undefined): string {
    return this.competitors().find(c => c.id === competitorId)?.name ?? competitorId ?? '';
  }

  addCompetitor(): void {
    const opportunityId = this.opp()?.id;
    if (!opportunityId || !this.selectedCompetitorId) return;

    this.service.createCompetitor({
      parentType: 'Opportunity',
      parentId: opportunityId,
      competitorId: this.selectedCompetitorId,
    }).subscribe({
      next: () => {
        this.selectedCompetitorId = '';
        this.ngOnInit();
      },
      error: () => {}
    });
  }

  removeCompetitor(competitorDetailId: string | undefined): void {
    if (!competitorDetailId) return;
    this.service.deleteCompetitor(competitorDetailId).subscribe({
      next: () => this.ngOnInit(),
      error: () => {}
    });
  }

  convertToQuotation(): void {
    const opportunityId = this.opp()?.id;
    if (!opportunityId || this.isConverting()) return;

    this.isConverting.set(true);
    this.conversionService.convertOpportunityToQuotation(opportunityId).subscribe({
      next: (quotation) => this.router.navigate(['/sales/quotations', quotation.id]),
      error: () => this.isConverting.set(false),
    });
  }

  markWon(): void {
    const id = this.opp()?.id;
    if (!id) return;
    this.isUpdatingStatus.set(true);
    this.service.convert(id).subscribe({
      next: (o) => { this.opp.set(o); this.isUpdatingStatus.set(false); this.toaster.success('::SuccessfullyUpdated'); },
      error: (err) => { this.isUpdatingStatus.set(false); this.toaster.error(err?.error?.error?.message ?? '::OperationFailed'); },
    });
  }

  markLost(reason: string): void {
    const id = this.opp()?.id;
    if (!id) return;
    this.isUpdatingStatus.set(true);
    this.service.declareLost(id, reason).subscribe({
      next: (o) => { this.opp.set(o); this.isUpdatingStatus.set(false); this.showLostReasonPrompt.set(false); this.toaster.success('::SuccessfullyUpdated'); },
      error: (err) => { this.isUpdatingStatus.set(false); this.toaster.error(err?.error?.error?.data?.reason ?? err?.error?.error?.message ?? '::OperationFailed'); },
    });
  }

  closeOpportunity(): void {
    const id = this.opp()?.id;
    if (!id) return;
    this.isUpdatingStatus.set(true);
    this.service.close(id).subscribe({
      next: (o) => { this.opp.set(o); this.isUpdatingStatus.set(false); this.toaster.success('::SuccessfullyUpdated'); },
      error: (err) => { this.isUpdatingStatus.set(false); this.toaster.error(err?.error?.error?.message ?? '::OperationFailed'); },
    });
  }

  reopen(): void {
    const id = this.opp()?.id;
    if (!id) return;
    this.isUpdatingStatus.set(true);
    this.service.reopen(id).subscribe({
      next: (o) => { this.opp.set(o); this.isUpdatingStatus.set(false); this.toaster.success('::SuccessfullyUpdated'); },
      error: (err) => { this.isUpdatingStatus.set(false); this.toaster.error(err?.error?.error?.message ?? '::OperationFailed'); },
    });
  }
}
