import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProjectUpdateService } from '../../proxy/projects/project-update.service';
import { ProjectService } from '../../proxy/projects/project.service';

@Component({
  selector: 'app-project-update-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Project Update</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Project *</label>
              <select class="form-select" formControlName="projectId" (change)="onProjectSelected()">
                <option [ngValue]="null">-- Select Project --</option>
                @for (p of projectsList; track p.id) {
                  <option [value]="p.id">{{ p.projectNumber }} - {{ p.projectName }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">Date *</label>
              <input type="date" class="form-control" formControlName="date">
            </div>
            <div class="col-md-3">
              <label class="form-label">Percent Complete (%)</label>
              <input type="number" step="0.1" class="form-control" formControlName="percentComplete" placeholder="0 - 100">
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Summary</label>
            <input type="text" class="form-control" formControlName="summary" placeholder="Brief summary of progress / checkpoint...">
          </div>

          <div class="mb-3">
            <label class="form-label">Notes & Details</label>
            <textarea class="form-control" rows="5" formControlName="notes" placeholder="Detailed update notes, blockers, milestones achieved, next steps..."></textarea>
          </div>

          <div class="row mb-4">
            <div class="col-md-4">
              <div class="form-check form-switch mt-2">
                <input class="form-check-input" type="checkbox" id="sent" formControlName="sent">
                <label class="form-check-label" for="sent">Mark as Sent / Published</label>
              </div>
            </div>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/projects/project-updates" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class ProjectUpdateFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ProjectUpdateService);
  private projectService = inject(ProjectService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;
  projectsList: any[] = [];

  constructor() {
    const today = new Date().toISOString().substring(0, 10);
    this.form = this.fb.group({
      projectId: [null, Validators.required],
      date: [today, Validators.required],
      percentComplete: [0, [Validators.min(0), Validators.max(100)]],
      summary: ['', Validators.maxLength(500)],
      notes: ['', Validators.maxLength(4000)],
      sent: [false],
    });
  }

  ngOnInit() {
    this.projectService.getList({ maxResultCount: 1000 } as any).subscribe(res => {
      this.projectsList = res.items ?? [];
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        const dateStr = res.date ? res.date.substring(0, 10) : '';
        this.form.patchValue({
          ...res,
          date: dateStr,
        });
      });
    }
  }

  onProjectSelected() {
    const projectId = this.form.get('projectId')?.value;
    if (projectId) {
      const selectedProject = this.projectsList.find(p => p.id === projectId);
      if (selectedProject && selectedProject.percentComplete != null) {
        this.form.patchValue({ percentComplete: selectedProject.percentComplete });
      }
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
        this.router.navigate(['/projects/project-updates']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
