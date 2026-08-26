import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { CommonCodeService } from '../../proxy/edi/common-code.service';
import { CodeListService } from '../../proxy/edi/code-list.service';
import { CodeListDto } from '../../proxy/edi/models';

@Component({
  selector: 'app-common-code-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} EDI Common Code</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Code List *</label>
              <select class="form-select" formControlName="codeListId">
                <option [ngValue]="null">-- Select Code List --</option>
                @for (cl of codeLists; track cl.id) {
                  <option [ngValue]="cl.id">{{ cl.title }}</option>
                }
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Code *</label>
              <input type="text" class="form-control" formControlName="code" placeholder="e.g. 380 or MY or MYR">
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Title *</label>
            <input type="text" class="form-control" formControlName="title" placeholder="e.g. Commercial Invoice">
          </div>

          <div class="mb-3">
            <label class="form-label">Description</label>
            <textarea class="form-control" rows="3" formControlName="description" placeholder="Description of this code value..."></textarea>
          </div>

          <div class="mb-3">
            <label class="form-label">Additional Data (JSON)</label>
            <textarea class="form-control font-monospace" rows="4" formControlName="additionalDataJson" placeholder="Optional JSON metadata..."></textarea>
          </div>

          <div class="form-check form-switch mb-4">
            <input class="form-check-input" type="checkbox" id="isActive" formControlName="isActive">
            <label class="form-check-label" for="isActive">Active</label>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/edi/common-codes" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class CommonCodeFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(CommonCodeService);
  private codeListService = inject(CodeListService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;
  codeLists: CodeListDto[] = [];

  constructor() {
    this.form = this.fb.group({
      codeListId: [null, Validators.required],
      code: ['', [Validators.required, Validators.maxLength(300)]],
      title: ['', [Validators.required, Validators.maxLength(300)]],
      description: ['', Validators.maxLength(2000)],
      additionalDataJson: [''],
      isActive: [true],
    });
  }

  ngOnInit() {
    this.codeListService.getList({ maxResultCount: 200 } as any).subscribe(res => {
      this.codeLists = res.items ?? [];
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue({
          codeListId: res.codeListId,
          code: res.code,
          title: res.title,
          description: res.description,
          additionalDataJson: res.additionalDataJson,
          isActive: res.isActive,
        });
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
        this.router.navigate(['/edi/common-codes']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
