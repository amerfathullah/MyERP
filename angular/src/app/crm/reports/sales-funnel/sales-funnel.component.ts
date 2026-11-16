import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesPipelineService } from '../../../proxy/crm/sales-pipeline.service';
import type { SalesFunnelDto, SalesFunnelStageItemDto } from '../../../proxy/crm/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export interface FunnelStageMetric {
  title: string;
  value: number;
  color: string;
  widthPercent: number;
  stepConversionPercent: number | null;
}

export function buildFunnelMetrics(stages: SalesFunnelStageItemDto[] | undefined): FunnelStageMetric[] {
  if (!stages || stages.length === 0) {
    return [];
  }
  const maxVal = Math.max(1, ...stages.map(s => s.value ?? 0));
  return stages.map((stage, idx) => {
    const value = stage.value ?? 0;
    const prevValue = idx > 0 ? (stages[idx - 1].value ?? 0) : 0;
    const stepConversionPercent = idx > 0 && prevValue > 0
      ? Math.round((value / prevValue) * 1000) / 10
      : null;
    return {
      title: stage.title ?? '',
      value,
      color: stage.color ?? '#0d6efd',
      widthPercent: Math.round((value / maxVal) * 1000) / 10,
      stepConversionPercent,
    };
  });
}

@Component({
  selector: 'app-sales-funnel',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-filter text-primary me-2"></i>
            {{ '::SalesFunnel' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SalesFunnelDescription' | abpLocalization }}
          </p>
        </div>
        @if (stageMetrics().length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fas fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2 ms-auto">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadReport()"
                [disabled]="loading()"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fas fa-sync me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- KPI Cards -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-danger-subtle text-danger py-2 px-3">
              <div class="small fw-bold">{{ '::ActiveLeads' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.activeLeads ?? 0 }}</div>
            </div>
          </div>
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-warning-subtle text-warning-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::Opportunities' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.opportunities ?? 0 }}</div>
            </div>
          </div>
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::Quotations' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.quotations ?? 0 }}</div>
            </div>
          </div>
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::Converted' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.converted ?? 0 }}</div>
            </div>
          </div>
          <div class="col-md">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::OverallConversionRate' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ overallConversionRate() | number:'1.1-1' }}%</div>
            </div>
          </div>
        </div>

        <!-- Visual Funnel Bars -->
        <div class="card border-0 shadow-sm mb-3">
          <div class="card-body">
            @for (stage of stageMetrics(); track stage.title) {
              <div class="mb-3">
                <div class="d-flex justify-content-between align-items-center mb-1">
                  <span class="fw-semibold">{{ stage.title }}</span>
                  <span class="font-monospace small">
                    <strong>{{ stage.value }}</strong>
                    @if (stage.stepConversionPercent !== null) {
                      <span class="text-muted ms-2">({{ stage.stepConversionPercent | number:'1.1-1' }}% from previous)</span>
                    }
                  </span>
                </div>
                <div class="progress" style="height: 22px;">
                  <div
                    class="progress-bar fw-semibold"
                    role="progressbar"
                    [style.width.%]="stage.widthPercent"
                    [style.background-color]="stage.color"
                  >
                    {{ stage.value }}
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Stage Table -->
        <div class="card border-0 shadow-sm">
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover table-sm mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Stage' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Count' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ConversionRate' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (stage of stageMetrics(); track stage.title) {
                    <tr>
                      <td class="fw-medium">{{ stage.title }}</td>
                      <td class="text-end font-monospace">{{ stage.value }}</td>
                      <td class="text-end font-monospace">
                        {{ stage.stepConversionPercent !== null ? (stage.stepConversionPercent | number:'1.1-1') + '%' : '-' }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class SalesFunnelComponent implements OnInit {
  private pipelineService = inject(SalesPipelineService);
  private companyContext = inject(CompanyContextService);

  fromDate = new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];

  loading = signal(false);
  report = signal<SalesFunnelDto | null>(null);

  stageMetrics = computed(() => buildFunnelMetrics(this.report()?.stages));
  overallConversionRate = computed(() => {
    const r = this.report();
    const leads = r?.activeLeads ?? 0;
    const converted = r?.converted ?? 0;
    if (leads <= 0) return 0;
    return Math.round((converted / leads) * 1000) / 10;
  });

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.loading.set(true);
    this.pipelineService.getSalesFunnel({
      companyId,
      fromDate: this.fromDate || null,
      toDate: this.toDate || null,
    }).subscribe({
      next: res => {
        this.report.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  exportCsv(): void {
    const metrics = this.stageMetrics();
    if (metrics.length === 0) return;
    const rows = metrics.map(m => ({
      Stage: m.title,
      Count: m.value,
      'Step Conversion %': m.stepConversionPercent !== null ? m.stepConversionPercent.toFixed(1) : '',
    }));
    exportToCsv('sales-funnel.csv', rows, ['Stage', 'Count', 'Step Conversion %']);
  }
}
