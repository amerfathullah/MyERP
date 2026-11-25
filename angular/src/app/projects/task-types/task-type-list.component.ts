import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { TaskTypeService } from '../../proxy/projects/task-type.service';
import { TaskTypeDto } from '../../proxy/projects/models';

@Component({
  selector: 'app-task-type-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::TaskTypes' | abpLocalization }}</h5>
        <a routerLink="/projects/task-types/new" class="btn btn-primary btn-sm">{{ '::NewTaskType' | abpLocalization }}</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" [placeholder]="'::Placeholder:FilterByName' | abpLocalization" [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>{{ '::Name' | abpLocalization }}</th>
              <th class="text-end">{{ '::Weight' | abpLocalization }}</th>
              <th>{{ '::Description' | abpLocalization }}</th>
              <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/projects/task-types', item.id, 'edit']" class="fw-semibold">
                    {{ item.name }}
                  </a>
                </td>
                <td class="text-end">{{ item.weight | number:'1.2-2' }}</td>
                <td>{{ item.description || '—' }}</td>
                <td class="text-end">
                  <a [routerLink]="['/projects/task-types', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">{{ '::Edit' | abpLocalization }}</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">{{ '::Delete' | abpLocalization }}</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="text-center text-muted py-4">{{ '::NoTaskTypesFound' | abpLocalization }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class TaskTypeListComponent implements OnInit {
  private service = inject(TaskTypeService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: TaskTypeDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: TaskTypeDto): void {
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
