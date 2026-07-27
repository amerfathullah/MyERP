import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe , LocalizationService } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { Confirmation, ToasterService , ConfirmationService } from '@abp/ng.theme.shared';

@Component({
  standalone: true,
  selector: 'app-leave-type-list',
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-calendar-minus me-2"></i>{{ '::LeaveTypes' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showForm = !showForm">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>
        <div class="card-body">
          @if (showForm) {
            <div class="border rounded p-3 mb-3 bg-light">
              <div class="row g-2">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Name' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" [(ngModel)]="newItem.name" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::MaxDays' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" type="number" [(ngModel)]="newItem.maxLeavesAllowed" />
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::IsPaid' | abpLocalization }}</label>
                  <div><input type="checkbox" [(ngModel)]="newItem.isPaidLeave" /></div>
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::CarryForward' | abpLocalization }}</label>
                  <div><input type="checkbox" [(ngModel)]="newItem.allowCarryForward" /></div>
                </div>
                <div class="col-md-1 d-flex align-items-end">
                  <button class="btn btn-primary btn-sm" (click)="save()"><i class="fas fa-save"></i></button>
                </div>
              </div>
            </div>
          }
          @if (items().length === 0) {
            <div class="text-center text-muted py-4">
              <i class="fas fa-calendar-minus fa-2x mb-2"></i>
              <p>{{ '::NoLeaveTypesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead><tr>
                <th>{{ '::Name' | abpLocalization }}</th>
                <th>{{ '::MaxDays' | abpLocalization }}</th>
                <th>{{ '::IsPaid' | abpLocalization }}</th>
                <th>{{ '::CarryForward' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr>
                    <td>{{ item.name }}</td>
                    <td>{{ item.maxLeavesAllowed }}</td>
                    <td><i [class]="item.isPaidLeave ? 'fas fa-check text-success' : 'fas fa-times text-muted'"></i></td>
                    <td><i [class]="item.allowCarryForward ? 'fas fa-check text-success' : 'fas fa-times text-muted'"></i></td>
                    <td><button class="btn btn-outline-danger btn-sm" (click)="remove(item.id)"><i class="fas fa-trash"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  `
})
export class LeaveTypeListComponent implements OnInit {
  private http = inject(HttpClient);
  private localization = inject(LocalizationService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items = signal<any[]>([]);
  showForm = false;
  newItem: any = { name: '', maxLeavesAllowed: 12, isPaidLeave: true, allowCarryForward: false };

  l(key: string) { return this.localization.instant(key); }

  ngOnInit() { this.load(); }

  load() {
    this.http.get<any>('/api/app/leave-type').subscribe({ next: res => this.items.set(res.items ?? []), error: () => {} });
  }

  save() {
    if (!this.newItem.name) return;
    this.http.post('/api/app/leave-type', this.newItem).subscribe({
      next: () => { this.toaster.success('::SuccessfullyCreated'); this.showForm = false; this.newItem = { name: '', maxLeavesAllowed: 12, isPaidLeave: true, allowCarryForward: false }; this.load(); },
      error: () => {}
    });
  }

  remove(id: string) {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.http.delete(`/api/app/leave-type/${id}`).subscribe({
        next: () => { this.toaster.success(this.l('::SuccessfullyDeleted')); this.load(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Delete failed'),
      });
    });
  }
}
