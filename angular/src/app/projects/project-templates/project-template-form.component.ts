import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProjectTemplateService } from '../../proxy/projects/project-template.service';

interface TaskRow {
  key: string;
  subject: string;
  taskWeight: number;
  expectedHours: number;
  isMilestone: boolean;
  dependsOnKeys: string[];
}

function newKey(): string {
  return crypto.randomUUID();
}

@Component({
  selector: 'app-project-template-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'Edit' : 'NewProjectTemplate') | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'TemplateName' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.templateName" />
          </div>
          <div class="col-md-6 d-flex align-items-end">
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="disabled" [(ngModel)]="form.disabled" />
              <label class="form-check-label" for="disabled">{{ 'Disabled' | abpLocalization }}</label>
            </div>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Tasks' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr>
            <th style="min-width:200px">{{ 'TaskSubject' | abpLocalization }}</th>
            <th style="width:100px">{{ 'TaskWeight' | abpLocalization }}</th>
            <th style="width:120px">{{ 'ExpectedHours' | abpLocalization }}</th>
            <th style="width:90px" class="text-center">{{ 'IsMilestone' | abpLocalization }}</th>
            <th style="min-width:220px">{{ 'DependsOn' | abpLocalization }}</th>
            <th></th>
          </tr></thead>
          <tbody>
            @for (row of form.tasks; track row.key) {
              <tr>
                <td><input class="form-control form-control-sm" [(ngModel)]="row.subject" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="row.taskWeight" min="0" step="0.1" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="row.expectedHours" min="0" step="0.5" /></td>
                <td class="text-center"><input type="checkbox" [(ngModel)]="row.isMilestone" /></td>
                <td>
                  <div class="d-flex flex-wrap gap-1">
                    @for (depKey of row.dependsOnKeys; track depKey) {
                      <span class="badge bg-light text-dark border d-flex align-items-center gap-1">
                        {{ subjectFor(depKey) }}
                        <i class="fa fa-times cursor-pointer" (click)="removeDependency(row, depKey)"></i>
                      </span>
                    }
                  </div>
                  <select class="form-select form-select-sm mt-1" (change)="addDependency(row, $any($event.target).value); $any($event.target).value=''">
                    <option value="">-- {{ 'AddItem' | abpLocalization }} --</option>
                    @for (other of otherTasks(row); track other.key) { <option [value]="other.key">{{ other.subject }}</option> }
                  </select>
                </td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="removeTask(row.key)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addTask()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/projects/templates">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving() || !form.templateName"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class ProjectTemplateFormComponent implements OnInit {
  private service = inject(ProjectTemplateService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  saving = signal(false);
  isEdit = signal(false);
  private templateId: string | null = null;

  form: { templateName: string; disabled: boolean; tasks: TaskRow[] } = {
    templateName: '', disabled: false, tasks: [],
  };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.templateId = id;
      this.service.get(id).subscribe(t => {
        this.form = {
          templateName: t.templateName!, disabled: !!t.disabled,
          tasks: (t.tasks ?? []).map(row => ({
            key: row.key, subject: row.subject, taskWeight: row.taskWeight ?? 1,
            expectedHours: row.expectedHours ?? 0, isMilestone: !!row.isMilestone,
            dependsOnKeys: row.dependsOnKeys ?? [],
          })),
        };
      });
    }
  }

  subjectFor(key: string): string {
    return this.form.tasks.find(t => t.key === key)?.subject ?? '—';
  }

  otherTasks(row: TaskRow): TaskRow[] {
    return this.form.tasks.filter(t => t.key !== row.key && !row.dependsOnKeys.includes(t.key));
  }

  addTask(): void {
    this.form.tasks.push({ key: newKey(), subject: '', taskWeight: 1, expectedHours: 0, isMilestone: false, dependsOnKeys: [] });
  }

  removeTask(key: string): void {
    this.form.tasks = this.form.tasks.filter(t => t.key !== key);
    for (const t of this.form.tasks) {
      t.dependsOnKeys = t.dependsOnKeys.filter(k => k !== key);
    }
  }

  addDependency(row: TaskRow, key: string): void {
    if (!key || row.dependsOnKeys.includes(key)) return;
    row.dependsOnKeys = [...row.dependsOnKeys, key];
  }

  removeDependency(row: TaskRow, key: string): void {
    row.dependsOnKeys = row.dependsOnKeys.filter(k => k !== key);
  }

  save(): void {
    this.saving.set(true);
    const dto = {
      templateName: this.form.templateName,
      disabled: this.form.disabled,
      tasks: this.form.tasks.filter(t => t.subject).map(t => ({
        key: t.key, subject: t.subject, taskWeight: t.taskWeight, expectedHours: t.expectedHours,
        isMilestone: t.isMilestone, dependsOnKeys: t.dependsOnKeys,
      })),
    };
    const req = this.templateId ? this.service.update(this.templateId, dto) : this.service.create(dto);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.toaster.success(this.templateId ? '::SuccessfullyUpdated' : '::SuccessfullySaved');
        this.router.navigate(['/projects/templates']);
      },
      error: () => this.saving.set(false),
    });
  }
}
