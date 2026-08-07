import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { PrintFormatService } from '../../proxy/settings/print-format.service';

@Component({
  selector: 'app-print-format-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title">{{ isEdit ? 'Edit' : 'New' }} Print Format</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="mb-3">
            <label class="form-label">Name *</label>
            <input type="text" class="form-control" formControlName="name">
          </div>
          <div class="mb-3">
            <label class="form-label">Document Type *</label>
            <select class="form-select" formControlName="documentType">
              <option value="SalesInvoice">Sales Invoice</option>
              <option value="PurchaseOrder">Purchase Order</option>
              <option value="DeliveryNote">Delivery Note</option>
              <option value="PaymentEntry">Payment Entry</option>
            </select>
          </div>
          <div class="mb-3 form-check">
            <input type="checkbox" class="form-check-input" formControlName="isDefault" id="isDefault">
            <label class="form-check-label" for="isDefault">Is Default</label>
          </div>
          <div class="mb-3">
            <label class="form-label">HTML Template *</label>
            <textarea class="form-control" formControlName="htmlTemplate" rows="10"></textarea>
            <small class="form-text text-muted">Use Razor/Liquid syntax or pure HTML placeholders.</small>
          </div>
          <div class="mb-3">
            <label class="form-label">CSS Styles</label>
            <textarea class="form-control" formControlName="cssStyles" rows="5"></textarea>
          </div>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
          <a routerLink="/settings/print-formats" class="btn btn-secondary ms-2">Cancel</a>
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
    
    req.subscribe(() => {
      this.router.navigate(['/settings/print-formats']);
    });
  }
}
