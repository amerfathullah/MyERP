import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DrivingLicenseCategoryService } from '../../proxy/assets/driving-license-category.service';

@Component({
  selector: 'app-driving-license-category-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditDrivingLicenseCategory' : 'NewDrivingLicenseCategory') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'CategoryName' | abpLocalization }} *</label>
              <input class="form-control" [(ngModel)]="form.categoryName" />
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'Description' | abpLocalization }}</label>
              <input class="form-control" [(ngModel)]="form.description" />
            </div>
          </div>
          <hr />
          <div class="d-flex gap-2">
            <button type="button" class="btn btn-primary" [disabled]="!form.categoryName || isSaving()" (click)="save()">
              @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
              {{ 'Save' | abpLocalization }}
            </button>
            <a class="btn btn-secondary" routerLink="/assets/driving-license-categories">{{ 'Cancel' | abpLocalization }}</a>
          </div>
        </div>
      </div>
    </abp-page>
  `,
})
export class DrivingLicenseCategoryFormComponent implements OnInit {
  private service = inject(DrivingLicenseCategoryService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  isEdit = signal(false);
  isSaving = signal(false);
  editId = signal<string | null>(null);

  form = { categoryName: '', description: '' };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.editId.set(id);
      this.service.get(id).subscribe(cat => {
        this.form = { categoryName: cat.categoryName ?? '', description: cat.description ?? '' };
      });
    }
  }

  save(): void {
    if (!this.form.categoryName) return;
    this.isSaving.set(true);
    const req$ = this.isEdit()
      ? this.service.update(this.editId()!, this.form)
      : this.service.create(this.form);
    req$.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/driving-license-categories']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
