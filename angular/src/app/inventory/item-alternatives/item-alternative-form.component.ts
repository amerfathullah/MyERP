import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemAlternativeService } from '../../proxy/inventory/item-alternative.service';
import { ItemService } from '../../proxy/inventory/item.service';
import type { ItemDto } from '../../proxy/inventory/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-item-alternative-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">
          <i class="bi bi-shuffle me-2"></i>
          {{ (isEditMode ? 'MyERP::EditItemAlternative' : 'MyERP::NewItemAlternative') | abpLocalization }}
        </h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::Item' | abpLocalization }} *</label>
              <select class="form-select" formControlName="itemId">
                <option value="">-- {{ 'MyERP::Select' | abpLocalization }} --</option>
                @for (item of items; track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::AlternativeItem' | abpLocalization }} *</label>
              <select class="form-select" formControlName="alternativeItemId">
                <option value="">-- {{ 'MyERP::Select' | abpLocalization }} --</option>
                @for (item of items; track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-12">
              <div class="form-check form-switch">
                <input class="form-check-input" type="checkbox" id="twoWaySwitch" formControlName="twoWay" />
                <label class="form-check-label" for="twoWaySwitch">
                  {{ 'MyERP::TwoWayAlternative' | abpLocalization }}
                </label>
                <div class="form-text text-muted">
                  {{ 'MyERP::TwoWayAlternativeHelp' | abpLocalization }}
                </div>
              </div>
            </div>
          </div>

          <div class="d-flex justify-content-end gap-2 mt-4">
            <a routerLink=".." class="btn btn-secondary btn-sm">
              {{ 'MyERP::Cancel' | abpLocalization }}
            </a>
            <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || isSaving">
              @if (isSaving) {
                <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
              }
              {{ 'MyERP::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class ItemAlternativeFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(ItemAlternativeService);
  private readonly itemService = inject(ItemService);
  private readonly companyContext = inject(CompanyContextService);
  private readonly toaster = inject(ToasterService);

  id?: string;
  isEditMode = false;
  isSaving = false;
  items: ItemDto[] = [];

  form: FormGroup = this.fb.group({
    companyId: ['', Validators.required],
    itemId: ['', Validators.required],
    alternativeItemId: ['', Validators.required],
    twoWay: [false],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.params['id'];
    this.isEditMode = !!this.id;

    const currentCompanyId = this.companyContext.selectedCompanyId();
    if (currentCompanyId) {
      this.form.patchValue({ companyId: currentCompanyId });
    }

    this.itemService.getList({ skipCount: 0, maxResultCount: 500 }).subscribe((result) => {
      this.items = result.items ?? [];
    });

    if (this.isEditMode && this.id) {
      this.service.get(this.id).subscribe((item) => {
        this.form.patchValue({
          companyId: item.companyId,
          itemId: item.itemId,
          alternativeItemId: item.alternativeItemId,
          twoWay: item.twoWay,
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;

    if (this.form.value.itemId === this.form.value.alternativeItemId) {
      this.toaster.error('Alternative item cannot be the same as the base item.');
      return;
    }

    this.isSaving = true;
    const value = this.form.value;

    const request$ = this.isEditMode && this.id
      ? this.service.update(this.id, value)
      : this.service.create(value);

    request$.subscribe({
      next: () => {
        this.isSaving = false;
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['..'], { relativeTo: this.route });
      },
      error: (err) => {
        this.isSaving = false;
        this.toaster.error(err?.error?.error?.message ?? 'Save failed');
      }
    });
  }
}
