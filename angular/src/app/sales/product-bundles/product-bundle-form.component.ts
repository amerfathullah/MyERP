import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProductBundleService } from '../../proxy/sales/product-bundle.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-product-bundle-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe, SaveShortcutDirective, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid py-3">
      <h4 class="mb-3">
        <i class="fa fa-cubes me-2 text-primary"></i>
        {{ '::NewProductBundle' | abpLocalization }}
      </h4>

      <form [formGroup]="form" (appSaveShortcut)="save()">
        <div class="card shadow-sm mb-3">
          <div class="card-header"><h6 class="mb-0">{{ '::BundleDetails' | abpLocalization }}</h6></div>
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ '::ParentItem' | abpLocalization }} *</label>
                <select class="form-select" formControlName="parentItemId">
                  <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                  @for (item of availableItems(); track item.id) {
                    <option [value]="item.id">{{ item.itemCode }} — {{ item.itemName }}</option>
                  }
                </select>
                <small class="text-muted">{{ '::ParentItemHelp' | abpLocalization }}</small>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="description">
              </div>
            </div>
          </div>
        </div>

        <!-- Component Items -->
        <div class="card shadow-sm mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">{{ '::ComponentItems' | abpLocalization }}</h6>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addComponent()">
              <i class="fa fa-plus me-1"></i>{{ '::AddComponent' | abpLocalization }}
            </button>
          </div>
          <div class="card-body p-0">
            <table class="table mb-0">
              <thead>
                <tr>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody formArrayName="items">
                @for (comp of componentsArray.controls; track $index; let i = $index) {
                  <tr [formGroupName]="i">
                    <td>
                      <select class="form-select form-select-sm" formControlName="itemId" (change)="onComponentItemSelected(i)">
                        <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                        @for (item of availableItems(); track item.id) {
                          <option [value]="item.id">{{ item.itemCode }} — {{ item.itemName }}</option>
                        }
                      </select>
                    </td>
                    <td><input type="text" class="form-control form-control-sm" formControlName="description"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="quantity" min="0.01" step="0.01"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="rate" min="0" step="0.01"></td>
                    <td>
                      <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeComponent(i)">
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
          <button type="button" class="btn btn-outline-secondary" routerLink="/sales/product-bundles">{{ '::Cancel' | abpLocalization }}</button>
          <button type="button" class="btn btn-primary" (click)="save()" [disabled]="saving()">
            <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </div>
  `,
})
export class ProductBundleFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(ProductBundleService);
  private toaster = inject(ToasterService);
  private http = inject(HttpClient);

  saving = signal(false);
  availableItems = signal<any[]>([]);

  form = this.fb.group({
    parentItemId: ['', Validators.required],
    description: [''],
    items: this.fb.array([]),
  });

  get componentsArray() { return this.form.get('items') as FormArray; }

  ngOnInit() {
    this.http.get<any>('/api/app/item?maxResultCount=500').subscribe({
      next: (res) => this.availableItems.set(res.items || []),
      error: () => {},
    });
    this.addComponent();
  }

  addComponent() {
    this.componentsArray.push(this.fb.group({
      itemId: ['', Validators.required],
      description: [''],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
      rate: [0],
    }));
  }

  removeComponent(i: number) { this.componentsArray.removeAt(i); }

  onComponentItemSelected(index: number) {
    const group = this.componentsArray.at(index);
    const itemId = group.get('itemId')!.value;
    const item = this.availableItems().find((i: any) => i.id === itemId);
    if (item) {
      group.patchValue({ description: item.itemName || item.description || '' });
    }
  }

  save() {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);

    const raw = this.form.getRawValue();
    const dto = {
      parentItemId: raw.parentItemId,
      description: raw.description || undefined,
      items: (raw.items || []).filter((i: any) => i.itemId).map((i: any) => ({
        itemId: i.itemId,
        description: i.description || '',
        quantity: i.quantity,
        rate: i.rate || 0,
      })),
    };

    this.service.create(dto as any).subscribe({
      next: (created) => {
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/sales/product-bundles']);
      },
      error: () => this.saving.set(false),
    });
  }

  hasUnsavedChanges() { return this.form.dirty; }
}
