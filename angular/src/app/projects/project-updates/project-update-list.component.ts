import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { ProjectUpdateService } from '../../proxy/projects/project-update.service';
import { ProjectUpdateDto } from '../../proxy/projects/models';

@Component({
  selector: 'app-project-update-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::ProjectUpdates' | abpLocalization }}</h5>
        <a routerLink="/projects/project-updates/new" class="btn btn-primary btn-sm">{{ '::NewUpdate' | abpLocalization }}</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" [placeholder]="'::Placeholder:FilterByProjectOrSummary' | abpLocalization" [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>{{ '::Project' | abpLocalization }}</th>
              <th>{{ '::Date' | abpLocalization }}</th>
              <th>{{ '::Time' | abpLocalization }}</th>
              <th class="text-end">{{ '::Progress' | abpLocalization }}</th>
              <th>{{ '::Summary' | abpLocalization }}</th>
              <th class="text-center">{{ '::Status' | abpLocalization }}</th>
              <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/projects/project-updates', item.id, 'edit']" class="fw-semibold">
                    {{ item.projectNumber }} - {{ item.projectName }}
                  </a>
                </td>
                <td>{{ item.date | date:'yyyy-MM-dd' }}</td>
                <td>{{ item.time || '—' }}</td>
                <td class="text-end">
                  <span class="badge bg-info text-dark">{{ item.percentComplete }}%</span>
                </td>
                <td>{{ item.summary || '—' }}</td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.sent ? 'bg-success' : 'bg-secondary'">
                    {{ (item.sent ? '::Sent' : '::Draft') | abpLocalization }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/projects/project-updates', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">{{ '::Edit' | abpLocalization }}</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">{{ '::Delete' | abpLocalization }}</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="7" class="text-center text-muted py-4">{{ '::NoProjectUpdatesFound' | abpLocalization }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class ProjectUpdateListComponent implements OnInit {
  private service = inject(ProjectUpdateService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: ProjectUpdateDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: ProjectUpdateDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(item.id!).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyDeleted');
          this.load();
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
      });
    });
  }
}
