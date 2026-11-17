import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ItemManufacturerService } from '../../proxy/inventory/item-manufacturer.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { ManufacturerService } from '../../proxy/inventory/manufacturer.service';
import type { ItemDto, ManufacturerDto } from '../../proxy/inventory/models';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-item-manufacturer-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <div class="card border-0 shadow-sm">
        <div class="card-header">
          <h5 class="mb-0">
            <i class="fas fa-cogs text-primary me-2"></i>
            {{ (isEditMode ? '::EditItemManufacturer' : '::NewItemManufacturer') | abpLocalization }}
          </h5>
        </div>
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ '::Item' | abpLocalization }} *</label>
                <select class="form-select" formControlName="itemId">
                  <option value="">-- {{ '::Select' | abpLocalization }} --</option>
                  @for (item of items(); track item.id) {
                    <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::Manufacturers' | abpLocalization }} *</label>
                <select class="form-select" formControlName="manufacturerId">
                  <option value="">-- {{ '::Select' | abpLocalization }} --</option>
                  @for (mfr of manufacturers(); track mfr.id) {
                    <option [value]="mfr.id">{{ mfr.shortName }}{{ mfr.fullName ? ' - ' + mfr.fullName : '' }}</option>
                  }
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::ManufacturerPartNo' | abpLocalization }} *</label>
                <input type="text" class="form-control" formControlName="manufacturerPartNo" />
              </div>
              <div class="col-md-6 d-flex align-items-end">
                <div class="form-check mb-2">
                  <input
                    id="isDefaultCheckbox"
                    type="checkbox"
                    class="form-check-input"
                    formControlName="isDefault"
                  />
                  <label class="form-check-label" for="isDefaultCheckbox">
                    {{ '::IsDefault' | abpLocalization }}
                  </label>
                </div>
              </div>
              <div class="col-12">
                <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                <textarea class="form-control" rows="2" formControlName="description"></textarea>
              </div>
            </div>
            <div class="mt-3 d-flex gap-2">
              <button
                type="submit"
                class="btn btn-primary btn-sm"
                [disabled]="saving() || form.invalid"
              >
                @if (saving()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                }
                {{ '::Save' | abpLocalization }}
              </button>
              <a routerLink="/inventory/item-manufacturers" class="btn btn-outline-secondary btn-sm">
                {{ '::Cancel' | abpLocalization }}
              </a>
            </div>
          </form>
        </div>
      </div>
    </div>
  `,
})
export class ItemManufacturerFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(ItemManufacturerService);
  private itemService = inject(ItemService);
  private manufacturerService = inject(ManufacturerService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  items = signal<ItemDto[]>([]);
  manufacturers = signal<ManufacturerDto[]>([]);
  saving = signal(false);

  isEditMode = false;
  editId = '';

  form: FormGroup = this.fb.group({
    companyId: ['', Validators.required],
    itemId: ['', Validators.required],
    manufacturerId: ['', Validators.required],
    manufacturerPartNo: ['', [Validators.required, Validators.maxLength(128)]],
    description: [''],
    isDefault: [false],
  });

  ngOnInit(): void {
    this.companyContext.load();
    this.editId = this.route.snapshot.paramMap.get('id') ?? '';
    this.isEditMode = Boolean(this.editId);

    this.companyService.getList({ skipCount: 0, maxResultCount: 50, sorting: '' }).subscribe({
      next: res => {
        const companyId = this.companyContext.currentCompanyId() || res.items?.[0]?.id || '';
        if (!this.form.get('companyId')?.value) {
          this.form.patchValue({ companyId });
        }
        this.loadLookups(companyId);
      },
    });

    this.manufacturerService.getList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: res => this.manufacturers.set(res.items ?? []),
    });

    if (this.isEditMode) {
      this.service.get(this.editId).subscribe({
        next: dto => {
          this.form.patchValue({
            companyId: dto.companyId,
            itemId: dto.itemId,
            manufacturerId: dto.manufacturerId,
            manufacturerPartNo: dto.manufacturerPartNo,
            description: dto.description ?? '',
            isDefault: Boolean(dto.isDefault),
          });
        },
      });
    }
  }

  loadLookups(companyId: string): void {
    this.itemService.getList({ companyId: companyId || undefined, maxResultCount: 300 }).subscribe({
      next: res => this.items.set(res.items ?? []),
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = this.form.value;
    const req$ = this.isEditMode
      ? this.service.update(this.editId, payload)
      : this.service.create(payload);

    req$.subscribe({
      next: () => {
        this.toaster.success('::SavedSuccessfully');
        this.saving.set(false);
        this.router.navigate(['/inventory/item-manufacturers']);
      },
      error: (err) => {
        this.saving.set(false);
        this.toaster.error(err?.error?.error?.message || '::FailedToSaveItemManufacturer');
      },
    });
  }
}
