import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { LeaveAllocationService } from '../../proxy/human-resources/leave-allocation.service';
import { LeaveService } from '../../proxy/human-resources/leave.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';

interface LeaveBalanceRow {
  employeeId: string;
  employeeName: string;
  leaveTypeId: string;
  leaveTypeName: string;
  allocated: number;
  used: number;
  balance: number;
}

@Component({
  selector: 'app-leave-balance',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="bi bi-pie-chart me-2"></i>{{ 'MyERP::LeaveBalance' | abpLocalization }}</h5>
        <div class="d-flex gap-2">
          <select class="form-select form-select-sm" style="width: 200px;" [(ngModel)]="filterLeaveType" (change)="filterData()">
            <option value="">{{ 'MyERP::AllLeaveTypes' | abpLocalization }}</option>
            @for (lt of leaveTypes(); track lt.id) {
              <option [value]="lt.id">{{ lt.name }}</option>
            }
          </select>
          <input type="date" class="form-control form-control-sm" style="width: 160px;"
            [(ngModel)]="asOfDate" (change)="loadBalances()" />
        </div>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <!-- Summary cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="card bg-light">
                <div class="card-body text-center py-3">
                  <div class="fs-3 fw-bold text-primary">{{ totalEmployees() }}</div>
                  <div class="small text-muted">Employees</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card bg-light">
                <div class="card-body text-center py-3">
                  <div class="fs-3 fw-bold text-success">{{ totalAllocated() }}</div>
                  <div class="small text-muted">Total Allocated</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card bg-light">
                <div class="card-body text-center py-3">
                  <div class="fs-3 fw-bold text-warning">{{ totalUsed() }}</div>
                  <div class="small text-muted">Total Used</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card bg-light">
                <div class="card-body text-center py-3">
                  <div class="fs-3 fw-bold text-info">{{ totalRemaining() }}</div>
                  <div class="small text-muted">Total Remaining</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Balance table -->
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm">
              <thead class="table-light">
                <tr>
                  <th>{{ 'MyERP::Employee' | abpLocalization }}</th>
                  <th>{{ 'MyERP::LeaveType' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::Allocated' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::Used' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::Balance' | abpLocalization }}</th>
                  <th style="width: 150px;"></th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.employeeId + row.leaveTypeId) {
                  <tr>
                    <td class="fw-medium">{{ row.employeeName }}</td>
                    <td><span class="badge bg-info">{{ row.leaveTypeName }}</span></td>
                    <td class="text-end">{{ row.allocated }}</td>
                    <td class="text-end">{{ row.used }}</td>
                    <td class="text-end">
                      <span class="fw-bold" [class.text-danger]="row.balance <= 0"
                        [class.text-success]="row.balance > 0">
                        {{ row.balance }}
                      </span>
                    </td>
                    <td>
                      <div class="progress" style="height: 8px;">
                        <div class="progress-bar" [class.bg-success]="getUsagePercent(row) < 80"
                          [class.bg-warning]="getUsagePercent(row) >= 80 && getUsagePercent(row) < 100"
                          [class.bg-danger]="getUsagePercent(row) >= 100"
                          [style.width.%]="Math.min(100, getUsagePercent(row))"></div>
                      </div>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="text-center text-muted py-4">
                      No leave allocations found for this period
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class LeaveBalanceComponent implements OnInit {
  private allocationService = inject(LeaveAllocationService);
  private leaveService = inject(LeaveService);
  private employeeService = inject(EmployeeService);

  Math = Math;
  loading = signal(true);
  rows = signal<LeaveBalanceRow[]>([]);
  filteredRows = signal<LeaveBalanceRow[]>([]);
  leaveTypes = signal<{ id: string; name: string }[]>([]);

  filterLeaveType = '';
  asOfDate = new Date().toISOString().substring(0, 10);

  totalEmployees = signal(0);
  totalAllocated = signal(0);
  totalUsed = signal(0);
  totalRemaining = signal(0);

  ngOnInit() {
    this.leaveService.getLeaveTypes().subscribe((types: any) => {
      this.leaveTypes.set((types ?? []).map((t: any) => ({ id: t.id, name: t.name ?? t.leaveName ?? t.id })));
      this.loadBalances();
    });
  }

  loadBalances() {
    this.loading.set(true);
    // Load all allocations and compute balances
    this.allocationService.getList({ skipCount: 0, maxResultCount: 1000 } as any).subscribe({
      next: (result) => {
        const allocations = result.items ?? [];
        // Build balance rows from allocations
        const balanceRows: LeaveBalanceRow[] = allocations.map((a: any) => ({
          employeeId: a.employeeId,
          employeeName: a.employeeName || a.employeeId?.substring(0, 8) || '—',
          leaveTypeId: a.leaveTypeId,
          leaveTypeName: this.leaveTypes().find(lt => lt.id === a.leaveTypeId)?.name || '—',
          allocated: a.totalLeavesAllocated ?? 0,
          used: a.usedLeaves ?? 0,
          balance: (a.totalLeavesAllocated ?? 0) - (a.usedLeaves ?? 0),
        }));

        this.rows.set(balanceRows);
        this.filterData();
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  filterData() {
    let filtered = this.rows();
    if (this.filterLeaveType) {
      filtered = filtered.filter(r => r.leaveTypeId === this.filterLeaveType);
    }
    this.filteredRows.set(filtered);
    this.recalculateSummary(filtered);
  }

  private recalculateSummary(rows: LeaveBalanceRow[]) {
    const uniqueEmployees = new Set(rows.map(r => r.employeeId));
    this.totalEmployees.set(uniqueEmployees.size);
    this.totalAllocated.set(rows.reduce((s, r) => s + r.allocated, 0));
    this.totalUsed.set(rows.reduce((s, r) => s + r.used, 0));
    this.totalRemaining.set(rows.reduce((s, r) => s + r.balance, 0));
  }

  getUsagePercent(row: LeaveBalanceRow): number {
    if (row.allocated <= 0) return 0;
    return (row.used / row.allocated) * 100;
  }
}
