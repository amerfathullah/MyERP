import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { QualityProcedureDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-quality-procedure-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit ? '::EditQualityProcedure' : 'NewQualityProcedure') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3 mb-3">
              <div class="col-md-6">
                <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
                <input type="text" class="form-control form-control-sm" formControlName="name" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::ParentProcedure' | abpLocalization }}</label>
                <select class="form-select form-select-sm" formControlName="parentQualityProcedureId">
                  <option [ngValue]="null">{{ '::None' | abpLocalization }} (Root)</option>
                  @for (p of parentProcedures; track p.id) {
                    @if (!id || p.id !== id) {
                      <option [value]="p.id">{{ p.name }}</option>
                    }
                  }
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::ProcessOwner' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm" formControlName="processOwner" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::Sequence' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" formControlName="sequence" />
              </div>
              <div class="col-12">
                <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                <textarea class="form-control form-control-sm" rows="2" formControlName="description"></textarea>
              </div>
              <div class="col-12">
                <div class="form-check">
                  <input type="checkbox" class="form-check-input" formControlName="isGroup" id="isGroup" />
                  <label class="form-check-label" for="isGroup">{{ '::IsGroup' | abpLocalization }}</label>
                </div>
              </div>
            </div>

            <!-- Steps Table -->
            <div class="card bg-light mb-3">
              <div class="card-header d-flex justify-content-between align-items-center py-2">
                <span class="fw-semibold">{{ '::ProcedureSteps' | abpLocalization }}</span>
                <button type="button" class="btn btn-outline-primary btn-sm" (click)="addStep()">
                  <i class="fa fa-plus me-1"></i>{{ '::AddStep' | abpLocalization }}
                </button>
              </div>
              <div class="card-body p-0">
                <table class="table table-sm mb-0">
                  <thead>
                    <tr>
                      <th style="width: 70px">{{ '::Seq' | abpLocalization }}</th>
                      <th>{{ '::StepDescription' | abpLocalization }} *</th>
                      <th style="width: 250px">{{ '::ChildProcedure' | abpLocalization }}</th>
                      <th style="width: 60px"></th>
                    </tr>
                  </thead>
                  <tbody formArrayName="steps">
                    @for (step of stepsArray.controls; track $index; let i = $index) {
                      <tr [formGroupName]="i">
                        <td>
                          <input type="number" class="form-control form-control-sm" formControlName="sequence" />
                        </td>
                        <td>
                          <input type="text" class="form-control form-control-sm" formControlName="description" />
                        </td>
                        <td>
                          <select class="form-select form-select-sm" formControlName="childProcedureId">
                            <option [ngValue]="null">{{ '::None' | abpLocalization }}</option>
                            @for (p of parentProcedures; track p.id) {
                              @if (!id || p.id !== id) {
                                <option [value]="p.id">{{ p.name }}</option>
                              }
                            }
                          </select>
                        </td>
                        <td class="text-center">
                          <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeStep(i)">
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
              <a routerLink="/inventory/quality-procedures" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
              <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || isSaving">
                <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityProcedureFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(QualityManagementService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  form!: FormGroup;
  isEdit = false;
  isSaving = false;
  id: string | null = null;
  parentProcedures: QualityProcedureDto[] = [];

  get stepsArray(): FormArray {
    return this.form.get('steps') as FormArray;
  }

  ngOnInit() {
    this.service.getProcedureList({ maxResultCount: 100, skipCount: 0 }).subscribe((res) => {
      this.parentProcedures = res.items ?? [];
    });

    this.form = this.fb.group({
      name: ['', Validators.required],
      parentQualityProcedureId: [null],
      isGroup: [false],
      description: [''],
      processOwner: [''],
      sequence: [1],
      steps: this.fb.array([]),
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.isEdit = true;
      this.service.getProcedure(this.id).subscribe((res) => {
        this.form.patchValue({
          name: res.name,
          parentQualityProcedureId: res.parentQualityProcedureId ?? null,
          isGroup: res.isGroup ?? false,
          description: res.description ?? '',
          processOwner: res.processOwner ?? '',
          sequence: res.sequence ?? 1,
        });

        if (res.steps) {
          this.stepsArray.clear();
          res.steps.forEach((step) => {
            this.stepsArray.push(
              this.fb.group({
                sequence: [step.sequence ?? 1],
                description: [step.description, Validators.required],
                childProcedureId: [step.childProcedureId ?? null],
              })
            );
          });
        }
      });
    }
  }

  addStep() {
    this.stepsArray.push(
      this.fb.group({
        sequence: [this.stepsArray.length + 1],
        description: ['', Validators.required],
        childProcedureId: [null],
      })
    );
  }

  removeStep(index: number) {
    this.stepsArray.removeAt(index);
  }

  save() {
    if (this.form.invalid) return;

    this.isSaving = true;
    const val = this.form.value;

    const op = this.isEdit && this.id
      ? this.service.updateProcedure(this.id, val)
      : this.service.createProcedure(val);

    op.subscribe({
      next: () => {
        this.isSaving = false;
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/inventory/quality-procedures']);
      },
      error: (err) => {
        this.isSaving = false;
        this.toaster.error(err?.error?.error?.message ?? 'Save failed');
      },
    });
  }
}
