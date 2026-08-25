import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { SupplierGroupService } from '../../proxy/core/supplier-group.service';
import type { SupplierGroupDto } from '../../proxy/core/models';

@Component({
  selector: 'app-supplier-group-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Supplier Group</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="mb-3">
            <label class="form-label">Supplier Group Name *</label>
            <input type="text" class="form-control" formControlName="name" maxlength="140" placeholder="e.g. Raw Material, Services, Hardware, Logistics">
          </div>

          <div class="mb-3">
            <label class="form-label">Parent Supplier Group</label>
            <select class="form-select" formControlName="parentId">
              <option [ngValue]="null">— None (Root Group) —</option>
              @for (g of groups(); track g.id) {
                @if (g.id !== id) {
                  <option [value]="g.id">{{ g.name }}</option>
                }
              }
            </select>
          </div>

          <div class="mb-3">
            <div class="form-check form-switch">
              <input type="checkbox" class="form-check-input" formControlName="isGroup" id="isGroup">
              <label class="form-check-label" for="isGroup">Is Group (Folder node — contains other supplier groups)</label>
            </div>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/settings/supplier-groups" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class SupplierGroupFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(SupplierGroupService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  groups = signal<SupplierGroupDto[]>([]);
  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(140)]],
      parentId: [null],
      isGroup: [false],
    });
  }

  ngOnInit() {
    this.service.getList({ isGroup: true, maxResultCount: 200 } as any).subscribe(r => {
      this.groups.set(r.items ?? []);
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue(res);
      });
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/settings/supplier-groups']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
