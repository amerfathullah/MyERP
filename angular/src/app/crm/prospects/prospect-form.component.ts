import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ProspectService } from '../../proxy/crm/prospect.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-prospect-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">{{ (isEditMode ? 'MyERP::EditProspect' : 'MyERP::NewProspect') | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::ProspectName' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="prospectName" />
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::Industry' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="industry" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::Website' | abpLocalization }}</label>
              <input type="url" class="form-control" formControlName="website" placeholder="https://" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-12">
              <label class="form-label">{{ 'MyERP::Notes' | abpLocalization }}</label>
              <textarea class="form-control" formControlName="notes" rows="4"></textarea>
            </div>
          </div>

          <div class="d-flex justify-content-end gap-2">
            <a routerLink=".." class="btn btn-secondary">{{ 'MyERP::Cancel' | abpLocalization }}</a>
            <button type="submit" class="btn btn-primary" [disabled]="!form.valid || saving">
              {{ 'MyERP::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
})
export class ProspectFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ProspectService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form!: FormGroup;
  saving = false;
  isEditMode = false;
  editId: string | null = null;

  ngOnInit() {
    this.form = this.fb.group({
      prospectName: ['', Validators.required],
      industry: [''],
      website: [''],
      notes: [''],
    });

    this.editId = this.route.snapshot.paramMap.get('id');
    if (this.editId) {
      this.isEditMode = true;
      this.service.get(this.editId).subscribe({
        next: (p) => {
          this.form.patchValue({
            prospectName: p.prospectName,
            industry: p.industry,
            website: p.website,
            notes: p.notes,
          });
        },
      });
    }
  }

  save() {
    if (!this.form.valid) return;
    this.saving = true;

    const action$ = this.isEditMode
      ? this.service.update(this.editId!, this.form.value)
      : this.service.create(this.form.value);

    action$.subscribe({
      next: () => {
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['../'], { relativeTo: this.route });
      },
      error: () => { this.saving = false; },
    });
  }
}
