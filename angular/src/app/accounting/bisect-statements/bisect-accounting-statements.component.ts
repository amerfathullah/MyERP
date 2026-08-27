import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { BisectAccountingStatementsService } from '../../proxy/accounting/bisect-accounting-statements.service';
import { CompanyService } from '../../proxy/core/company.service';
import { BisectAlgorithm, type BisectAccountingStatementsDto, type BisectNodeDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-bisect-accounting-statements',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="card mb-4">
      <div class="card-header bg-light">
        <h5 class="card-title mb-0">Bisect Accounting Statements</h5>
        <small class="text-muted">Diagnose and locate P&L vs Balance Sheet statement discrepancies using binary search.</small>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="buildTree()">
          <div class="row g-3">
            <div class="col-md-3">
              <label class="form-label">Company</label>
              <select class="form-select form-select-sm" formControlName="companyId">
                <option value="" disabled>Select Company</option>
                @for (c of companies; track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>

            <div class="col-md-3">
              <label class="form-label">From Date</label>
              <input type="date" class="form-control form-control-sm" formControlName="fromDate">
            </div>

            <div class="col-md-3">
              <label class="form-label">To Date</label>
              <input type="date" class="form-control form-control-sm" formControlName="toDate">
            </div>

            <div class="col-md-2">
              <label class="form-label">Algorithm</label>
              <select class="form-select form-select-sm" formControlName="algorithm">
                <option [ngValue]="BisectAlgorithm.BFS">BFS (Breadth-First)</option>
                <option [ngValue]="BisectAlgorithm.DFS">DFS (Depth-First)</option>
              </select>
            </div>

            <div class="col-md-1 d-flex align-items-end">
              <button type="submit" class="btn btn-primary btn-sm w-100" [disabled]="form.invalid || loading">
                @if (loading) {
                  <i class="fa fa-spin fa-spinner"></i>
                } @else {
                  <i class="fa fa-sitemap me-1"></i>Run
                }
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>

    @if (currentDoc) {
      <div class="card mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h6 class="card-title mb-0">Current Sub-Period Diagnostic</h6>
          <div class="btn-group btn-group-sm">
            <button type="button" class="btn btn-outline-secondary" (click)="moveUp()" [disabled]="!hasParent">
              <i class="fa fa-arrow-up me-1"></i>Move Up (Parent)
            </button>
            <button type="button" class="btn btn-outline-primary" (click)="bisectLeft()" [disabled]="!hasLeft">
              <i class="fa fa-arrow-left me-1"></i>Bisect Left (Earlier)
            </button>
            <button type="button" class="btn btn-outline-primary" (click)="bisectRight()" [disabled]="!hasRight">
              Bisect Right (Later)<i class="fa fa-arrow-right ms-1"></i>
            </button>
          </div>
        </div>
        <div class="card-body">
          <div class="row text-center mb-4">
            <div class="col-md-3">
              <div class="p-3 border rounded bg-light">
                <div class="text-muted small">Current Period</div>
                <div class="fw-bold fs-6">
                  {{ currentDoc.currentFromDate | date:'yyyy-MM-dd' }} ~ {{ currentDoc.currentToDate | date:'yyyy-MM-dd' }}
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="p-3 border rounded bg-light">
                <div class="text-muted small">P&L Net Income</div>
                <div class="fw-bold fs-5 text-primary">{{ currentDoc.plSummary | number:'1.2-2' }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="p-3 border rounded bg-light">
                <div class="text-muted small">B/S Net Equity Change</div>
                <div class="fw-bold fs-5 text-info">{{ currentDoc.bsSummary | number:'1.2-2' }}</div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="p-3 border rounded" [ngClass]="currentDoc.difference > 0 ? 'bg-danger-subtle text-danger' : 'bg-success-subtle text-success'">
                <div class="text-muted small">Difference (|P&L - B/S|)</div>
                <div class="fw-bold fs-5">{{ currentDoc.difference | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>

          @if (currentDoc.difference > 0) {
            <div class="alert alert-warning d-flex align-items-center">
              <i class="fa fa-triangle-exclamation me-2 fs-4"></i>
              <div>
                <strong>Discrepancy Detected in this interval!</strong>
                Step Left or Right into sub-periods to isolate the date of the imbalanced transaction.
              </div>
            </div>
          } @else {
            <div class="alert alert-success d-flex align-items-center">
              <i class="fa fa-check-circle me-2 fs-4"></i>
              <div>
                <strong>Statements Balanced for this interval.</strong>
                P&L Net Income matches Balance Sheet Equity Change perfectly.
              </div>
            </div>
          }

          <h6 class="mt-4 mb-2">Interval Hierarchy & Nodes</h6>
          <div class="table-responsive">
            <table class="table table-hover table-bordered table-sm align-middle">
              <thead class="table-light">
                <tr>
                  <th>Period</th>
                  <th class="text-end">P&L Income</th>
                  <th class="text-end">B/S Equity</th>
                  <th class="text-end">Difference</th>
                  <th class="text-center">Status</th>
                </tr>
              </thead>
              <tbody>
                @for (n of currentDoc.nodes; track n.id) {
                  <tr [class.table-active]="n.id === currentDoc.currentNodeId">
                    <td>
                      @if (n.id === currentDoc.currentNodeId) {
                        <i class="fa fa-caret-right text-primary me-1"></i>
                      }
                      {{ n.periodFromDate | date:'yyyy-MM-dd' }} ~ {{ n.periodToDate | date:'yyyy-MM-dd' }}
                    </td>
                    <td class="text-end">{{ n.isGenerated ? (n.plSummary | number:'1.2-2') : '—' }}</td>
                    <td class="text-end">{{ n.isGenerated ? (n.bsSummary | number:'1.2-2') : '—' }}</td>
                    <td class="text-end fw-bold" [ngClass]="n.difference > 0 ? 'text-danger' : 'text-success'">
                      {{ n.isGenerated ? (n.difference | number:'1.2-2') : '—' }}
                    </td>
                    <td class="text-center">
                      @if (n.id === currentDoc.currentNodeId) {
                        <span class="badge bg-primary">Current</span>
                      } @else if (n.isGenerated) {
                        <span class="badge bg-secondary">Calculated</span>
                      } @else {
                        <span class="badge bg-light text-dark">Pending</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </div>
    }
  `
})
export class BisectAccountingStatementsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(BisectAccountingStatementsService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);

  BisectAlgorithm = BisectAlgorithm;
  companies: any[] = [];
  form: FormGroup;
  currentDoc: BisectAccountingStatementsDto | null = null;
  loading = false;

  get currentNode(): BisectNodeDto | undefined {
    return this.currentDoc?.nodes.find(n => n.id === this.currentDoc?.currentNodeId);
  }

  get hasLeft(): boolean {
    return !!this.currentNode?.leftChildId;
  }

  get hasRight(): boolean {
    return !!this.currentNode?.rightChildId;
  }

  get hasParent(): boolean {
    return !!this.currentNode?.parentNodeId;
  }

  constructor() {
    const today = new Date();
    const yearStart = new Date(today.getFullYear(), 0, 1);
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      fromDate: [yearStart.toISOString().split('T')[0], Validators.required],
      toDate: [today.toISOString().split('T')[0], Validators.required],
      algorithm: [BisectAlgorithm.BFS, Validators.required],
    });
  }

  ngOnInit() {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100 } as any).subscribe(res => {
      this.companies = res.items || [];
      if (this.companies.length > 0 && !this.form.get('companyId')?.value) {
        this.form.patchValue({ companyId: this.companies[0].id });
      }
    });
  }

  buildTree() {
    if (this.form.invalid) return;
    this.loading = true;
    this.service.createAndBuildTree(this.form.value).subscribe({
      next: (res) => {
        this.currentDoc = res;
        this.loading = false;
        this.toaster.success('Tree generated successfully.');
      },
      error: (err) => {
        this.loading = false;
        this.toaster.error(err?.error?.error?.message ?? 'Failed to build tree');
      }
    });
  }

  bisectLeft() {
    if (!this.currentDoc) return;
    this.service.bisectLeft(this.currentDoc.id).subscribe(res => {
      this.currentDoc = res;
    });
  }

  bisectRight() {
    if (!this.currentDoc) return;
    this.service.bisectRight(this.currentDoc.id).subscribe(res => {
      this.currentDoc = res;
    });
  }

  moveUp() {
    if (!this.currentDoc) return;
    this.service.moveUp(this.currentDoc.id).subscribe(res => {
      this.currentDoc = res;
    });
  }
}
