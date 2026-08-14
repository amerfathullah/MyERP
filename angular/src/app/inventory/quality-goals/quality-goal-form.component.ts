import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityGoalStore } from '../store/quality-goal.store';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';

@Component({
  selector: 'app-quality-goal-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit ? '::Edit' : 'NewQualityGoal') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3 mb-3">
              <div class="col-md-6">
                <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
                <input type="text" class="form-control form-control-sm" formControlName="name" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::Frequency' | abpLocalization }} *</label>
                <select class="form-select form-select-sm" formControlName="frequency">
                  <option value="Daily">Daily</option>
                  <option value="Weekly">Weekly</option>
                  <option value="Monthly">Monthly</option>
                  <option value="Quarterly">Quarterly</option>
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::TargetValue' | abpLocalization }} *</label>
                <input type="number" class="form-control form-control-sm" formControlName="targetValue" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::Uom' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm" formControlName="uom" />
              </div>
              <div class="col-12">
                <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                <textarea class="form-control form-control-sm" rows="2" formControlName="goal"></textarea>
              </div>
              <div class="col-12">
                <div class="form-check">
                  <input type="checkbox" class="form-check-input" formControlName="isEnabled" id="isEnabled" />
                  <label class="form-check-label" for="isEnabled">{{ '::IsActive' | abpLocalization }}</label>
                </div>
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
                      <th style="width: 150px">{{ '::Target' | abpLocalization }} *</th>
                      <th style="width: 120px">{{ '::Uom' | abpLocalization }}</th>
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
                          <input type="text" class="form-control form-control-sm" formControlName="uom" />
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

            <div class="d-flex justify-content-end gap-2">
              <a routerLink="/inventory/quality-goals" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
              <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || store.isLoading()">
                <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityGoalFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(QualityManagementService);
  protected readonly store = inject(QualityGoalStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  form!: FormGroup;
  isEdit = false;
  id: string | null = null;

  get objectivesArray(): FormArray {
    return this.form.get('objectives') as FormArray;
  }

  ngOnInit() {
    this.form = this.fb.group({
      name: ['', Validators.required],
      goal: [''],
      frequency: ['Monthly', Validators.required],
      targetValue: [0, Validators.required],
      uom: ['%'],
      isEnabled: [true],
      objectives: this.fb.array([]),
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.isEdit = true;
      this.service.getGoal(this.id).subscribe((res) => {
        this.form.patchValue(res);
        if (res.objectives) {
          this.objectivesArray.clear();
          res.objectives.forEach((obj) => {
            this.objectivesArray.push(
              this.fb.group({
                objective: [obj.objective, Validators.required],
                target: [obj.target, Validators.required],
                uom: [obj.uom ?? ''],
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
        target: [0, Validators.required],
        uom: [this.form.get('uom')?.value ?? '%'],
      })
    );
  }

  removeObjective(index: number) {
    this.objectivesArray.removeAt(index);
  }

  save() {
    if (this.form.invalid) return;

    const val = this.form.value;
    const onSuccess = () => this.router.navigate(['/inventory/quality-goals']);

    if (this.isEdit && this.id) {
      this.store.update({ id: this.id, input: val, onSuccess });
    } else {
      this.store.create({ input: val, onSuccess });
    }
  }
}
