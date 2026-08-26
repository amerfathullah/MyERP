import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { EmployeeDto } from '../../proxy/human-resources/models';
import { EmploymentStatus, employmentStatusOptions } from '../../proxy/human-resources/employment-status.enum';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  standalone: true,
  selector: 'app-employee-detail',
  imports: [CommonModule, FormsModule, LocalizationPipe, RouterLink, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />

    @if (loading()) {
      <div class="d-flex justify-content-center p-5">
        <div class="spinner-border text-primary" role="status"></div>
      </div>
    } @else if (employee()) {
      <div class="row mb-3">
        <div class="col">
          <h4 class="mb-0">{{ employee()!.fullName || (employee()!.firstName + ' ' + (employee()!.lastName || '')) }}</h4>
          <small class="text-muted">{{ employee()!.employeeId }}</small>
        </div>
        <div class="col-auto">
          <app-status-badge [status]="employee()!.status || 'Active'" />
          <a [routerLink]="['/hr/employees', employee()!.id, 'edit']" class="btn btn-sm btn-outline-primary ms-2">
            <i class="fas fa-edit"></i> {{ '::Edit' | abpLocalization }}
          </a>
        </div>
      </div>

      <!-- KPI Cards -->
      <div class="row mb-4 g-3">
        <div class="col-md-3">
          <div class="card text-center">
            <div class="card-body py-3">
              <div class="text-muted small">{{ '::Department' | abpLocalization }}</div>
              <div class="fw-bold">{{ employee()!.department || '—' }}</div>
            </div>
          </div>
        </div>
        <div class="col-md-3">
          <div class="card text-center">
            <div class="card-body py-3">
              <div class="text-muted small">{{ '::Designation' | abpLocalization }}</div>
              <div class="fw-bold">{{ employee()!.designation || '—' }}</div>
            </div>
          </div>
        </div>
        <div class="col-md-3">
          <div class="card text-center">
            <div class="card-body py-3">
              <div class="text-muted small">{{ '::DateOfJoining' | abpLocalization }}</div>
              <div class="fw-bold">{{ employee()!.dateOfJoining ? (employee()!.dateOfJoining | date:'dd/MM/yyyy') : '—' }}</div>
            </div>
          </div>
        </div>
        <div class="col-md-3">
          <div class="card text-center">
            <div class="card-body py-3">
              <div class="text-muted small">{{ '::DateOfBirth' | abpLocalization }}</div>
              <div class="fw-bold">{{ employee()!.dateOfBirth ? (employee()!.dateOfBirth | date:'dd/MM/yyyy') : '—' }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Personal + Contact Info -->
      <div class="row g-3 mb-4">
        <div class="col-md-6">
          <div class="card">
            <div class="card-header"><h6 class="mb-0">{{ '::PersonalInfo' | abpLocalization }}</h6></div>
            <div class="card-body">
              <dl class="row mb-0">
                <dt class="col-sm-5">{{ '::FirstName' | abpLocalization }}</dt>
                <dd class="col-sm-7">{{ employee()!.firstName }}</dd>
                <dt class="col-sm-5">{{ '::LastName' | abpLocalization }}</dt>
                <dd class="col-sm-7">{{ employee()!.lastName || '—' }}</dd>
                <dt class="col-sm-5">{{ '::Citizenship' | abpLocalization }}</dt>
                <dd class="col-sm-7">{{ employee()!.citizenship || '—' }}</dd>
                <dt class="col-sm-5">{{ '::Status' | abpLocalization }}</dt>
                <dd class="col-sm-7"><app-status-badge [status]="employee()!.status || 'Active'" /></dd>
              </dl>
              <div class="d-flex align-items-end gap-2 mt-3">
                <div class="flex-grow-1">
                  <label class="form-label small text-muted mb-1">{{ 'MyERP::ChangeStatus' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" [(ngModel)]="selectedStatus">
                    @for (opt of statusOptions; track opt.value) {
                      <option [ngValue]="opt.value">{{ opt.key }}</option>
                    }
                  </select>
                </div>
                @if (selectedStatus === resignedStatus) {
                  <div class="flex-grow-1">
                    <label class="form-label small text-muted mb-1">{{ '::DateOfResignation' | abpLocalization }}</label>
                    <input type="date" class="form-control form-control-sm" [(ngModel)]="resignationDate">
                  </div>
                }
                <button class="btn btn-sm btn-primary" (click)="changeStatus()">
                  {{ 'MyERP::Apply' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        </div>
        <div class="col-md-6">
          <div class="card">
            <div class="card-header"><h6 class="mb-0">{{ '::ContactInfo' | abpLocalization }}</h6></div>
            <div class="card-body">
              <dl class="row mb-0">
                <dt class="col-sm-5">{{ '::Email' | abpLocalization }}</dt>
                <dd class="col-sm-7">
                  @if (employee()!.email) {
                    <a [href]="'mailto:' + employee()!.email">{{ employee()!.email }}</a>
                  } @else { — }
                </dd>
                <dt class="col-sm-5">{{ '::Phone' | abpLocalization }}</dt>
                <dd class="col-sm-7">{{ employee()!.phone || '—' }}</dd>
              </dl>
            </div>
          </div>
        </div>
      </div>

      <!-- Employment Details -->
      <div class="card mb-4">
        <div class="card-header"><h6 class="mb-0">{{ '::EmploymentDetails' | abpLocalization }}</h6></div>
        <div class="card-body">
          <div class="row">
            <div class="col-md-4">
              <dl class="mb-0">
                <dt>{{ '::EmployeeId' | abpLocalization }}</dt>
                <dd>{{ employee()!.employeeId }}</dd>
              </dl>
            </div>
            <div class="col-md-4">
              <dl class="mb-0">
                <dt>{{ '::DateOfJoining' | abpLocalization }}</dt>
                <dd>{{ employee()!.dateOfJoining ? (employee()!.dateOfJoining | date:'dd/MM/yyyy') : '—' }}</dd>
              </dl>
            </div>
            <div class="col-md-4">
              <dl class="mb-0">
                <dt>{{ '::DateOfResignation' | abpLocalization }}</dt>
                <dd>
                  @if (employee()!.dateOfResignation) {
                    <span class="text-danger">{{ employee()!.dateOfResignation | date:'dd/MM/yyyy' }}</span>
                  } @else {
                    <span class="text-success">{{ '::Active' | abpLocalization }}</span>
                  }
                </dd>
              </dl>
            </div>
          </div>
        </div>
      </div>

      <!-- Activity Log -->
      <app-activity-log documentType="Employee" [documentId]="employee()!.id!" />
    }
  `,
})
export class EmployeeDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(EmployeeService);
  private toaster = inject(ToasterService);

  employee = signal<EmployeeDto | null>(null);
  loading = signal(true);

  statusOptions = employmentStatusOptions;
  resignedStatus = EmploymentStatus.Resigned;
  selectedStatus: EmploymentStatus = EmploymentStatus.Active;
  resignationDate: string | null = null;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/hr/employees']);
      return;
    }
    this.service.get(id).subscribe({
      next: (emp) => {
        this.employee.set(emp);
        this.loading.set(false);
        const matchedOption = this.statusOptions.find(o => o.key === emp.status);
        this.selectedStatus = matchedOption ? matchedOption.value : EmploymentStatus.Active;
        this.resignationDate = emp.dateOfResignation ? emp.dateOfResignation.substring(0, 10) : null;
      },
      error: () => {
        this.toaster.error('::RecordNotFound');
        this.router.navigate(['/hr/employees']);
      },
    });
  }

  changeStatus() {
    const emp = this.employee();
    if (!emp?.id) return;

    this.service.changeStatus(emp.id, {
      status: this.selectedStatus,
      dateOfResignation: this.selectedStatus === this.resignedStatus ? this.resignationDate : null,
    }).subscribe({
      next: (updated) => {
        this.employee.set(updated);
        this.toaster.success('MyERP::SuccessfullySaved');
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
