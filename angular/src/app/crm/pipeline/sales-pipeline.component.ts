import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { RestService } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface PipelineStage {
  stageName: string;
  count: number;
  totalAmount: number;
  weightedAmount: number;
  avgProbability: number;
}

interface PipelineOpportunity {
  id: string;
  title: string;
  salesStage: string;
  amount: number;
  probability: number;
  weightedAmount: number;
  expectedClosingDate: string | null;
  contactName: string | null;
  daysOpen: number;
}

interface PipelineDashboard {
  totalLeads: number;
  activeLeads: number;
  qualifiedLeads: number;
  lostLeads: number;
  totalOpportunities: number;
  openOpportunities: number;
  openOpportunitiesAmount: number;
  weightedPipelineValue: number;
  wonOpportunities: number;
  wonAmount: number;
  lostOpportunities: number;
  stageBreakdown: PipelineStage[];
  totalQuotations: number;
  openQuotations: number;
  openQuotationsAmount: number;
  convertedQuotations: number;
  ordersThisMonth: number;
  ordersThisMonthAmount: number;
  leadToOpportunityRate: number;
  opportunityToQuotationRate: number;
  quotationToOrderRate: number;
}

@Component({
  selector: 'app-sales-pipeline',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h4 class="fw-semibold mb-0"><i class="fa fa-chart-line me-2 text-primary"></i>{{ '::SalesPipeline' | abpLocalization }}</h4>
      </div>

      @if (isLoading()) {
        <div class="d-flex justify-content-center py-5">
          <div class="spinner-border text-primary"></div>
        </div>
      } @else if (data()) {
        <!-- Conversion Funnel -->
        <div class="card mb-4">
          <div class="card-header fw-bold">
            <i class="fa fa-filter me-2"></i>{{ '::ConversionFunnel' | abpLocalization }}
          </div>
          <div class="card-body">
            <div class="row text-center">
              <div class="col">
                <div class="funnel-stage" style="background: linear-gradient(135deg, #e3f2fd, #bbdefb); border-radius: 8px; padding: 1rem;">
                  <div class="fs-2 fw-bold text-primary">{{ data()!.totalLeads }}</div>
                  <div class="text-muted small">{{ '::Leads' | abpLocalization }}</div>
                </div>
              </div>
              <div class="col d-flex align-items-center justify-content-center">
                <div class="text-center">
                  <i class="fa fa-chevron-right text-muted fs-4"></i>
                  <div class="small text-success fw-bold">{{ data()!.leadToOpportunityRate }}%</div>
                </div>
              </div>
              <div class="col">
                <div class="funnel-stage" style="background: linear-gradient(135deg, #e8f5e9, #c8e6c9); border-radius: 8px; padding: 1rem;">
                  <div class="fs-2 fw-bold text-success">{{ data()!.openOpportunities }}</div>
                  <div class="text-muted small">{{ '::Opportunities' | abpLocalization }}</div>
                </div>
              </div>
              <div class="col d-flex align-items-center justify-content-center">
                <div class="text-center">
                  <i class="fa fa-chevron-right text-muted fs-4"></i>
                  <div class="small text-success fw-bold">{{ data()!.opportunityToQuotationRate }}%</div>
                </div>
              </div>
              <div class="col">
                <div class="funnel-stage" style="background: linear-gradient(135deg, #fff3e0, #ffe0b2); border-radius: 8px; padding: 1rem;">
                  <div class="fs-2 fw-bold text-warning">{{ data()!.openQuotations }}</div>
                  <div class="text-muted small">{{ '::Quotations' | abpLocalization }}</div>
                </div>
              </div>
              <div class="col d-flex align-items-center justify-content-center">
                <div class="text-center">
                  <i class="fa fa-chevron-right text-muted fs-4"></i>
                  <div class="small text-success fw-bold">{{ data()!.quotationToOrderRate }}%</div>
                </div>
              </div>
              <div class="col">
                <div class="funnel-stage" style="background: linear-gradient(135deg, #e8eaf6, #c5cae9); border-radius: 8px; padding: 1rem;">
                  <div class="fs-2 fw-bold text-info">{{ data()!.ordersThisMonth }}</div>
                  <div class="text-muted small">{{ '::OrdersThisMonth' | abpLocalization }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Pipeline KPI Cards -->
        <div class="row mb-4">
          <div class="col-md-3">
            <div class="card border-start border-primary border-4">
              <div class="card-body text-center">
                <div class="fs-4 fw-bold text-primary">{{ data()!.openOpportunitiesAmount | number:'1.0-0' }}</div>
                <small class="text-muted">{{ '::OpenPipelineValue' | abpLocalization }}</small>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-success border-4">
              <div class="card-body text-center">
                <div class="fs-4 fw-bold text-success">{{ data()!.weightedPipelineValue | number:'1.0-0' }}</div>
                <small class="text-muted">{{ '::WeightedValue' | abpLocalization }}</small>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-info border-4">
              <div class="card-body text-center">
                <div class="fs-4 fw-bold text-info">{{ data()!.wonAmount | number:'1.0-0' }}</div>
                <small class="text-muted">{{ '::WonAmount' | abpLocalization }}</small>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-warning border-4">
              <div class="card-body text-center">
                <div class="fs-4 fw-bold text-warning">{{ data()!.ordersThisMonthAmount | number:'1.0-0' }}</div>
                <small class="text-muted">{{ '::OrdersThisMonth' | abpLocalization }}</small>
              </div>
            </div>
          </div>
        </div>

        <!-- Stage Breakdown + Top Opportunities -->
        <div class="row">
          <!-- Stage Breakdown -->
          <div class="col-md-5">
            <div class="card mb-4">
              <div class="card-header fw-bold">
                <i class="fa fa-layer-group me-2"></i>{{ '::ByStage' | abpLocalization }}
              </div>
              <div class="card-body p-0">
                @if (data()!.stageBreakdown.length) {
                  <table class="table table-sm table-hover mb-0">
                    <thead>
                      <tr>
                        <th>{{ '::Stage' | abpLocalization }}</th>
                        <th class="text-end">#</th>
                        <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                        <th class="text-end">{{ '::Weighted' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (stage of data()!.stageBreakdown; track stage.stageName) {
                        <tr>
                          <td>
                            <span class="badge bg-light text-dark border">{{ stage.stageName }}</span>
                            <small class="text-muted ms-1">({{ stage.avgProbability }}%)</small>
                          </td>
                          <td class="text-end">{{ stage.count }}</td>
                          <td class="text-end fw-medium">{{ stage.totalAmount | number:'1.0-0' }}</td>
                          <td class="text-end text-success">{{ stage.weightedAmount | number:'1.0-0' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                } @else {
                  <div class="text-center text-muted py-4">
                    <i class="fa fa-chart-pie fa-2x mb-2 opacity-50"></i>
                    <div>{{ '::NoActiveOpportunities' | abpLocalization }}</div>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Top Opportunities -->
          <div class="col-md-7">
            <div class="card mb-4">
              <div class="card-header fw-bold d-flex justify-content-between align-items-center">
                <span><i class="fa fa-trophy me-2"></i>{{ '::TopOpportunities' | abpLocalization }}</span>
                <a routerLink="/crm/opportunities" class="btn btn-sm btn-outline-primary">{{ '::ViewAll' | abpLocalization }}</a>
              </div>
              <div class="card-body p-0">
                @if (topOpportunities().length) {
                  <table class="table table-sm table-hover mb-0">
                    <thead>
                      <tr>
                        <th>{{ '::Title' | abpLocalization }}</th>
                        <th>{{ '::Stage' | abpLocalization }}</th>
                        <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                        <th class="text-end">{{ '::Probability' | abpLocalization }}</th>
                        <th>{{ '::ClosingDate' | abpLocalization }}</th>
                        <th class="text-end">{{ '::DaysOpen' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (opp of topOpportunities(); track opp.id) {
                        <tr>
                          <td>
                            <a [routerLink]="['/crm/opportunities', opp.id]" class="text-decoration-none fw-medium">
                              {{ opp.title }}
                            </a>
                            @if (opp.contactName) {
                              <br><small class="text-muted">{{ opp.contactName }}</small>
                            }
                          </td>
                          <td><span class="badge" [ngClass]="getStageBadgeClass(opp.salesStage)">{{ opp.salesStage }}</span></td>
                          <td class="text-end fw-medium">{{ opp.amount | number:'1.0-0' }}</td>
                          <td class="text-end">
                            <span [ngClass]="getProbabilityClass(opp.probability)">{{ opp.probability }}%</span>
                          </td>
                          <td>
                            @if (opp.expectedClosingDate) {
                              <span [ngClass]="isOverdue(opp.expectedClosingDate) ? 'text-danger' : ''">
                                {{ opp.expectedClosingDate | date:'dd/MM/yyyy' }}
                              </span>
                            } @else {
                              <span class="text-muted">—</span>
                            }
                          </td>
                          <td class="text-end">
                            <span [ngClass]="opp.daysOpen > 90 ? 'text-danger' : opp.daysOpen > 30 ? 'text-warning' : 'text-success'">
                              {{ opp.daysOpen }}d
                            </span>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                } @else {
                  <div class="text-center text-muted py-4">
                    <i class="fa fa-handshake fa-2x mb-2 opacity-50"></i>
                    <div>{{ '::NoActiveOpportunities' | abpLocalization }}</div>
                  </div>
                }
              </div>
            </div>
          </div>
        </div>

        <!-- Win/Loss Summary -->
        <div class="row mb-4">
          <div class="col-md-4">
            <div class="card bg-success bg-opacity-10">
              <div class="card-body text-center">
                <div class="fs-3 fw-bold text-success">{{ data()!.wonOpportunities }}</div>
                <div class="text-muted">{{ '::Won' | abpLocalization }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card bg-danger bg-opacity-10">
              <div class="card-body text-center">
                <div class="fs-3 fw-bold text-danger">{{ data()!.lostOpportunities }}</div>
                <div class="text-muted">{{ '::Lost' | abpLocalization }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card bg-info bg-opacity-10">
              <div class="card-body text-center">
                <div class="fs-3 fw-bold text-info">
                  {{ data()!.wonOpportunities + data()!.lostOpportunities > 0
                    ? (data()!.wonOpportunities / (data()!.wonOpportunities + data()!.lostOpportunities) * 100 | number:'1.0-0')
                    : 0 }}%
                </div>
                <div class="text-muted">{{ '::WinRate' | abpLocalization }}</div>
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .funnel-stage { transition: transform 0.2s; }
    .funnel-stage:hover { transform: scale(1.05); }
  `],
})
export class SalesPipelineComponent implements OnInit {
  private rest = inject(RestService);
  private companyContext = inject(CompanyContextService);

  data = signal<PipelineDashboard | null>(null);
  topOpportunities = signal<PipelineOpportunity[]>([]);
  isLoading = signal(false);

  ngOnInit(): void {
    this.loadPipeline();
  }

  private loadPipeline(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    const params: any = {};
    if (companyId) params.companyId = companyId;

    this.rest.request<void, PipelineDashboard>({ method: 'GET', url: '/api/app/sales-pipeline/pipeline-data', params })
      .subscribe({
        next: (result) => {
          this.data.set(result);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });

    this.rest.request<void, PipelineOpportunity[]>({ method: 'GET', url: '/api/app/sales-pipeline/top-opportunities', params: { ...params, maxCount: 10 } })
      .subscribe({
        next: (result) => this.topOpportunities.set(result ?? []),
        error: () => {},
      });
  }

  getStageBadgeClass(stage: string): string {
    switch (stage?.toLowerCase()) {
      case 'prospecting': return 'bg-secondary';
      case 'qualification': return 'bg-info';
      case 'proposal': return 'bg-primary';
      case 'negotiation': return 'bg-warning text-dark';
      default: return 'bg-light text-dark border';
    }
  }

  getProbabilityClass(prob: number): string {
    if (prob >= 75) return 'text-success fw-bold';
    if (prob >= 50) return 'text-info';
    if (prob >= 25) return 'text-warning';
    return 'text-muted';
  }

  isOverdue(dateStr: string): boolean {
    return new Date(dateStr) < new Date();
  }
}
