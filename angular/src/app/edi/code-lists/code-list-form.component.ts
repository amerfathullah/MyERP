import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { CodeListService } from '../../proxy/edi/code-list.service';

@Component({
  selector: 'app-code-list-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} EDI Code List</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Title *</label>
              <input type="text" class="form-control" formControlName="title" placeholder="e.g. UN/EDIFACT 1001 Document Types">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Canonical URI</label>
              <input type="text" class="form-control" formControlName="canonicalUri" placeholder="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">
            </div>
          </div>

          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">URL</label>
              <input type="text" class="form-control" formControlName="url" placeholder="https://...">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Default Common Code</label>
              <input type="text" class="form-control" formControlName="defaultCommonCode" placeholder="Fallback code when none matches">
            </div>
          </div>

          <div class="row">
            <div class="col-md-4 mb-3">
              <label class="form-label">Version</label>
              <input type="text" class="form-control" formControlName="version" placeholder="e.g. D.16B or 2.1">
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">Publisher</label>
              <input type="text" class="form-control" formControlName="publisher" placeholder="e.g. UN/CEFACT or ISO">
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">Publisher ID</label>
              <input type="text" class="form-control" formControlName="publisherId" placeholder="e.g. UN/ECE">
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Description</label>
            <textarea class="form-control" rows="3" formControlName="description" placeholder="Description of the standard code list..."></textarea>
          </div>

          <div class="form-check form-switch mb-4">
            <input class="form-check-input" type="checkbox" id="isActive" formControlName="isActive">
            <label class="form-check-label" for="isActive">Active</label>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/edi/code-lists" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class CodeListFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(CodeListService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(300)]],
      canonicalUri: ['', Validators.maxLength(500)],
      url: ['', Validators.maxLength(1024)],
      defaultCommonCode: ['', Validators.maxLength(300)],
      version: ['', Validators.maxLength(50)],
      publisher: ['', Validators.maxLength(200)],
      publisherId: ['', Validators.maxLength(100)],
      description: ['', Validators.maxLength(2000)],
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
        this.router.navigate(['/edi/code-lists']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
