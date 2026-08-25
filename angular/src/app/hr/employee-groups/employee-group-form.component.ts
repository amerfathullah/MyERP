import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { EmployeeGroupService } from '../../proxy/human-resources/employee-group.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { CompanyDto } from '../../proxy/core/models';
import type { EmployeeDto } from '../../proxy/human-resources/models';

@Component({
  selector: 'app-employee-group-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Employee Group</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Company *</label>
              <select class="form-select" formControlName="companyId">
                <option value="">— Select Company —</option>
                @for (c of companies(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-6">
              <label class="form-label">Group Name *</label>
              <input type="text" class="form-control" formControlName="groupName" maxlength="140" placeholder="e.g. Operations Shift A, Management Team">
            </div>
          </div>

          <div class="mb-3">
            <div class="form-check form-switch">
              <input type="checkbox" class="form-check-input" formControlName="isDisabled" id="isDisabled">
              <label class="form-check-label" for="isDisabled">Disabled</label>
            </div>
          </div>

          <div class="card mb-3">
            <div class="card-header d-flex justify-content-between align-items-center bg-light">
              <span class="fw-semibold">Group Members</span>
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="addMember()">+ Add Member</button>
            </div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>Employee *</th>
                    <th>Designation</th>
                    <th style="width: 50px;"></th>
                  </tr>
                </thead>
                <tbody formArrayName="items">
                  @for (item of itemsArray.controls; track $index) {
                    <tr [formGroupName]="$index">
                      <td>
                        <select class="form-select form-select-sm" formControlName="employeeId" (change)="onEmployeeChange($index)">
                          <option value="">— Select Employee —</option>
                          @for (emp of employees(); track emp.id) {
                            <option [value]="emp.id">{{ emp.employeeId ? emp.employeeId + ' - ' : '' }}{{ emp.fullName }}</option>
                          }
                        </select>
                      </td>
                      <td>
                        <input type="text" class="form-control form-control-sm" formControlName="designation" placeholder="e.g. Technician">
                      </td>
                      <td class="text-center">
                        <button type="button" class="btn btn-sm btn-link text-danger" (click)="removeMember($index)">×</button>
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="3" class="text-center text-muted py-3">No members added yet.</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/hr/employee-groups" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class EmployeeGroupFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(EmployeeGroupService);
  private employeeService = inject(EmployeeService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  employees = signal<EmployeeDto[]>([]);
  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  get itemsArray(): FormArray {
    return this.form.get('items') as FormArray;
  }

  constructor() {
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      groupName: ['', [Validators.required, Validators.maxLength(140)]],
      isDisabled: [false],
      items: this.fb.array([]),
    });
  }

  ngOnInit() {
    this.companyService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
      this.companies.set(r.items ?? []);
    });

    this.employeeService.getList({ skipCount: 0, maxResultCount: 500 } as any).subscribe(r => {
      this.employees.set(r.items ?? []);
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue({
          companyId: res.companyId,
          groupName: res.groupName,
          isDisabled: res.isDisabled,
        });

        this.itemsArray.clear();
        if (res.items) {
          res.items.forEach(item => {
            this.itemsArray.push(this.fb.group({
              employeeId: [item.employeeId, Validators.required],
              employeeName: [item.employeeName, Validators.required],
              designation: [item.designation || ''],
            }));
          });
        }
      });
    } else {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }
  }

  addMember() {
    this.itemsArray.push(this.fb.group({
      employeeId: ['', Validators.required],
      employeeName: ['', Validators.required],
      designation: [''],
    }));
  }

  removeMember(index: number) {
    this.itemsArray.removeAt(index);
  }

  onEmployeeChange(index: number) {
    const row = this.itemsArray.at(index);
    const empId = row.get('employeeId')?.value;
    const emp = this.employees().find(e => e.id === empId);
    if (emp) {
      row.patchValue({
        employeeName: emp.fullName,
        designation: emp.designation || '',
      });
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/hr/employee-groups']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
