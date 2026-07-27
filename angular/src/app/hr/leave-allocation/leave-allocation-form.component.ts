import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { LeaveAllocationService } from '../../proxy/human-resources/leave-allocation.service';
import { LeaveService } from '../../proxy/human-resources/leave.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-leave-allocation-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0"><i class="bi bi-calendar-plus me-2"></i>{{ 'MyERP::NewLeaveAllocation' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::Employee' | abpLocalization }} *</label>
              <select class="form-select" formControlName="employeeId">
                <option value="">-- Select Employee --</option>
                @for (emp of employees(); track emp.id) {
                  <option [value]="emp.id">{{ emp.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::LeaveType' | abpLocalization }} *</label>
              <select class="form-select" formControlName="leaveTypeId">
                <option value="">-- Select Leave Type --</option>
                @for (lt of leaveTypes(); track lt.id) {
                  <option [value]="lt.id">{{ lt.name }}</option>
                }
              </select>
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::FromDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="fromDate" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::ToDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="toDate" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::TotalLeaves' | abpLocalization }} *</label>
              <input type="number" class="form-control" formControlName="totalLeavesAllocated" min="0" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::CarryForwardDays' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="carryForwardDays" min="0" />
              <small class="text-muted">Days carried from previous period</small>
            </div>
          </div>

          <div class="d-flex justify-content-end gap-2 mt-4">
            <a routerLink=".." class="btn btn-secondary">{{ 'MyERP::Cancel' | abpLocalization }}</a>
            <button type="submit" class="btn btn-primary" [disabled]="!form.valid || saving">
              {{ 'MyERP::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
})
export class LeaveAllocationFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(LeaveAllocationService);
  private leaveService = inject(LeaveService);
  private employeeService = inject(EmployeeService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  form!: FormGroup;
  saving = false;
  employees = signal<{ id: string; name: string }[]>([]);
  leaveTypes = signal<{ id: string; name: string }[]>([]);

  ngOnInit() {
    const year = new Date().getFullYear();
    this.form = this.fb.group({
      employeeId: ['', Validators.required],
      leaveTypeId: ['', Validators.required],
      fromDate: [`${year}-01-01`, Validators.required],
      toDate: [`${year}-12-31`, Validators.required],
      totalLeavesAllocated: [14, [Validators.required, Validators.min(0)]],
      carryForwardDays: [0],
    });

    this.employeeService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(res => {
      this.employees.set((res.items ?? []).map((e: any) => ({
        id: e.id,
        name: `${e.firstName ?? ''} ${e.lastName ?? ''}`.trim() || e.employeeId || e.id,
      })));
    });

    this.leaveService.getLeaveTypes().subscribe((types: any) => {
      this.leaveTypes.set((types ?? []).map((t: any) => ({ id: t.id, name: t.name ?? t.leaveName ?? t.id })));
    });
  }

  save() {
    if (!this.form.valid) return;
    this.saving = true;
    const payload = { ...this.form.value, companyId: this.companyContext.currentCompanyId() };
    this.service.create(payload).subscribe({
      next: () => {
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['/hr/leave-allocations']);
      },
      error: () => { this.saving = false; },
    });
  }
}
