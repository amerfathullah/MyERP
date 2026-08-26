import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { ChequePrintTemplateService } from '../../proxy/accounting/cheque-print-template.service';
import { ChequePrintTemplateDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-cheque-print-template-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Cheque Print Templates</h5>
        <a routerLink="/accounting/cheque-print-templates/new" class="btn btn-primary btn-sm">New Template</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by bank name..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Bank Name</th>
              <th>Cheque Size</th>
              <th>Dimensions (W × H cm)</th>
              <th>Acc. Payee Badge</th>
              <th class="text-end" style="width: 160px;">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/accounting/cheque-print-templates', item.id, 'edit']" class="fw-semibold">
                    {{ item.bankName }}
                  </a>
                </td>
                <td>{{ item.chequeSize === 1 ? 'A4' : 'Regular' }}</td>
                <td>{{ item.chequeWidth }} × {{ item.chequeHeight }} cm</td>
                <td>
                  @if (item.isAccountPayable) {
                    <span class="badge bg-info text-dark">{{ item.messageToShow || 'Acc. Payee' }}</span>
                  } @else {
                    <span class="badge bg-secondary">No</span>
                  }
                </td>
                <td class="text-end">
                  <a [routerLink]="['/accounting/cheque-print-templates', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="text-center text-muted py-4">No cheque print templates found.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class ChequePrintTemplateListComponent implements OnInit {
  private service = inject(ChequePrintTemplateService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: ChequePrintTemplateDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: ChequePrintTemplateDto): void {
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
