import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetMaintenanceTeamService } from '../../proxy/assets/asset-maintenance-team.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-asset-maintenance-team-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'Edit' : 'NewAssetMaintenanceTeam') | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'TeamName' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.teamName" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'MaintenanceManager' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.maintenanceManagerId">
              <option value="">--</option>
              @for (e of employees(); track e.id) { <option [value]="e.id">{{ e.fullName }}</option> }
            </select>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Members' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Employee' | abpLocalization }}</th><th>{{ 'Role' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (m of form.members; track $index) {
              <tr>
                <td>
                  <select class="form-select form-select-sm" [(ngModel)]="m.employeeId">
                    @for (e of employees(); track e.id) { <option [value]="e.id">{{ e.fullName }}</option> }
                  </select>
                </td>
                <td><input class="form-control form-control-sm" [(ngModel)]="m.maintenanceRole" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.members.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addMember()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/assets/maintenance-teams">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving() || !form.teamName"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class AssetMaintenanceTeamFormComponent implements OnInit {
  private service = inject(AssetMaintenanceTeamService);
  private employeeService = inject(EmployeeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  saving = signal(false);
  isEdit = signal(false);
  private teamId: string | null = null;

  employees = signal<{ id: string; fullName: string }[]>([]);

  form: { teamName: string; maintenanceManagerId: string; members: { employeeId: string; maintenanceRole: string }[] } = {
    teamName: '', maintenanceManagerId: '', members: [],
  };

  ngOnInit(): void {
    this.employeeService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.employees.set((r.items ?? []).map(e => ({ id: e.id!, fullName: e.fullName ?? e.firstName ?? '' }))));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.teamId = id;
      this.service.get(id).subscribe(t => {
        this.form = {
          teamName: t.teamName!, maintenanceManagerId: t.maintenanceManagerId ?? '',
          members: (t.members ?? []).map(m => ({ employeeId: m.employeeId!, maintenanceRole: m.maintenanceRole ?? '' })),
        };
      });
    }
  }

  addMember(): void {
    this.form.members.push({ employeeId: '', maintenanceRole: '' });
  }

  save(): void {
    this.saving.set(true);
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      teamName: this.form.teamName,
      maintenanceManagerId: this.form.maintenanceManagerId || null,
      members: this.form.members.filter(m => m.employeeId),
    };
    const req = this.teamId ? this.service.update(this.teamId, dto) : this.service.create(dto);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.toaster.success(this.teamId ? '::SuccessfullyUpdated' : '::SuccessfullySaved');
        this.router.navigate(['/assets/maintenance-teams']);
      },
      error: () => this.saving.set(false),
    });
  }
}
