import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { BomDto, BomDiffResultDto, BomRowChangedDto, BomFieldChangeDto, BomChildRowDiffDto } from '../../proxy/manufacturing/models';

interface FlattenedRowChange {
  rowIndex: number;
  identifier: string;
  fieldLabel: string;
  oldValue: string | null;
  newValue: string | null;
}

@Component({
  selector: 'app-bom-comparison-tool',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::BomComparisonTool' | abpLocalization">
      <div class="card mb-4">
        <div class="card-body">
          <p class="text-muted">{{ '::BomComparisonToolDescription' | abpLocalization }}</p>

          <form [formGroup]="form" (ngSubmit)="compare()">
            <div class="row align-items-center">
              <div class="col-12 col-md-5 mb-3">
                <label class="form-label font-weight-bold">
                  {{ '::BOM1' | abpLocalization }} <span class="text-danger">*</span>
                </label>
                <select class="form-select" formControlName="bomId1">
                  <option value="">{{ '::SelectBom' | abpLocalization }}</option>
                  @for (b of boms(); track b.id) {
                    <option [value]="b.id">{{ b.bomNumber }} — {{ b.itemName }}</option>
                  }
                </select>
              </div>

              <div class="col-12 col-md-2 mb-3 text-center d-none d-md-block pt-3">
                <button
                  type="button"
                  class="btn btn-outline-secondary btn-sm"
                  title="Swap BOMs"
                  (click)="swapBoms()"
                  [disabled]="isSubmitting() || !form.value.bomId1 || !form.value.bomId2"
                >
                  <i class="fa fa-arrow-right-arrow-left"></i>
                </button>
              </div>

              <div class="col-12 col-md-5 mb-3">
                <label class="form-label font-weight-bold">
                  {{ '::BOM2' | abpLocalization }} <span class="text-danger">*</span>
                </label>
                <select class="form-select" formControlName="bomId2">
                  <option value="">{{ '::SelectBom' | abpLocalization }}</option>
                  @for (b of boms(); track b.id) {
                    <option [value]="b.id">{{ b.bomNumber }} — {{ b.itemName }}</option>
                  }
                </select>
              </div>
            </div>

            @if (isSameBom()) {
              <div class="alert alert-warning py-2 mb-3">
                <i class="fa fa-triangle-exclamation me-2"></i>{{ '::MyERP:10047' | abpLocalization }}
              </div>
            }

            <div class="d-flex gap-2">
              <button
                type="submit"
                class="btn btn-primary"
                [disabled]="form.invalid || isSubmitting() || isSameBom()"
              >
                @if (isSubmitting()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                }
                <i class="fa fa-code-compare me-1"></i>{{ '::CompareBoms' | abpLocalization }}
              </button>

              <button
                type="button"
                class="btn btn-outline-secondary"
                (click)="resetForm()"
                [disabled]="isSubmitting()"
              >
                <i class="fa fa-rotate-left me-1"></i>{{ '::Reset' | abpLocalization }}
              </button>
            </div>
          </form>
        </div>
      </div>

      @if (diffResult(); as res) {
        @if (!hasDifferences(res)) {
          <div class="alert alert-info">
            <i class="fa fa-circle-info me-2"></i>{{ '::NoDifferencesFound' | abpLocalization }}
          </div>
        } @else {
          <!-- Summary chips -->
          <div class="d-flex flex-wrap gap-2 mb-4">
            <span class="badge bg-secondary p-2">
              <i class="fa fa-tag me-1"></i>{{ res.bomNumber1 }} vs {{ res.bomNumber2 }}
            </span>
            @if (res.headerChanges.length > 0) {
              <span class="badge bg-primary p-2">
                {{ res.headerChanges.length }} {{ '::HeaderChanges' | abpLocalization }}
              </span>
            }
            @if (getItemChanges(res.rowChanges).length > 0) {
              <span class="badge bg-info p-2">
                {{ getItemChanges(res.rowChanges).length }} {{ '::ItemChanges' | abpLocalization }}
              </span>
            }
            @if (getOperationChanges(res.rowChanges).length > 0) {
              <span class="badge bg-info p-2">
                {{ getOperationChanges(res.rowChanges).length }} {{ '::OperationChanges' | abpLocalization }}
              </span>
            }
            @if (getSecondaryItemChanges(res.rowChanges).length > 0) {
              <span class="badge bg-info p-2">
                {{ getSecondaryItemChanges(res.rowChanges).length }} {{ '::SecondaryItemChanges' | abpLocalization }}
              </span>
            }
            @if (res.addedRows.length > 0) {
              <span class="badge bg-success p-2">
                +{{ res.addedRows.length }} {{ '::AddedRows' | abpLocalization }}
              </span>
            }
            @if (res.removedRows.length > 0) {
              <span class="badge bg-danger p-2">
                -{{ res.removedRows.length }} {{ '::RemovedRows' | abpLocalization }}
              </span>
            }
          </div>

          <!-- 1. Header Changes -->
          @if (res.headerChanges.length > 0) {
            <div class="card mb-4 shadow-sm">
              <div class="card-header bg-light font-weight-bold">
                <i class="fa fa-sliders me-2 text-primary"></i>{{ '::HeaderChanges' | abpLocalization }}
              </div>
              <div class="table-responsive">
                <table class="table table-sm table-hover align-middle mb-0">
                  <thead class="table-light">
                    <tr>
                      <th style="width: 30%">{{ '::Field' | abpLocalization }}</th>
                      <th style="width: 35%">{{ res.bomNumber1 }}</th>
                      <th style="width: 35%">{{ res.bomNumber2 }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (ch of res.headerChanges; track ch.fieldName) {
                      <tr>
                        <td class="fw-semibold">{{ ch.fieldLabel || ch.fieldName }}</td>
                        <td class="text-danger">
                          <code>{{ ch.oldValue ?? '-' }}</code>
                        </td>
                        <td class="text-success">
                          <code>{{ ch.newValue ?? '-' }}</code>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- 2. Changes in Items -->
          @if (getItemChanges(res.rowChanges).length > 0) {
            <div class="card mb-4 shadow-sm">
              <div class="card-header bg-light font-weight-bold">
                <i class="fa fa-boxes-stacked me-2 text-info"></i>{{ '::ItemChanges' | abpLocalization }}
              </div>
              <div class="table-responsive">
                <table class="table table-sm table-hover align-middle mb-0">
                  <thead class="table-light">
                    <tr>
                      <th style="width: 10%">#</th>
                      <th style="width: 25%">{{ '::Item' | abpLocalization }}</th>
                      <th style="width: 20%">{{ '::Field' | abpLocalization }}</th>
                      <th style="width: 22%">{{ res.bomNumber1 }}</th>
                      <th style="width: 23%">{{ res.bomNumber2 }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (ch of getItemChanges(res.rowChanges); track ch.rowIndex + ch.fieldLabel) {
                      <tr>
                        <td>{{ ch.rowIndex + 1 }}</td>
                        <td class="fw-semibold">{{ ch.identifier }}</td>
                        <td>{{ ch.fieldLabel }}</td>
                        <td class="text-danger">
                          <code>{{ ch.oldValue ?? '-' }}</code>
                        </td>
                        <td class="text-success">
                          <code>{{ ch.newValue ?? '-' }}</code>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- 3. Changes in Operations -->
          @if (getOperationChanges(res.rowChanges).length > 0) {
            <div class="card mb-4 shadow-sm">
              <div class="card-header bg-light font-weight-bold">
                <i class="fa fa-gear me-2 text-warning"></i>{{ '::OperationChanges' | abpLocalization }}
              </div>
              <div class="table-responsive">
                <table class="table table-sm table-hover align-middle mb-0">
                  <thead class="table-light">
                    <tr>
                      <th style="width: 10%">#</th>
                      <th style="width: 25%">{{ '::Operation' | abpLocalization }}</th>
                      <th style="width: 20%">{{ '::Field' | abpLocalization }}</th>
                      <th style="width: 22%">{{ res.bomNumber1 }}</th>
                      <th style="width: 23%">{{ res.bomNumber2 }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (ch of getOperationChanges(res.rowChanges); track ch.rowIndex + ch.fieldLabel) {
                      <tr>
                        <td>{{ ch.rowIndex + 1 }}</td>
                        <td class="fw-semibold">{{ ch.identifier }}</td>
                        <td>{{ ch.fieldLabel }}</td>
                        <td class="text-danger">
                          <code>{{ ch.oldValue ?? '-' }}</code>
                        </td>
                        <td class="text-success">
                          <code>{{ ch.newValue ?? '-' }}</code>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- 4. Changes in Scrap / Secondary Items -->
          @if (getSecondaryItemChanges(res.rowChanges).length > 0) {
            <div class="card mb-4 shadow-sm">
              <div class="card-header bg-light font-weight-bold">
                <i class="fa fa-recycle me-2 text-secondary"></i>{{ '::SecondaryItemChanges' | abpLocalization }}
              </div>
              <div class="table-responsive">
                <table class="table table-sm table-hover align-middle mb-0">
                  <thead class="table-light">
                    <tr>
                      <th style="width: 10%">#</th>
                      <th style="width: 25%">{{ '::Item' | abpLocalization }}</th>
                      <th style="width: 20%">{{ '::Field' | abpLocalization }}</th>
                      <th style="width: 22%">{{ res.bomNumber1 }}</th>
                      <th style="width: 23%">{{ res.bomNumber2 }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (ch of getSecondaryItemChanges(res.rowChanges); track ch.rowIndex + ch.fieldLabel) {
                      <tr>
                        <td>{{ ch.rowIndex + 1 }}</td>
                        <td class="fw-semibold">{{ ch.identifier }}</td>
                        <td>{{ ch.fieldLabel }}</td>
                        <td class="text-danger">
                          <code>{{ ch.oldValue ?? '-' }}</code>
                        </td>
                        <td class="text-success">
                          <code>{{ ch.newValue ?? '-' }}</code>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- 5. Added Rows (Present in BOM 2, Missing in BOM 1) -->
          @if (res.addedRows.length > 0) {
            <div class="card mb-4 shadow-sm border-success">
              <div class="card-header bg-success-subtle text-success-emphasis font-weight-bold">
                <i class="fa fa-plus-circle me-2"></i>{{ '::AddedRows' | abpLocalization }}
              </div>
              <div class="table-responsive">
                <table class="table table-sm table-hover align-middle mb-0">
                  <thead class="table-light">
                    <tr>
                      <th style="width: 15%">{{ '::Type' | abpLocalization }}</th>
                      <th style="width: 35%">{{ '::Name' | abpLocalization }}</th>
                      <th style="width: 15%">{{ '::Quantity' | abpLocalization }} / {{ '::Time' | abpLocalization }}</th>
                      <th style="width: 15%">{{ '::Rate' | abpLocalization }} / {{ '::Cost' | abpLocalization }}</th>
                      <th style="width: 20%">{{ '::Amount' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of res.addedRows; track $index) {
                      <tr>
                        <td>
                          <span [class]="getTableRowBadgeClass(row.tableName)">
                            {{ row.tableName }}
                          </span>
                        </td>
                        <td class="fw-semibold">
                          {{ getRowIdentifier(row) }}
                        </td>
                        <td>
                          @if (row.tableName === 'Operations') {
                            {{ row.timeInMins ?? 0 }} mins
                          } @else {
                            {{ row.quantity }} {{ row.uom ?? '' }}
                          }
                        </td>
                        <td>
                          @if (row.tableName === 'Operations') {
                            {{ row.operatingCost ?? 0 | number: '1.2-2' }}
                          } @else {
                            {{ row.rate | number: '1.2-2' }}
                          }
                        </td>
                        <td>
                          {{ row.amount | number: '1.2-2' }}
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- 6. Removed Rows (Present in BOM 1, Missing in BOM 2) -->
          @if (res.removedRows.length > 0) {
            <div class="card mb-4 shadow-sm border-danger">
              <div class="card-header bg-danger-subtle text-danger-emphasis font-weight-bold">
                <i class="fa fa-minus-circle me-2"></i>{{ '::RemovedRows' | abpLocalization }}
              </div>
              <div class="table-responsive">
                <table class="table table-sm table-hover align-middle mb-0">
                  <thead class="table-light">
                    <tr>
                      <th style="width: 15%">{{ '::Type' | abpLocalization }}</th>
                      <th style="width: 35%">{{ '::Name' | abpLocalization }}</th>
                      <th style="width: 15%">{{ '::Quantity' | abpLocalization }} / {{ '::Time' | abpLocalization }}</th>
                      <th style="width: 15%">{{ '::Rate' | abpLocalization }} / {{ '::Cost' | abpLocalization }}</th>
                      <th style="width: 20%">{{ '::Amount' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of res.removedRows; track $index) {
                      <tr>
                        <td>
                          <span [class]="getTableRowBadgeClass(row.tableName)">
                            {{ row.tableName }}
                          </span>
                        </td>
                        <td class="fw-semibold">
                          {{ getRowIdentifier(row) }}
                        </td>
                        <td>
                          @if (row.tableName === 'Operations') {
                            {{ row.timeInMins ?? 0 }} mins
                          } @else {
                            {{ row.quantity }} {{ row.uom ?? '' }}
                          }
                        </td>
                        <td>
                          @if (row.tableName === 'Operations') {
                            {{ row.operatingCost ?? 0 | number: '1.2-2' }}
                          } @else {
                            {{ row.rate | number: '1.2-2' }}
                          }
                        </td>
                        <td>
                          {{ row.amount | number: '1.2-2' }}
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        }
      }
    </abp-page>
  `,
})
export class BomComparisonToolComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ManufacturingService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  boms = signal<BomDto[]>([]);
  isSubmitting = signal(false);
  diffResult = signal<BomDiffResultDto | null>(null);

  form = this.fb.group({
    bomId1: ['', Validators.required],
    bomId2: ['', Validators.required],
  });

  isSameBom(): boolean {
    const { bomId1, bomId2 } = this.form.getRawValue();
    return Boolean(bomId1 && bomId2 && bomId1 === bomId2);
  }

  ngOnInit(): void {
    const companyId = this.companyContext.currentCompanyId();
    this.service
      .getBomList({ skipCount: 0, maxResultCount: 1000, sorting: 'bomNumber', companyId } as any)
      .subscribe({
        next: (res) => this.boms.set(res.items ?? []),
        error: (err) => this.toaster.error(err?.message || '::DefaultErrorMessage'),
      });
  }

  swapBoms(): void {
    const { bomId1, bomId2 } = this.form.getRawValue();
    this.form.patchValue({
      bomId1: bomId2,
      bomId2: bomId1,
    });
    if (this.diffResult()) {
      this.compare();
    }
  }

  resetForm(): void {
    this.form.reset({ bomId1: '', bomId2: '' });
    this.diffResult.set(null);
  }

  compare(): void {
    if (this.form.invalid || this.isSameBom()) {
      this.form.markAllAsTouched();
      return;
    }

    const { bomId1, bomId2 } = this.form.getRawValue();
    this.isSubmitting.set(true);
    this.diffResult.set(null);

    this.service
      .getBomDiff({ bomId1: bomId1!, bomId2: bomId2! })
      .subscribe({
        next: (res) => {
          this.isSubmitting.set(false);
          this.diffResult.set(res);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.toaster.error(err?.error?.message || err?.message || '::DefaultErrorMessage');
        },
      });
  }

  hasDifferences(res: BomDiffResultDto): boolean {
    return (
      res.headerChanges.length > 0 ||
      res.rowChanges.length > 0 ||
      res.addedRows.length > 0 ||
      res.removedRows.length > 0
    );
  }

  getItemChanges(rowChanges: BomRowChangedDto[]): FlattenedRowChange[] {
    return this.flattenTableChanges(rowChanges, 'Items');
  }

  getOperationChanges(rowChanges: BomRowChangedDto[]): FlattenedRowChange[] {
    return this.flattenTableChanges(rowChanges, 'Operations');
  }

  getSecondaryItemChanges(rowChanges: BomRowChangedDto[]): FlattenedRowChange[] {
    return this.flattenTableChanges(rowChanges, 'SecondaryItems');
  }

  private flattenTableChanges(rowChanges: BomRowChangedDto[], tableName: string): FlattenedRowChange[] {
    const result: FlattenedRowChange[] = [];
    for (const r of rowChanges) {
      if (r.tableName === tableName) {
        for (const ch of r.changes) {
          result.push({
            rowIndex: r.rowIndex,
            identifier: r.identifier,
            fieldLabel: ch.fieldLabel || ch.fieldName,
            oldValue: ch.oldValue ?? null,
            newValue: ch.newValue ?? null,
          });
        }
      }
    }
    return result;
  }

  getRowIdentifier(row: BomChildRowDiffDto): string {
    if (row.tableName === 'Operations') {
      return row.operationName || 'Operation';
    }
    if (row.itemCode && row.itemName && row.itemCode !== row.itemName) {
      return `${row.itemCode} - ${row.itemName}`;
    }
    return row.itemCode || row.itemName || 'Item';
  }

  getTableRowBadgeClass(tableName: string): string {
    switch (tableName) {
      case 'Operations':
        return 'badge bg-warning text-dark';
      case 'SecondaryItems':
        return 'badge bg-secondary';
      default:
        return 'badge bg-info text-dark';
    }
  }
}
