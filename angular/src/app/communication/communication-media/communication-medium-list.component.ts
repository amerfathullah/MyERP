import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { CommunicationMediumService } from '../../proxy/communication/communication-medium.service';
import { CommunicationMediumDto } from '../../proxy/communication/models';

@Component({
  selector: 'app-communication-medium-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Communication Media</h5>
        <a routerLink="/communication/communication-media/new" class="btn btn-primary btn-sm">New Medium</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by communication channel..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Type</th>
              <th>Channel</th>
              <th>Timeslots Count</th>
              <th class="text-center" style="width: 100px;">Status</th>
              <th class="text-end" style="width: 160px;">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/communication/communication-media', item.id, 'edit']" class="fw-semibold">
                    {{ getTypeName(item.communicationMediumType) }}
                  </a>
                </td>
                <td>{{ item.communicationChannel || '—' }}</td>
                <td>{{ item.timeslots?.length || 0 }} timeslot(s)</td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.isDisabled ? 'bg-secondary' : 'bg-success'">
                    {{ item.isDisabled ? 'Disabled' : 'Active' }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/communication/communication-media', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="text-center text-muted py-4">No communication media found.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class CommunicationMediumListComponent implements OnInit {
  private service = inject(CommunicationMediumService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: CommunicationMediumDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  getTypeName(type: number): string {
    switch (type) {
      case 0: return 'Voice';
      case 1: return 'Email';
      case 2: return 'Chat';
      default: return 'Unknown';
    }
  }

  delete(item: CommunicationMediumDto): void {
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
