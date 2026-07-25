import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ProjectStore } from '../store/project.store';
import { ProjectService } from '../../proxy/projects/project.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { CompanyDto } from '../../proxy/core/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-project-form',
  standalone: true,
  imports: [AutoValidationDirective, SaveShortcutDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './project-form.component.html',
  styleUrls: ['./project-form.component.scss'],
})
export class ProjectFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(ProjectStore);
  private service = inject(ProjectService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  form!: FormGroup;
  companies = signal<CompanyDto[]>([]);
  isEditMode = false;
  entityId: string | null = null;

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.form = this.fb.group({
      companyId: ['', Validators.required],
      projectName: ['', [Validators.required, Validators.maxLength(200)]],
      description: [''],
      status: [0],
      percentComplete: [0],
      startDate: [new Date().toISOString().split('T')[0]],
      endDate: [''],
      estimatedCost: [0],
      notes: [''],
    });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe((res) => this.companies.set(res.items ?? []));

    // Auto-fill companyId from context for new documents
    if (!this.isEditMode) {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const dto = this.form.getRawValue() as any;
    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe({
        next: () => this.router.navigate(['/projects', this.entityId]),
        error: () => {},
      });
    } else {
      this.service.create(dto).subscribe({
        next: () => this.router.navigate(['/projects']),
        error: () => {},
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/projects']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
