import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ProjectStore } from '../store/project.store';
import { CompanyService } from '../../proxy/core/company.service';
import type { CompanyDto } from '../../proxy/core/models';

@Component({
  selector: 'app-project-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './project-form.component.html',
  styleUrls: ['./project-form.component.scss'],
})
export class ProjectFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(ProjectStore);
  private companyService = inject(CompanyService);

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
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.store.create(this.form.getRawValue());
    this.router.navigate(['/projects']);
  }

  cancel(): void {
    this.router.navigate(['/projects']);
  }
}
