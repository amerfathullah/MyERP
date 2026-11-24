import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { CommonCodeService } from '../../proxy/edi/common-code.service';
import { CodeListService } from '../../proxy/edi/code-list.service';
import { CommonCodeDto, CodeListDto } from '../../proxy/edi/models';

@Component({
  selector: 'app-common-code-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::EdiCommonCodes' | abpLocalization }}</h5>
        <a routerLink="/edi/common-codes/new" class="btn btn-primary btn-sm">{{ '::NewCommonCode' | abpLocalization }}</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" [placeholder]="'::Placeholder:FilterByCodeTitleOrDescription' | abpLocalization" [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
          <div class="col-md-4">
            <select class="form-select form-select-sm" [(ngModel)]="selectedCodeListId" (ngModelChange)="load()">
              <option [ngValue]="null">{{ '::AllCodeLists' | abpLocalization }}</option>
              @for (cl of codeLists; track cl.id) {
                <option [ngValue]="cl.id">{{ cl.title }}</option>
              }
            </select>
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th style="width: 150px;">{{ '::Code' | abpLocalization }}</th>
              <th>{{ '::Title' | abpLocalization }}</th>
              <th>{{ '::Description' | abpLocalization }}</th>
              <th class="text-center" style="width: 100px;">{{ '::Status' | abpLocalization }}</th>
              <th class="text-end" style="width: 160px;">{{ '::Actions' | abpLocalization }}</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/edi/common-codes', item.id, 'edit']" class="fw-semibold">
                    {{ item.code }}
                  </a>
                </td>
                <td>{{ item.title }}</td>
                <td>{{ item.description || '—' }}</td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ (item.isActive ? '::Active' : '::Inactive') | abpLocalization }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/edi/common-codes', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">{{ '::Edit' | abpLocalization }}</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">{{ '::Delete' | abpLocalization }}</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="text-center text-muted py-4">{{ '::NoCommonCodesFound' | abpLocalization }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class CommonCodeListComponent implements OnInit {
  private service = inject(CommonCodeService);
  private codeListService = inject(CodeListService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: CommonCodeDto[] = [];
  codeLists: CodeListDto[] = [];
  selectedCodeListId: string | null = null;
  filter = '';

  ngOnInit() {
    this.codeListService.getList({ maxResultCount: 200 } as any).subscribe(res => {
      this.codeLists = res.items ?? [];
    });
    this.load();
  }

  load(): void {
    this.service.getList({
      codeListId: this.selectedCodeListId,
      filter: this.filter,
      maxResultCount: 200,
      skipCount: 0
    } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: CommonCodeDto): void {
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
