import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { InstallationNoteService } from '../../proxy/sales/installation-note.service';

const STATUS = ['Draft', 'Submitted', 'Cancelled'] as const;

@Component({
  selector: 'app-installation-note-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, StatusBadgeComponent, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    @if (isLoading) { <app-loading-overlay /> }
    @if (!isLoading && note) {
      <abp-page [title]="note.installationNumber ?? 'Installation Note'">
        <div class="d-flex justify-content-between align-items-center mb-4">
          <app-status-badge [status]="STATUS[note.status ?? 0]" />
          <div class="btn-group btn-group-sm">
            @if (note.status === 0) {
              <button class="btn btn-outline-success" (click)="submit()"><i class="fa fa-check me-1"></i>{{ 'Submit' | abpLocalization }}</button>
              <button class="btn btn-outline-danger" (click)="cancel()"><i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
            }
            @if (note.status === 1) {
              <button class="btn btn-outline-danger" (click)="cancel()"><i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
            }
          </div>
        </div>

        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-4"><strong>{{ 'Customer' | abpLocalization }}:</strong> {{ note.customerId ?? '—' }}</div>
            <div class="col-md-4"><strong>Installation Date:</strong> {{ note.installationDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-4"><strong>Delivery Note:</strong> {{ note.deliveryNoteId ?? '—' }}</div>
          </div>
          @if (note.remarks) {
            <div class="mt-2"><strong>{{ 'Remarks' | abpLocalization }}:</strong> {{ note.remarks }}</div>
          }
        </div></div>

        @if (note.items && note.items.length > 0) {
          <div class="card"><div class="card-body">
            <h6 class="card-title">Installed Items</h6>
            <table class="table table-sm mb-0">
              <thead><tr>
                <th>{{ 'Item' | abpLocalization }}</th>
                <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                <th>Serial No</th>
              </tr></thead>
              <tbody>
                @for (item of note.items; let i = $index; track i) {
                  <tr>
                    <td>{{ item.itemId ?? '—' }}</td>
                    <td class="text-end">{{ item.quantity }}</td>
                    <td>{{ item.serialNo ?? '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div></div>
        }
      </abp-page>
    }
  `
})
export class InstallationNoteDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(InstallationNoteService);
  private toaster = inject(ToasterService);

  note: any = null;
  isLoading = false;
  STATUS = STATUS;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.load(id);
  }

  load(id: string) {
    this.isLoading = true;
    this.service.get(id).subscribe({
      next: n => { this.note = n; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  submit() {
    this.service.submit(this.note.id).subscribe({
      next: () => { this.toaster.success('Installation Note submitted'); this.load(this.note.id); },
      error: () => {}
    });
  }

  cancel() {
    if (confirm('Cancel this installation note?')) {
      this.service.cancel(this.note.id).subscribe({
        next: () => { this.toaster.success('Cancelled'); this.load(this.note.id); },
        error: () => {}
      });
    }
  }
}
