import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityReviewStore } from '../store/quality-review.store';
import { QualityGoalStore } from '../store/quality-goal.store';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import { QualityReviewStatus } from '../../proxy/inventory/quality-review-status.enum';

@Component({
  selector: 'app-quality-review-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit ? '::ReviewDetails' : 'NewQualityReview') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3 mb-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'QualityGoal' | abpLocalization }} *</label>
                <select class="form-select form-select-sm" formControlName="qualityGoalId">
                  <option value="">{{ '::Select' | abpLocalization }}</option>
                  @for (goal of goalStore.entities(); track goal.id) {
                    <option [value]="goal.id">{{ goal.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::ReviewDate' | abpLocalization }} *</label>
                <input type="date" class="form-control form-control-sm" formControlName="reviewDate" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::ActualValue' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" formControlName="actualValue" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::Status' | abpLocalization }}</label>
                <div>
                  <span class="badge py-2 px-3" [ngClass]="getStatusBadgeClass(form.get('status')?.value)">
                    {{ getStatusLabel(form.get('status')?.value) }}
                  </span>
                </div>
              </div>
              <div class="col-12">
                <label class="form-label">{{ '::Notes' | abpLocalization }}</label>
                <textarea class="form-control form-control-sm" rows="2" formControlName="notes"></textarea>
              </div>
            </div>

            <!-- Objectives Child Table -->
            <div class="card bg-light mb-3">
              <div class="card-header d-flex justify-content-between align-items-center py-2">
                <span class="fw-semibold">{{ '::Objectives' | abpLocalization }}</span>
                <button type="button" class="btn btn-outline-primary btn-sm" (click)="addObjective()">
                  <i class="fa fa-plus me-1"></i>{{ '::Add' | abpLocalization }}
                </button>
              </div>
              <div class="card-body p-0">
                <table class="table table-sm mb-0">
                  <thead>
                    <tr>
                      <th>{{ '::Objective' | abpLocalization }} *</th>
                      <th style="width: 120px">{{ '::Target' | abpLocalization }}</th>
                      <th style="width: 120px">{{ '::Actual' | abpLocalization }}</th>
                      <th style="width: 130px">{{ '::Status' | abpLocalization }}</th>
                      <th style="width: 60px"></th>
                    </tr>
                  </thead>
                  <tbody formArrayName="objectives">
                    @for (obj of objectivesArray.controls; track $index; let i = $index) {
                      <tr [formGroupName]="i">
                        <td>
                          <input type="text" class="form-control form-control-sm" formControlName="objective" />
                        </td>
                        <td>
                          <input type="number" class="form-control form-control-sm" formControlName="target" />
                        </td>
                        <td>
                          <input type="number" class="form-control form-control-sm" formControlName="actual" />
                        </td>
                        <td>
                          <select class="form-select form-select-sm" formControlName="status">
                            <option [value]="QualityReviewStatus.Open">Open</option>
                            <option [value]="QualityReviewStatus.Passed">Passed</option>
                            <option [value]="QualityReviewStatus.Failed">Failed</option>
                          </select>
                        </td>
                        <td class="text-center">
                          <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeObjective(i)">
                            <i class="fa fa-trash"></i>
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>

            <div class="d-flex justify-content-between align-items-center">
              <div class="d-flex gap-2">
                @if (isEdit && id) {
                  <button type="button" class="btn btn-success btn-sm" (click)="evaluate(true)">
                    <i class="fa fa-check me-1"></i>Mark Passed
                  </button>
                  <button type="button" class="btn btn-danger btn-sm" (click)="evaluate(false)">
                    <i class="fa fa-times me-1"></i>Mark Failed
                  </button>
                }
              </div>
              <div class="d-flex gap-2">
                <a routerLink="/inventory/quality-reviews" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
                @if (!isEdit) {
                  <button type="button" class="btn btn-outline-primary btn-sm"
                    [disabled]="!canAutoEvaluate() || store.isLoading()" (click)="autoEvaluate()">
                    <i class="fa fa-bolt me-1"></i>{{ '::AutoEvaluate' | abpLocalization }}
                  </button>
                }
                <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || store.isLoading()">
                  <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
                </button>
              </div>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityReviewFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(QualityManagementService);
  protected readonly store = inject(QualityReviewStore);
  protected readonly goalStore = inject(QualityGoalStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly QualityReviewStatus = QualityReviewStatus;
  form!: FormGroup;
  isEdit = false;
  id: string | null = null;

  get objectivesArray(): FormArray {
    return this.form.get('objectives') as FormArray;
  }

  ngOnInit() {
    this.goalStore.load({ maxResultCount: 100, skipCount: 0 });

    this.form = this.fb.group({
      qualityGoalId: ['', Validators.required],
      reviewDate: [new Date().toISOString().substring(0, 10), Validators.required],
      actualValue: [null],
      status: [QualityReviewStatus.Open],
      notes: [''],
      objectives: this.fb.array([]),
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.isEdit = true;
      this.service.getReview(this.id).subscribe((res) => {
        this.form.patchValue({
          qualityGoalId: res.qualityGoalId,
          reviewDate: res.reviewDate ? res.reviewDate.substring(0, 10) : '',
          actualValue: res.actualValue,
          status: res.status,
          notes: res.notes,
        });
        if (res.objectives) {
          this.objectivesArray.clear();
          res.objectives.forEach((obj) => {
            this.objectivesArray.push(
              this.fb.group({
                objective: [obj.objective, Validators.required],
                target: [obj.target, Validators.required],
                actual: [obj.actual],
                status: [obj.status ?? QualityReviewStatus.Open],
              })
            );
          });
        }
      });
    }
  }

  addObjective() {
    this.objectivesArray.push(
      this.fb.group({
        objective: ['', Validators.required],
        target: [100, Validators.required],
        actual: [null],
        status: [QualityReviewStatus.Open],
      })
    );
  }

  removeObjective(index: number) {
    this.objectivesArray.removeAt(index);
  }

  save() {
    if (this.form.invalid) return;

    const val = this.form.value;
    const onSuccess = () => this.router.navigate(['/inventory/quality-reviews']);

    this.store.create({ input: val, onSuccess });
  }

  canAutoEvaluate(): boolean {
    const v = this.form.value;
    return !!v.qualityGoalId && !!v.reviewDate && v.actualValue !== null && v.actualValue !== undefined;
  }

  autoEvaluate() {
    if (!this.canAutoEvaluate()) return;
    const v = this.form.value;
    this.store.autoEvaluate({
      goalId: v.qualityGoalId,
      input: {
        actualValue: v.actualValue,
        reviewDate: v.reviewDate,
        notes: v.notes,
      },
      onSuccess: () => this.router.navigate(['/inventory/quality-reviews']),
    });
  }

  evaluate(passed: boolean) {
    if (!this.id) return;
    this.store.evaluate({
      id: this.id,
      input: {
        passed,
        actualValue: this.form.get('actualValue')?.value,
        notes: this.form.get('notes')?.value,
      },
      onSuccess: () => {
        this.form.patchValue({ status: passed ? QualityReviewStatus.Passed : QualityReviewStatus.Failed });
      },
    });
  }

  getStatusBadgeClass(status?: QualityReviewStatus): string {
    switch (Number(status)) {
      case QualityReviewStatus.Passed:
        return 'bg-success';
      case QualityReviewStatus.Failed:
        return 'bg-danger';
      default:
        return 'bg-warning text-dark';
    }
  }

  getStatusLabel(status?: QualityReviewStatus): string {
    switch (Number(status)) {
      case QualityReviewStatus.Passed:
        return 'Passed';
      case QualityReviewStatus.Failed:
        return 'Failed';
      default:
        return 'Open';
    }
  }
}
