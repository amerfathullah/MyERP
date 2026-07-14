import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';

interface AccountingDimensionDto {
  id: string;
  documentType: string;
  label: string;
  fieldName: string;
  isEnabled: boolean;
  isMandatory: boolean;
  companyId: string | null;
}

@Component({
  selector: 'app-accounting-dimensions',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'AccountingDimensions' | abpLocalization">
      <!-- Create form -->
      <div class="card mb-4">
        <div class="card-header">
          <h6 class="card-title mb-0"><i class="fa fa-plus me-2"></i>{{ 'NewDimension' | abpLocalization }}</h6>
        </div>
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="create()" class="row g-3 align-items-end">
            <div class="col-md-3">
              <label class="form-label">{{ 'DocumentType' | abpLocalization }}</label>
              <select class="form-select" formControlName="documentType">
                <option value="" disabled>Select...</option>
                <option value="Branch">Branch</option>
                <option value="Department">Department</option>
                <option value="CostCenter">Cost Center</option>
                <option value="Project">Project</option>
                <option value="Territory">Territory</option>
                <option value="CustomerGroup">Customer Group</option>
                <option value="SupplierGroup">Supplier Group</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'Label' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="label" placeholder="e.g. Branch" />
            </div>
            <div class="col-md-2">
              <div class="form-check mt-4">
                <input class="form-check-input" type="checkbox" formControlName="isMandatory" id="isMandatory">
                <label class="form-check-label" for="isMandatory">{{ 'Mandatory' | abpLocalization }}</label>
              </div>
            </div>
            <div class="col-md-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || saving()">
                <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
              </button>
            </div>
          </form>
        </div>
      </div>

      <!-- Dimensions list -->
      <div class="card">
        <div class="card-header">
          <h6 class="card-title mb-0">{{ 'AccountingDimensions' | abpLocalization }}</h6>
        </div>
        <div class="card-body">
          @if (loading()) {
            <div class="text-center p-4"><i class="fa fa-spinner fa-spin"></i></div>
          } @else if (dimensions().length === 0) {
            <div class="text-center text-muted p-4">
              <i class="fa fa-cubes fa-2x mb-2 d-block"></i>
              {{ 'NoDimensionsYet' | abpLocalization }}
            </div>
          } @else {
            <table class="table table-hover">
              <thead>
                <tr>
                  <th>{{ 'DocumentType' | abpLocalization }}</th>
                  <th>{{ 'Label' | abpLocalization }}</th>
                  <th>{{ 'FieldName' | abpLocalization }}</th>
                  <th>{{ 'Mandatory' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (dim of dimensions(); track dim.id) {
                  <tr>
                    <td>{{ dim.documentType }}</td>
                    <td>{{ dim.label }}</td>
                    <td><code>{{ dim.fieldName }}</code></td>
                    <td>
                      @if (dim.isMandatory) {
                        <span class="badge bg-warning">{{ 'Mandatory' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-light text-dark">Optional</span>
                      }
                    </td>
                    <td>
                      @if (dim.isEnabled) {
                        <span class="badge bg-success">Enabled</span>
                      } @else {
                        <span class="badge bg-secondary">Disabled</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (dim.isEnabled) {
                          <button class="btn btn-outline-secondary" (click)="toggleEnable(dim, false)">
                            <i class="fa fa-ban"></i>
                          </button>
                        } @else {
                          <button class="btn btn-outline-success" (click)="toggleEnable(dim, true)">
                            <i class="fa fa-check"></i>
                          </button>
                        }
                        <button class="btn btn-outline-danger" (click)="remove(dim)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </abp-page>
  `
})
export class AccountingDimensionsComponent implements OnInit {
  private http = inject(HttpClient);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);

  dimensions = signal<AccountingDimensionDto[]>([]);
  loading = signal(true);
  saving = signal(false);

  form = this.fb.group({
    documentType: ['', Validators.required],
    label: ['', Validators.required],
    isMandatory: [false]
  });

  ngOnInit() {
    this.loadDimensions();
  }

  loadDimensions() {
    this.loading.set(true);
    this.http.get<any>('/api/app/accounting-dimension').subscribe({
      next: (res) => {
        this.dimensions.set(res.items ?? res ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  create() {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.http.post<AccountingDimensionDto>('/api/app/accounting-dimension', this.form.value).subscribe({
      next: () => {
        this.toaster.success('Dimension created successfully');
        this.form.reset({ documentType: '', label: '', isMandatory: false });
        this.saving.set(false);
        this.loadDimensions();
      },
      error: () => this.saving.set(false)
    });
  }

  toggleEnable(dim: AccountingDimensionDto, enable: boolean) {
    const url = enable
      ? `/api/app/accounting-dimension/${dim.id}/enable`
      : `/api/app/accounting-dimension/${dim.id}/disable`;
    this.http.post(url, {}).subscribe({
      next: () => this.loadDimensions(),
      error: () => {}
    });
  }

  remove(dim: AccountingDimensionDto) {
    if (!confirm(`Delete dimension "${dim.label}"?`)) return;
    this.http.delete(`/api/app/accounting-dimension/${dim.id}`).subscribe({
      next: () => {
        this.toaster.success('Dimension deleted');
        this.loadDimensions();
      }
    });
  }
}
