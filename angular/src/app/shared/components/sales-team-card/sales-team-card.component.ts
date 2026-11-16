import { ChangeDetectionStrategy, Component, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SalesPersonService } from '../../../proxy/sales/sales-person.service';
import type { SalesTeamAllocationInputDto, SalesTeamEntryDto } from '../../../proxy/sales/models';

/** Shared read/edit card for Sales Team commission allocations on SO, DN, and SI documents. */
@Component({
  selector: 'app-sales-team-card',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    @if ((salesTeam()?.length ?? 0) > 0 || (canEdit() && forceVisible())) {
      <div class="card mb-4 mt-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h6 class="card-title mb-0">
            <i class="fa fa-people-group me-2"></i>{{ '::SalesTeam' | abpLocalization }}
          </h6>
          @if (canEdit() && !editing()) {
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="openEdit()">
              <i class="fa fa-pencil-alt me-1"></i>{{ '::EditSalesTeam' | abpLocalization }}
            </button>
          }
        </div>
        <div class="card-body">
          @if (editing()) {
            <div class="table-responsive">
              <table class="table table-sm mb-2">
                <thead>
                  <tr>
                    <th>{{ '::SalesPerson' | abpLocalization }}</th>
                    <th style="width: 140px;">{{ '::AllocatedPercentage' | abpLocalization }}</th>
                    <th style="width: 140px;">{{ '::CommissionRate' | abpLocalization }}</th>
                    <th style="width: 48px;"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of draft(); track $index; let i = $index) {
                    <tr>
                      <td>
                        <select class="form-select form-select-sm" [(ngModel)]="row.salesPersonId">
                          <option value="">--</option>
                          @for (sp of salesPersons(); track sp.id) {
                            <option [value]="sp.id">{{ sp.salesPersonName }}</option>
                          }
                        </select>
                      </td>
                      <td>
                        <input type="number" class="form-control form-control-sm text-end" [(ngModel)]="row.allocatedPercentage" min="0" max="100" step="0.01" />
                      </td>
                      <td>
                        <input type="number" class="form-control form-control-sm text-end" [(ngModel)]="row.commissionRate" min="0" max="100" step="0.01" />
                      </td>
                      <td>
                        <button type="button" class="btn btn-sm btn-outline-danger" (click)="removeRow(i)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <div class="d-flex justify-content-between align-items-center">
              <div class="d-flex align-items-center gap-2">
                <button type="button" class="btn btn-sm btn-outline-secondary" (click)="addRow()">
                  <i class="fa fa-plus me-1"></i>{{ '::AddRow' | abpLocalization }}
                </button>
                <span class="small fw-bold" [class.text-danger]="draftTotal() !== 100 && draft().length > 0">
                  {{ draftTotal() }}%
                </span>
              </div>
              <div class="d-flex gap-2">
                <button type="button" class="btn btn-sm btn-outline-secondary" (click)="cancelEdit()" [disabled]="saving()">{{ '::Cancel' | abpLocalization }}</button>
                <button type="button" class="btn btn-sm btn-primary" (click)="save()" [disabled]="saving()">{{ '::Save' | abpLocalization }}</button>
              </div>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead>
                  <tr>
                    <th>{{ '::SalesPerson' | abpLocalization }}</th>
                    <th class="text-end" style="width: 100px;">{{ '::AllocatedPercentage' | abpLocalization }}</th>
                    <th class="text-end" style="width: 120px;">{{ '::Amount' | abpLocalization }}</th>
                    <th class="text-end" style="width: 100px;">{{ '::CommissionRate' | abpLocalization }}</th>
                    <th class="text-end" style="width: 120px;">{{ '::Incentives' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of salesTeam() ?? []; track row.salesPersonId ?? $index) {
                    <tr>
                      <td>{{ row.salesPersonName || row.salesPersonId }}</td>
                      <td class="text-end">{{ row.allocatedPercentage }}%</td>
                      <td class="text-end font-monospace">{{ row.allocatedAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ row.commissionRate }}%</td>
                      <td class="text-end font-monospace fw-semibold">{{ row.incentives | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                @if ((totalCommission() ?? 0) > 0) {
                  <tfoot>
                    <tr>
                      <td colspan="4" class="text-end fw-bold">{{ '::TotalCommission' | abpLocalization }}</td>
                      <td class="text-end font-monospace fw-bold">{{ totalCommission() | number:'1.2-2' }}</td>
                    </tr>
                  </tfoot>
                }
              </table>
            </div>
          }
        </div>
      </div>
    }
  `,
})
export class SalesTeamCardComponent {
  private salesPersonService = inject(SalesPersonService);
  private toaster = inject(ToasterService);

  salesTeam = input<SalesTeamEntryDto[] | undefined>([]);
  totalCommission = input<number | undefined>(0);
  canEdit = input<boolean>(false);
  saving = input<boolean>(false);
  forceVisible = input<boolean>(false);
  editing = model<boolean>(false);
  salesTeamSaved = output<SalesTeamAllocationInputDto[]>();

  salesPersons = signal<Array<{ id?: string; salesPersonName?: string }>>([]);
  draft = signal<Array<{ salesPersonId: string; allocatedPercentage: number; commissionRate: number | null }>>([]);

  openEdit(): void {
    const existing = (this.salesTeam() ?? []).map(r => ({
      salesPersonId: r.salesPersonId ?? '',
      allocatedPercentage: r.allocatedPercentage ?? 0,
      commissionRate: r.commissionRate ?? null,
    }));
    this.draft.set(existing.length > 0 ? existing : [{ salesPersonId: '', allocatedPercentage: 100, commissionRate: null }]);
    if (this.salesPersons().length === 0) {
      this.salesPersonService.getList({ maxResultCount: 200 }).subscribe({
        next: res => this.salesPersons.set(res.items ?? []),
        error: () => {},
      });
    }
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
  }

  addRow(): void {
    this.draft.update(rows => [...rows, { salesPersonId: '', allocatedPercentage: 0, commissionRate: null }]);
  }

  removeRow(index: number): void {
    this.draft.update(rows => rows.filter((_, i) => i !== index));
  }

  draftTotal(): number {
    return Math.round(this.draft().reduce((sum, r) => sum + (Number(r.allocatedPercentage) || 0), 0) * 100) / 100;
  }

  save(): void {
    const validRows: SalesTeamAllocationInputDto[] = this.draft()
      .filter(r => r.salesPersonId)
      .map(r => ({
        salesPersonId: r.salesPersonId,
        allocatedPercentage: Number(r.allocatedPercentage) || 0,
        commissionRate: r.commissionRate,
      }));
    if (validRows.length > 0 && Math.abs(this.draftTotal() - 100) > 0.01) {
      this.toaster.warn('::SalesTeamMustEqual100');
      return;
    }
    this.salesTeamSaved.emit(validRows);
  }
}
