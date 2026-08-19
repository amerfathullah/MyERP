import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ProjectService } from '../../proxy/projects/project.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import type { ProjectDto, ProjectTaskDto, ProjectUserDto } from '../../proxy/projects/models';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe, RouterLink, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    @if (loading()) {
      <div class="text-center p-5"><i class="fas fa-spinner fa-spin fa-2x"></i></div>
    } @else if (project()) {
      <div class="row g-3 mb-3">
        <div class="col-md-8">
          <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h5 class="mb-0">{{ project()!.projectName }}</h5>
              <div class="d-flex gap-2">
                @if (project()!.status === 0) {
                  <a [routerLink]="['/projects', project()!.id, 'edit']" class="btn btn-sm btn-outline-primary">
                    <i class="fas fa-edit me-1"></i>{{ '::Edit' | abpLocalization }}
                  </a>
                }
                @for (action of availableActions(); track action.key) {
                  <button class="btn btn-sm" [class]="action.color" (click)="onAction(action.key)">
                    <i [class]="action.icon + ' me-1'"></i>{{ action.label | abpLocalization }}
                  </button>
                }
              </div>
            </div>
            <div class="card-body">
              <div class="row mb-3">
                <div class="col-md-4"><strong>{{ '::ProjectNumber' | abpLocalization }}:</strong></div>
                <div class="col-md-8">{{ project()!.projectNumber || '—' }}</div>
              </div>
              <div class="row mb-3">
                <div class="col-md-4"><strong>{{ '::Status' | abpLocalization }}:</strong></div>
                <div class="col-md-8">
                  <span class="badge" [class]="statusBadge()">{{ statusLabel() }}</span>
                </div>
              </div>
              <div class="row mb-3">
                <div class="col-md-4"><strong>{{ '::Priority' | abpLocalization }}:</strong></div>
                <div class="col-md-8">{{ priorityLabel() }}</div>
              </div>
              @if (project()!.expectedStartDate) {
                <div class="row mb-3">
                  <div class="col-md-4"><strong>{{ '::StartDate' | abpLocalization }}:</strong></div>
                  <div class="col-md-8">{{ project()!.expectedStartDate | date:'dd/MM/yyyy' }}</div>
                </div>
              }
              @if (project()!.expectedEndDate) {
                <div class="row mb-3">
                  <div class="col-md-4"><strong>{{ '::EndDate' | abpLocalization }}:</strong></div>
                  <div class="col-md-8">{{ project()!.expectedEndDate | date:'dd/MM/yyyy' }}</div>
                </div>
              }
              @if ((project()!.estimatedCost ?? 0) > 0) {
                <div class="row mb-3">
                  <div class="col-md-4"><strong>{{ '::EstimatedCost' | abpLocalization }}:</strong></div>
                  <div class="col-md-8">{{ project()!.estimatedCost | number:'1.2-2' }}</div>
                </div>
              }
              @if (project()!.notes) {
                <div class="row mb-3">
                  <div class="col-md-4"><strong>{{ '::Notes' | abpLocalization }}:</strong></div>
                  <div class="col-md-8">{{ project()!.notes }}</div>
                </div>
              }

              <!-- Progress Bar -->
              <div class="mt-3">
                <label class="form-label fw-semibold">{{ '::Progress' | abpLocalization }} ({{ project()!.percentComplete | number:'1.0-0' }}%)</label>
                <div class="progress" style="height: 24px;">
                  <div class="progress-bar" [class]="progressColor()" role="progressbar"
                    [style.width.%]="project()!.percentComplete"
                    [attr.aria-valuenow]="project()!.percentComplete" aria-valuemin="0" aria-valuemax="100">
                    {{ project()!.percentComplete | number:'1.0-0' }}%
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- KPI Cards -->
        <div class="col-md-4">
          <div class="card mb-3">
            <div class="card-body text-center">
              <h6 class="text-muted">{{ '::EstimatedCost' | abpLocalization }}</h6>
              <h4>{{ project()!.estimatedCost | number:'1.2-2' }}</h4>
            </div>
          </div>
          <div class="card mb-3">
            <div class="card-body text-center">
              <h6 class="text-muted">{{ '::TotalBillingAmount' | abpLocalization }}</h6>
              <h4>{{ project()!.totalBillingAmount | number:'1.2-2' }}</h4>
            </div>
          </div>
          <div class="card mb-3">
            <div class="card-body text-center">
              <h6 class="text-muted">{{ '::Tasks' | abpLocalization }}</h6>
              <h4>{{ project()!.taskCount }}</h4>
            </div>
          </div>
          @if (project()!.grossMargin !== undefined && project()!.grossMargin !== 0) {
            <div class="card">
              <div class="card-body text-center">
                <h6 class="text-muted">{{ '::GrossProfit' | abpLocalization }}</h6>
                <h4 [class]="(project()!.grossMargin ?? 0) >= 0 ? 'text-success' : 'text-danger'">
                  {{ project()!.grossMargin | number:'1.2-2' }}%
                </h4>
              </div>
            </div>
          }
        </div>
      </div>

      <!-- Tasks Table -->
      @if (tasks().length > 0) {
        <div class="card mb-3">
          <div class="card-header">
            <h6 class="mb-0"><i class="fas fa-tasks me-2"></i>{{ '::Tasks' | abpLocalization }}</h6>
          </div>
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover mb-0">
                <thead>
                  <tr>
                    <th>{{ '::TaskNumber' | abpLocalization }}</th>
                    <th>{{ '::Subject' | abpLocalization }}</th>
                    <th>{{ '::Status' | abpLocalization }}</th>
                    <th>{{ '::Priority' | abpLocalization }}</th>
                    <th>{{ '::Progress' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (task of tasks(); track task.id) {
                    <tr>
                      <td>{{ task.taskNumber }}</td>
                      <td>{{ task.subject }}</td>
                      <td><span class="badge" [class]="getTaskStatusBadge(task.status ?? 0)">{{ getTaskStatusLabel(task.status ?? 0) }}</span></td>
                      <td>{{ getPriorityLabel(task.priority ?? 0) }}</td>
                      <td>
                        <div class="progress" style="height: 16px; min-width: 80px;">
                          <div class="progress-bar bg-info" [style.width.%]="task.progress">{{ task.progress | number:'1.0-0' }}%</div>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- Team Members -->
      <div class="card mb-3">
        <div class="card-header">
          <h6 class="mb-0"><i class="fas fa-users me-2"></i>{{ '::TeamMembers' | abpLocalization }}</h6>
        </div>
        <div class="card-body">
          @if (project()!.users!.length > 0) {
            <div class="table-responsive mb-3">
              <table class="table table-sm table-hover mb-0">
                <thead>
                  <tr>
                    <th>{{ '::User' | abpLocalization }}</th>
                    <th>{{ '::ViewAttachments' | abpLocalization }}</th>
                    <th>{{ '::HideTimesheets' | abpLocalization }}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (member of project()!.users!; track member.id) {
                    <tr>
                      <td>{{ member.userId }}</td>
                      <td>
                        <input type="checkbox" class="form-check-input"
                          [checked]="member.viewAttachments"
                          (change)="toggleMemberFlag(member, 'viewAttachments')" />
                      </td>
                      <td>
                        <input type="checkbox" class="form-check-input"
                          [checked]="member.hideTimesheets"
                          (change)="toggleMemberFlag(member, 'hideTimesheets')" />
                      </td>
                      <td>
                        <button class="btn btn-sm btn-outline-danger" (click)="removeMember(member)">
                          <i class="fas fa-trash"></i>
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <p class="text-muted mb-3">{{ '::NoTeamMembersYet' | abpLocalization }}</p>
          }

          <div class="d-flex gap-2">
            <input type="text" class="form-control form-control-sm" style="max-width: 320px;"
              [(ngModel)]="newMemberUserId" placeholder="{{ '::UserIdPlaceholder' | abpLocalization }}" />
            <button class="btn btn-sm btn-outline-primary" (click)="addMember()" [disabled]="!newMemberUserId">
              <i class="fas fa-plus me-1"></i>{{ '::AddMember' | abpLocalization }}
            </button>
          </div>
        </div>
      </div>

      <app-activity-log documentType="Project" [documentId]="project()!.id!" />
    }
  `,
  styles: [`
    .progress-bar { transition: width 0.3s; }
  `]
})
export class ProjectDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private router = inject(Router);
  private service = inject(ProjectService);

  project = signal<ProjectDto | null>(null);
  tasks = signal<ProjectTaskDto[]>([]);
  loading = signal(true);
  newMemberUserId = '';

  statusLabel = computed(() => {
    const s = this.project()?.status;
    const map: Record<number, string> = { 0: 'Open', 1: 'Completed', 2: 'Cancelled' };
    return map[s ?? 0] ?? 'Unknown';
  });

  statusBadge = computed(() => {
    const s = this.project()?.status;
    const map: Record<number, string> = { 0: 'bg-primary', 1: 'bg-success', 2: 'bg-secondary' };
    return map[s ?? 0] ?? 'bg-secondary';
  });

  progressColor = computed(() => {
    const pct = this.project()?.percentComplete ?? 0;
    if (pct >= 100) return 'bg-success';
    if (pct >= 50) return 'bg-info';
    if (pct >= 25) return 'bg-warning';
    return 'bg-danger';
  });

  availableActions = computed(() => {
    const s = this.project()?.status;
    const actions: { key: string; label: string; icon: string; color: string }[] = [];
    if (s === 0) {
      actions.push({ key: 'complete', label: '::Complete', icon: 'fas fa-check', color: 'btn-outline-success' });
      actions.push({ key: 'cancel', label: '::Cancel', icon: 'fas fa-times', color: 'btn-outline-danger' });
    }
    return actions;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.service.get(id).subscribe({
      next: (p) => {
        this.project.set(p);
        this.loading.set(false);
        this.loadTasks(id);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  private loadTasks(projectId: string): void {
    this.service.getTasks(projectId).subscribe({
      next: (res: any) => this.tasks.set(res?.items ?? res ?? []),
      error: () => {}
    });
  }

  onAction(action: string): void {
    const id = this.project()?.id;
    if (!id) return;

    if (action === 'complete') {
      this.service.complete(id).subscribe({
        next: () => this.ngOnInit(),
        error: () => {}
      });
    } else if (action === 'cancel') {
      this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
        if (status !== Confirmation.Status.confirm) return;
        this.service.cancel(id).subscribe({
          next: () => this.ngOnInit(),
          error: () => {}
        });
      });
    }
  }

  priorityLabel(): string {
    const map: Record<number, string> = { 0: 'Low', 1: 'Medium', 2: 'High', 3: 'Urgent' };
    return map[this.project()?.priority ?? 1] ?? 'Medium';
  }

  getTaskStatusLabel(status: number): string {
    const map: Record<number, string> = { 0: 'Open', 1: 'Working', 2: 'Pending Review', 3: 'Completed', 4: 'Cancelled', 5: 'Overdue' };
    return map[status] ?? 'Unknown';
  }

  getTaskStatusBadge(status: number): string {
    const map: Record<number, string> = { 0: 'bg-primary', 1: 'bg-info', 2: 'bg-warning', 3: 'bg-success', 4: 'bg-secondary', 5: 'bg-danger' };
    return map[status] ?? 'bg-secondary';
  }

  getPriorityLabel(priority: number): string {
    const map: Record<number, string> = { 0: 'Low', 1: 'Medium', 2: 'High', 3: 'Urgent' };
    return map[priority] ?? 'Medium';
  }

  addMember(): void {
    const projectId = this.project()?.id;
    const userId = this.newMemberUserId.trim();
    if (!projectId || !userId) return;

    this.service.createUser({ projectId, userId }).subscribe({
      next: () => {
        this.newMemberUserId = '';
        this.ngOnInit();
      },
      error: () => {}
    });
  }

  removeMember(member: ProjectUserDto): void {
    if (!member.id) return;
    this.confirmation.warn('::RemoveMemberConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.deleteUser(member.id!).subscribe({
        next: () => this.ngOnInit(),
        error: () => {}
      });
    });
  }

  toggleMemberFlag(member: ProjectUserDto, flag: 'viewAttachments' | 'hideTimesheets'): void {
    if (!member.id) return;
    const updated = { viewAttachments: !!member.viewAttachments, hideTimesheets: !!member.hideTimesheets };
    updated[flag] = !updated[flag];
    this.service.updateUser(member.id, updated).subscribe({
      next: () => this.ngOnInit(),
      error: () => {}
    });
  }
}
