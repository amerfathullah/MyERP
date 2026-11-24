import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { BankService } from '../../proxy/accounting/bank.service';

@Component({
  selector: 'app-bank-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ (isEdit ? '::EditBank' : '::NewBank') | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="mb-3">
            <label class="form-label">{{ '::BankName' | abpLocalization }} *</label>
            <input type="text" class="form-control" formControlName="bankName" [placeholder]="'::Placeholder:BankNameExample' | abpLocalization">
          </div>

          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">{{ '::SwiftBicCode' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="swiftNumber" [placeholder]="'::Placeholder:SwiftCodeExample' | abpLocalization">
            </div>
            <div class="col-md-6 mb-3">
              <label class="form-label">{{ '::Website' | abpLocalization }}</label>
              <input type="url" class="form-control" formControlName="website" [placeholder]="'::Placeholder:BankWebsiteExample' | abpLocalization">
            </div>
          </div>

          <div class="form-check form-switch mb-4">
            <input class="form-check-input" type="checkbox" id="isActive" formControlName="isActive">
            <label class="form-check-label" for="isActive">{{ '::Active' | abpLocalization }}</label>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">{{ '::Save' | abpLocalization }}</button>
            <a routerLink="/accounting/banks" class="btn btn-secondary">{{ '::Cancel' | abpLocalization }}</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class BankFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(BankService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      bankName: ['', [Validators.required, Validators.maxLength(100)]],
      swiftNumber: ['', Validators.maxLength(50)],
      website: ['', Validators.maxLength(200)],
      isActive: [true],
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
        this.router.navigate(['/accounting/banks']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::Failed'),
    });
  }
}
