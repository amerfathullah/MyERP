import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { PrintFormatService } from '../../proxy/settings/print-format.service';

@Component({
  selector: 'app-print-format-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title">{{ (isEdit ? '::EditPrintFormat' : '::NewPrintFormat') | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="mb-3">
            <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
            <input type="text" class="form-control" formControlName="name">
          </div>
          <div class="mb-3">
            <label class="form-label">{{ '::DocumentType' | abpLocalization }} *</label>
            <select class="form-select" formControlName="documentType">
              <option value="SalesInvoice">{{ '::SalesInvoice' | abpLocalization }}</option>
              <option value="PurchaseOrder">{{ '::PurchaseOrder' | abpLocalization }}</option>
              <option value="DeliveryNote">{{ '::DeliveryNote' | abpLocalization }}</option>
              <option value="PaymentEntry">{{ '::PaymentEntry' | abpLocalization }}</option>
            </select>
          </div>
          <div class="mb-3 form-check">
            <input type="checkbox" class="form-check-input" formControlName="isDefault" id="isDefault">
            <label class="form-check-label" for="isDefault">{{ '::IsDefault' | abpLocalization }}</label>
          </div>
          <div class="mb-3">
            <label class="form-label">{{ '::HtmlTemplate' | abpLocalization }} *</label>
            <textarea class="form-control" formControlName="htmlTemplate" rows="10"></textarea>
            <small class="form-text text-muted">{{ '::HtmlTemplateHelp' | abpLocalization }}</small>
          </div>
          <div class="mb-3">
            <label class="form-label">{{ '::CssStyles' | abpLocalization }}</label>
            <textarea class="form-control" formControlName="cssStyles" rows="5"></textarea>
          </div>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">{{ '::Save' | abpLocalization }}</button>
          <a routerLink="/settings/print-formats" class="btn btn-secondary ms-2">{{ '::Cancel' | abpLocalization }}</a>
        </form>
      </div>
    </div>
  `
})
export class PrintFormatFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(PrintFormatService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      name: ['', Validators.required],
      documentType: ['SalesInvoice', Validators.required],
      isDefault: [false],
      htmlTemplate: ['', Validators.required],
      cssStyles: ['']
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
        this.router.navigate(['/settings/print-formats']);
      },
      error: (err) => this.toaster.error(err?.error?.error?.message || '::FailedToSavePrintFormat'),
    });
  }
}
