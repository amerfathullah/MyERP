import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import { RouterModule } from '@angular/router';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface WorkstationUtil {
  workstationId: string;
  workstationName: string;
  workstationType?: string;
  productionCapacity: number;
  activeJobCards: number;
  utilizationPercent: number;
  totalPlannedMinutes: number;
  totalActualMinutes: number;
  status: string;
  activeJobs: ActiveJob[];
}

interface ActiveJob {
  jobCardId: string;
  operationName?: string;
  forQuantity: number;
  completedQty: number;
  plannedTimeInMins: number;
  actualTimeInMins: number;
  status: number;
}

@Component({
  selector: 'app-workstation-capacity',
  standalone: true,
  imports: [CommonModule, LocalizationPipe, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">
          <i class="fas fa-industry me-2"></i>{{ '::WorkstationCapacity' | abpLocalization }}
        </h5>
        <button class="btn btn-sm btn-outline-primary" (click)="loadData()" [disabled]="isLoading()">
          <i class="fas fa-sync-alt me-1" [class.fa-spin]="isLoading()"></i>{{ '::Refresh' | abpLocalization }}
        </button>
      </div>
      <div class="card-body">
        @if (isLoading()) {
          <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
        } @else {
          <!-- KPI Summary -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="card text-center border-primary h-100"><div class="card-body py-3">
                <div class="text-muted small">{{ '::TotalWorkstations' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-primary">{{ workstations().length }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-success h-100"><div class="card-body py-3">
                <div class="text-muted small">{{ '::ActiveWorkstations' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-success">{{ activeCount() }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-danger h-100"><div class="card-body py-3">
                <div class="text-muted small">{{ '::FullCapacity' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-danger">{{ fullCount() }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-secondary h-100"><div class="card-body py-3">
                <div class="text-muted small">{{ '::IdleWorkstations' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-muted">{{ idleCount() }}</div>
              </div></div>
            </div>
          </div>

          <!-- Utilization Grid -->
          @if (workstations().length === 0) {
            <div class="text-center text-muted py-4">
              <i class="fas fa-industry fa-3x mb-3 text-secondary"></i>
              <p>{{ '::NoWorkstationsConfigured' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="row g-3">
              @for (ws of workstations(); track ws.workstationId) {
                <div class="col-md-6 col-lg-4">
                  <div class="card h-100" [class.border-success]="ws.status === 'Active'"
                       [class.border-danger]="ws.status === 'Full'"
                       [class.border-secondary]="ws.status === 'Idle'">
                    <div class="card-header d-flex justify-content-between align-items-center py-2">
                      <strong>{{ ws.workstationName }}</strong>
                      <span class="badge" [class.bg-success]="ws.status === 'Active'"
                            [class.bg-danger]="ws.status === 'Full'"
                            [class.bg-secondary]="ws.status === 'Idle'">
                        {{ ws.status }}
                      </span>
                    </div>
                    <div class="card-body py-2">
                      @if (ws.workstationType) {
                        <div class="text-muted small mb-1">{{ ws.workstationType }}</div>
                      }
                      <!-- Utilization bar -->
                      <div class="mb-2">
                        <div class="d-flex justify-content-between small mb-1">
                          <span>{{ '::Utilization' | abpLocalization }}</span>
                          <span class="fw-bold">{{ ws.utilizationPercent }}%</span>
                        </div>
                        <div class="progress" style="height: 8px;">
                          <div class="progress-bar" role="progressbar"
                               [style.width.%]="ws.utilizationPercent"
                               [class.bg-success]="ws.utilizationPercent < 70"
                               [class.bg-warning]="ws.utilizationPercent >= 70 && ws.utilizationPercent < 100"
                               [class.bg-danger]="ws.utilizationPercent >= 100">
                          </div>
                        </div>
                      </div>
                      <!-- Capacity info -->
                      <div class="d-flex justify-content-between small text-muted">
                        <span>{{ ws.activeJobCards }} / {{ ws.productionCapacity }} {{ '::Slots' | abpLocalization }}</span>
                        <span>{{ ws.totalActualMinutes | number:'1.0-0' }} min</span>
                      </div>
                      <!-- Active jobs list -->
                      @if (ws.activeJobs.length > 0) {
                        <hr class="my-2">
                        <div class="small">
                          @for (job of ws.activeJobs; track job.jobCardId) {
                            <div class="d-flex justify-content-between align-items-center mb-1">
                              <a [routerLink]="['/manufacturing/job-cards', job.jobCardId]"
                                 class="text-decoration-none text-truncate" style="max-width: 60%;">
                                <i class="fas fa-cog fa-sm me-1 text-muted"></i>
                                {{ job.operationName || 'JC' }}
                              </a>
                              <span class="badge bg-light text-dark">
                                {{ job.completedQty }}/{{ job.forQuantity }}
                              </span>
                            </div>
                          }
                        </div>
                      }
                    </div>
                  </div>
                </div>
              }
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class WorkstationCapacityComponent implements OnInit {
  private service = inject(ManufacturingService);
  private companyContext = inject(CompanyContextService);

  workstations = signal<WorkstationUtil[]>([]);
  isLoading = signal(false);

  activeCount = signal(0);
  fullCount = signal(0);
  idleCount = signal(0);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    this.service.getCapacityUtilization(companyId).subscribe({
      next: (data: any[]) => {
        this.workstations.set(data);
        this.activeCount.set(data.filter(w => w.status === 'Active').length);
        this.fullCount.set(data.filter(w => w.status === 'Full').length);
        this.idleCount.set(data.filter(w => w.status === 'Idle').length);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }
}
