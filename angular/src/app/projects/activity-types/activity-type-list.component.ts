import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ActivityTypeService } from '../../proxy/projects/activity-type.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import type { ActivityCostDto, ActivityTypeDto } from '../../proxy/projects/models';

@Component({
  selector: 'app-activity-type-list',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'ActivityTypes' | abpLocalization">
      <div class="card mb-3"><div class="card-body">
        <div class="row g-2 align-items-end">
          <div class="col-md-4">
            <label class="form-label">{{ 'ActivityType' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="newName" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'DefaultBillingRate' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="newBillingRate" min="0" step="0.01" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'DefaultCostingRate' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="newCostingRate" min="0" step="0.01" />
          </div>
          <div class="col-md-2">
            <button class="btn btn-primary" (click)="add()" [disabled]="!newName"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>
          </div>
        </div>
      </div></div>

      @if (types.length === 0) {
        <div class="text-center py-5">
          <p class="text-muted">{{ 'NoActivityTypesYet' | abpLocalization }}</p>
        </div>
      }

      @for (t of types; track t.id) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <div class="d-flex align-items-center gap-3">
              <strong>{{ t.name }}</strong>
              @if (!t.isEnabled) {
                <span class="badge bg-secondary-subtle text-secondary">{{ 'Disabled' | abpLocalization }}</span>
              }
            </div>
            <button class="btn btn-sm btn-outline-secondary" (click)="toggleExpand(t)">
              <i class="fa" [class.fa-chevron-down]="expandedId !== t.id" [class.fa-chevron-up]="expandedId === t.id"></i>
            </button>
          </div>
          <div class="card-body">
            <div class="row g-2 align-items-end mb-2">
              <div class="col-md-3">
                <label class="form-label">{{ 'DefaultBillingRate' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" [(ngModel)]="t.defaultBillingRate" min="0" step="0.01" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'DefaultCostingRate' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" [(ngModel)]="t.defaultCostingRate" min="0" step="0.01" />
              </div>
              <div class="col-md-2 d-flex align-items-center">
                <div class="form-check">
                  <input type="checkbox" class="form-check-input" [id]="'enabled-' + t.id" [(ngModel)]="t.isEnabled" />
                  <label class="form-check-label" [for]="'enabled-' + t.id">{{ 'IsEnabled' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4 d-flex gap-2 justify-content-end">
                <button class="btn btn-sm btn-primary" (click)="save(t)"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
                <button class="btn btn-sm btn-outline-danger" (click)="remove(t)"><i class="fa fa-trash"></i></button>
              </div>
            </div>

            @if (expandedId === t.id) {
              <hr />
              <h6 class="mb-2">{{ 'EmployeeRateOverrides' | abpLocalization }}</h6>
              <table class="table table-sm mb-2">
                <thead><tr>
                  <th>{{ 'Employee' | abpLocalization }}</th>
                  <th>{{ 'BillingRate' | abpLocalization }}</th>
                  <th>{{ 'CostingRate' | abpLocalization }}</th>
                  <th></th>
                </tr></thead>
                <tbody>
                  @for (c of costs; track c.id) {
                    <tr>
                      <td>{{ employeeName(c.employeeId) }}</td>
                      <td>{{ c.billingRate | number:'1.2-2' }}</td>
                      <td>{{ c.costingRate | number:'1.2-2' }}</td>
                      <td><button class="btn btn-sm btn-outline-danger" (click)="removeCost(c, t)"><i class="fa fa-trash"></i></button></td>
                    </tr>
                  }
                </tbody>
              </table>
              <div class="row g-2 align-items-end">
                <div class="col-md-4">
                  <select class="form-select form-select-sm" [(ngModel)]="newCostEmployeeId">
                    <option value="">-- {{ 'Employee' | abpLocalization }} --</option>
                    @for (e of employees(); track e.id) { <option [value]="e.id">{{ e.fullName }}</option> }
                  </select>
                </div>
                <div class="col-md-3">
                  <input type="number" class="form-control form-control-sm" [(ngModel)]="newCostBillingRate" placeholder="{{ 'BillingRate' | abpLocalization }}" min="0" step="0.01" />
                </div>
                <div class="col-md-3">
                  <input type="number" class="form-control form-control-sm" [(ngModel)]="newCostCostingRate" placeholder="{{ 'CostingRate' | abpLocalization }}" min="0" step="0.01" />
                </div>
                <div class="col-md-2">
                  <button class="btn btn-sm btn-outline-primary" (click)="addCost(t)" [disabled]="!newCostEmployeeId">{{ 'AddItem' | abpLocalization }}</button>
                </div>
              </div>
            }
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class ActivityTypeListComponent implements OnInit {
  private service = inject(ActivityTypeService);
  private employeeService = inject(EmployeeService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  types: ActivityTypeDto[] = [];
  employees = signal<{ id: string; fullName: string }[]>([]);

  newName = '';
  newBillingRate = 0;
  newCostingRate = 0;

  expandedId: string | null = null;
  costs: ActivityCostDto[] = [];
  newCostEmployeeId = '';
  newCostBillingRate = 0;
  newCostCostingRate = 0;

  ngOnInit(): void {
    this.load();
    this.employeeService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.employees.set((r.items ?? []).map((e: any) => ({ id: e.id, fullName: e.fullName ?? e.firstName ?? '' }))));
  }

  load(): void {
    this.service.getList().subscribe(r => this.types = r ?? []);
  }

  employeeName(id: string | undefined): string {
    return this.employees().find(e => e.id === id)?.fullName ?? '—';
  }

  add(): void {
    this.service.create({ name: this.newName, defaultBillingRate: this.newBillingRate, defaultCostingRate: this.newCostingRate }).subscribe(() => {
      this.toaster.success('::SuccessfullySaved');
      this.newName = '';
      this.newBillingRate = 0;
      this.newCostingRate = 0;
      this.load();
    });
  }

  save(t: ActivityTypeDto): void {
    this.service.update(t.id!, { defaultBillingRate: t.defaultBillingRate!, defaultCostingRate: t.defaultCostingRate!, isEnabled: t.isEnabled! }).subscribe(() => {
      this.toaster.success('::SuccessfullyUpdated');
    });
  }

  remove(t: ActivityTypeDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(t.id!).subscribe(() => { this.toaster.success('::SuccessfullyDeleted'); this.load(); });
    });
  }

  toggleExpand(t: ActivityTypeDto): void {
    if (this.expandedId === t.id) {
      this.expandedId = null;
      this.costs = [];
      return;
    }
    this.expandedId = t.id!;
    this.loadCosts(t.id!);
  }

  private loadCosts(activityTypeId: string): void {
    this.service.getCostsForActivity(activityTypeId).subscribe(r => this.costs = r ?? []);
  }

  addCost(t: ActivityTypeDto): void {
    this.service.setEmployeeCost({
      employeeId: this.newCostEmployeeId,
      activityTypeId: t.id!,
      billingRate: this.newCostBillingRate,
      costingRate: this.newCostCostingRate,
    }).subscribe(() => {
      this.toaster.success('::SuccessfullySaved');
      this.newCostEmployeeId = '';
      this.newCostBillingRate = 0;
      this.newCostCostingRate = 0;
      this.loadCosts(t.id!);
    });
  }

  removeCost(c: ActivityCostDto, t: ActivityTypeDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.deleteCost(c.id!).subscribe(() => { this.toaster.success('::SuccessfullyDeleted'); this.loadCosts(t.id!); });
    });
  }
}
