import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { BomDto } from '../../proxy/manufacturing/models';

@Component({
  selector: 'app-bom-update-tool',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::BomUpdateTool' | abpLocalization">
      <div class="card mb-4">
        <div class="card-body">
          <p class="text-muted">{{ '::BomUpdateToolDescription' | abpLocalization }}</p>

          <form [formGroup]="form" (ngSubmit)="submit()">
            <div class="row">
              <div class="col-12 col-md-6 mb-3">
                <label class="form-label">{{ '::CurrentBom' | abpLocalization }} <span class="text-danger">*</span></label>
                <select class="form-select" formControlName="currentBomId">
                  <option value="">{{ '::SelectBom' | abpLocalization }}</option>
                  @for (b of boms(); track b.id) {
                    <option [value]="b.id">{{ b.bomNumber }} — {{ b.itemName }}</option>
                  }
                </select>
              </div>
              <div class="col-12 col-md-6 mb-3">
                <label class="form-label">{{ '::NewBom' | abpLocalization }} <span class="text-danger">*</span></label>
                <select class="form-select" formControlName="newBomId">
                  <option value="">{{ '::SelectBom' | abpLocalization }}</option>
                  @for (b of boms(); track b.id) {
                    <option [value]="b.id">{{ b.bomNumber }} — {{ b.itemName }}</option>
                  }
                </select>
              </div>
            </div>

            <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSubmitting()">
              @if (isSubmitting()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              <i class="fa fa-arrows-rotate me-1"></i>{{ '::ReplaceBom' | abpLocalization }}
            </button>
          </form>
        </div>
      </div>

      @if (result()) {
        <div class="alert alert-success">
          {{ successMessage() }}
        </div>
      }
    </abp-page>
  `,
})
export class BomUpdateToolComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ManufacturingService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  boms = signal<BomDto[]>([]);
  isSubmitting = signal(false);
  result = signal<{ updatedBomItemCount: number; recostedBomCount: number } | null>(null);

  form = this.fb.group({
    currentBomId: ['', Validators.required],
    newBomId: ['', Validators.required],
  });

  successMessage() {
    const r = this.result();
    if (!r) return '';
    return this.l.instant('::BomReplaceSuccess', String(r.updatedBomItemCount), String(r.recostedBomCount));
  }

  ngOnInit(): void {
    const companyId = this.companyContext.currentCompanyId();
    this.service.getBomList({ skipCount: 0, maxResultCount: 500, sorting: 'bomNumber', companyId } as any)
      .subscribe(res => this.boms.set(res.items ?? []));
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const { currentBomId, newBomId } = this.form.getRawValue();

    this.isSubmitting.set(true);
    this.result.set(null);
    this.service.replaceBom({ currentBomId: currentBomId!, newBomId: newBomId! }).subscribe({
      next: (res) => {
        this.isSubmitting.set(false);
        this.result.set({ updatedBomItemCount: res.updatedBomItemCount ?? 0, recostedBomCount: res.recostedBomCount ?? 0 });
        this.toaster.success('::SuccessfullySaved');
        this.form.reset({ currentBomId: '', newBomId: '' });
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }
}
