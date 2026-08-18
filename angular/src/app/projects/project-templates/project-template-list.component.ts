import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ProjectTemplateService } from '../../proxy/projects/project-template.service';
import type { ProjectTemplateDto } from '../../proxy/projects/models';

@Component({
  selector: 'app-project-template-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'ProjectTemplates' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/projects/templates/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewProjectTemplate' | abpLocalization }}
        </button>
      </div>

      @if (templates.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-diagram-project fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoProjectTemplatesYet' | abpLocalization }}</p>
        </div>
      } @else {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'TemplateName' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Tasks' | abpLocalization }}</th>
                  <th>{{ 'Disabled' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (t of templates; track t.id) {
                  <tr>
                    <td>{{ t.templateName }}</td>
                    <td class="text-end">{{ t.tasks?.length ?? 0 }}</td>
                    <td>
                      @if (t.disabled) {
                        <span class="badge bg-secondary-subtle text-secondary">{{ 'Disabled' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/projects/templates', t.id]">
                          <i class="fa fa-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="remove(t)"><i class="fa fa-trash"></i></button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class ProjectTemplateListComponent implements OnInit {
  private service = inject(ProjectTemplateService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  templates: ProjectTemplateDto[] = [];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.service.getList().subscribe(r => this.templates = r.items ?? []);
  }

  remove(t: ProjectTemplateDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(t.id!).subscribe(() => { this.toaster.success('::SuccessfullyDeleted'); this.load(); });
    });
  }
}
