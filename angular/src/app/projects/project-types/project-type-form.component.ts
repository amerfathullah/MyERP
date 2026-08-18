import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProjectTypeService } from '../../proxy/projects/project-type.service';

@Component({
  selector: 'app-project-type-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditProjectType' : 'NewProjectType') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'Name' | abpLocalization }} *</label>
              <input class="form-control" [(ngModel)]="form.name" />
            </div>
            <div class="col-md-3">
              <div class="form-check mt-4">
                <input class="form-check-input" type="checkbox" [(ngModel)]="form.isActive" id="isActive" />
                <label class="form-check-label" for="isActive">{{ 'Active' | abpLocalization }}</label>
              </div>
            </div>
          </div>
          <hr />
          <div class="d-flex gap-2">
            <button type="button" class="btn btn-primary" [disabled]="!form.name || isSaving()" (click)="save()">
              @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
              {{ 'Save' | abpLocalization }}
            </button>
            <a class="btn btn-secondary" routerLink="/projects/project-types">{{ 'Cancel' | abpLocalization }}</a>
          </div>
        </div>
      </div>
    </abp-page>
  `,
})
export class ProjectTypeFormComponent implements OnInit {
  private service = inject(ProjectTypeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  isEdit = signal(false);
  isSaving = signal(false);
  editId = signal<string | null>(null);

  form = { name: '', isActive: true };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.editId.set(id);
      this.service.get(id).subscribe(t => { this.form = { name: t.name ?? '', isActive: t.isActive ?? true }; });
    }
  }

  save(): void {
    if (!this.form.name) return;
    this.isSaving.set(true);
    const req$ = this.isEdit()
      ? this.service.update(this.editId()!, this.form)
      : this.service.create(this.form);
    req$.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/projects/project-types']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
