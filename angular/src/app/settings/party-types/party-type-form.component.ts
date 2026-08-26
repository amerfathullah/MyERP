import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { PartyTypeService } from '../../proxy/core/party-type.service';
import { partyAccountTypeOptions } from '../../proxy/core/party-account-type.enum';

@Component({
  selector: 'app-party-type-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Party Type</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Party Type Name *</label>
              <input type="text" class="form-control" formControlName="name" placeholder="e.g. Customer, Supplier, Employee">
            </div>
            <div class="col-md-6">
              <label class="form-label">Account Type *</label>
              <select class="form-select" formControlName="accountType">
                @for (opt of accountTypeOptions; track opt.value) {
                  <option [value]="opt.value">{{ opt.key }}</option>
                }
              </select>
            </div>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/settings/party-types" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class PartyTypeFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(PartyTypeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;
  accountTypeOptions = partyAccountTypeOptions;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(140)]],
      accountType: [0, [Validators.required]],
    });
  }

  ngOnInit() {
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
        this.router.navigate(['/settings/party-types']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
