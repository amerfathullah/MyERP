import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { CustomerGroupService } from '../../proxy/core/customer-group.service';
import type { CustomerGroupDto } from '../../proxy/core/models';

@Component({
  selector: 'app-customer-group-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ (isEdit ? '::EditCustomerGroup' : '::NewCustomerGroup') | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="mb-3">
            <label class="form-label">{{ '::CustomerGroupName' | abpLocalization }} *</label>
            <input type="text" class="form-control" formControlName="name" maxlength="140" [placeholder]="'::Placeholder:CustomerGroupExample' | abpLocalization">
          </div>

          <div class="mb-3">
            <label class="form-label">{{ '::ParentCustomerGroup' | abpLocalization }}</label>
            <select class="form-select" formControlName="parentId">
              <option [ngValue]="null">{{ '::NoneRootGroup' | abpLocalization }}</option>
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
              <label class="form-check-label" for="isGroup">{{ '::IsGroupCustomerHelp' | abpLocalization }}</label>
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">{{ '::DefaultCreditLimit' | abpLocalization }}</label>
            <input type="number" step="0.01" class="form-control" formControlName="defaultCreditLimit" placeholder="0.00">
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">{{ '::Save' | abpLocalization }}</button>
            <a routerLink="/settings/customer-groups" class="btn btn-secondary">{{ '::Cancel' | abpLocalization }}</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class CustomerGroupFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(CustomerGroupService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  groups = signal<CustomerGroupDto[]>([]);
  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(140)]],
      parentId: [null],
      isGroup: [false],
      defaultCreditLimit: [0],
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
        this.router.navigate(['/settings/customer-groups']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
