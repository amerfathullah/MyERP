import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { CodeListService } from '../../proxy/edi/code-list.service';
import { CodeListDto } from '../../proxy/edi/models';

@Component({
  selector: 'app-code-list-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::EdiCodeLists' | abpLocalization }}</h5>
        <a routerLink="/edi/code-lists/new" class="btn btn-primary btn-sm">{{ '::NewCodeList' | abpLocalization }}</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" [placeholder]="'::Placeholder:FilterByTitleUriOrDescription' | abpLocalization" [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>{{ '::Title' | abpLocalization }}</th>
              <th>{{ '::CanonicalUri' | abpLocalization }}</th>
              <th>{{ '::Publisher' | abpLocalization }}</th>
              <th>{{ '::Version' | abpLocalization }}</th>
              <th>{{ '::DefaultCode' | abpLocalization }}</th>
              <th class="text-center" style="width: 100px;">{{ '::Status' | abpLocalization }}</th>
              <th class="text-end" style="width: 160px;">{{ '::Actions' | abpLocalization }}</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/edi/code-lists', item.id, 'edit']" class="fw-semibold">
                    {{ item.title }}
                  </a>
                </td>
                <td class="text-truncate" style="max-width: 250px;">{{ item.canonicalUri || '—' }}</td>
                <td>{{ item.publisher || '—' }}</td>
                <td>{{ item.version || '—' }}</td>
                <td>
                  @if (item.id && defaultCodes[item.id] !== undefined) {
                    <span class="badge bg-info text-dark font-monospace">{{ defaultCodes[item.id] || '—' }}</span>
                  } @else {
                    <button type="button" class="btn btn-sm btn-outline-secondary" (click)="resolveDefaultCode(item)">{{ '::CheckDefault' | abpLocalization }}</button>
                  }
                </td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ (item.isActive ? '::Active' : '::Inactive') | abpLocalization }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/edi/code-lists', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">{{ '::Edit' | abpLocalization }}</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">{{ '::Delete' | abpLocalization }}</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="7" class="text-center text-muted py-4">{{ '::NoCodeListsFound' | abpLocalization }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class CodeListListComponent implements OnInit {
  private service = inject(CodeListService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: CodeListDto[] = [];
  defaultCodes: Record<string, string> = {};
  filter = '';

  ngOnInit() {
    this.load();
  }

  resolveDefaultCode(item: CodeListDto): void {
    const identifier = item.canonicalUri || item.title;
    if (!item.id || !identifier) return;
    this.service.getDefaultCode(identifier).subscribe({
      next: code => { this.defaultCodes[item.id!] = code ?? ''; },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: CodeListDto): void {
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
