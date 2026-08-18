import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocationService } from '../../proxy/assets/location.service';
import type { LocationDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-location-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditLocation' : 'NewLocation') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'LocationName' | abpLocalization }} *</label>
                <input class="form-control" formControlName="locationName" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'ParentLocation' | abpLocalization }}</label>
                <select class="form-select" formControlName="parentLocationId">
                  <option [ngValue]="null">—</option>
                  @for (loc of parentOptions(); track loc.id) {
                    <option [ngValue]="loc.id">{{ loc.locationName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-3">
                <div class="form-check mt-2">
                  <input class="form-check-input" type="checkbox" formControlName="isGroup" id="isGroup" />
                  <label class="form-check-label" for="isGroup">{{ 'IsGroup' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-3">
                <div class="form-check mt-2">
                  <input class="form-check-input" type="checkbox" formControlName="isContainer" id="isContainer" />
                  <label class="form-check-label" for="isContainer">{{ 'IsContainer' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'Latitude' | abpLocalization }}</label>
                <input type="number" class="form-control" formControlName="latitude" step="0.000001" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'Longitude' | abpLocalization }}</label>
                <input type="number" class="form-control" formControlName="longitude" step="0.000001" />
              </div>
            </div>
            <hr />
            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
                @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ 'Save' | abpLocalization }}
              </button>
              <a class="btn btn-secondary" routerLink="/assets/locations">{{ 'Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class LocationFormComponent implements OnInit {
  private service = inject(LocationService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  isEdit = signal(false);
  isSaving = signal(false);
  editId = signal<string | null>(null);
  parentOptions = signal<LocationDto[]>([]);

  form = this.fb.group({
    locationName: ['', Validators.required],
    parentLocationId: [null as string | null],
    isGroup: [false],
    isContainer: [false],
    latitude: [null as number | null],
    longitude: [null as number | null],
  });

  ngOnInit(): void {
    this.service.getList({ maxResultCount: 1000 } as any).subscribe(r => this.parentOptions.set(r.items ?? []));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.editId.set(id);
      this.service.get(id).subscribe(loc => {
        this.form.patchValue({
          locationName: loc.locationName,
          parentLocationId: loc.parentLocationId ?? null,
          isGroup: loc.isGroup,
          isContainer: loc.isContainer,
          latitude: loc.latitude ?? null,
          longitude: loc.longitude ?? null,
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.isSaving.set(true);
    const val = this.form.getRawValue() as any;
    const req$ = this.isEdit()
      ? this.service.update(this.editId()!, val)
      : this.service.create(val);
    req$.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/locations']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
