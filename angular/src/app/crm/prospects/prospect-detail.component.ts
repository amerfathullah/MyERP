import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  standalone: true,
  selector: 'app-prospect-detail',
  imports: [CommonModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    @if (loading()) {
      <div class="d-flex justify-content-center p-5"><div class="spinner-border text-primary"></div></div>
    } @else if (prospect()) {
      <div class="row mb-3">
        <div class="col"><h4 class="mb-0">{{ prospect().prospectName || prospect().name }}</h4></div>
        <div class="col-auto">
          <app-status-badge [status]="prospect().status || 'Open'" />
        </div>
      </div>

      <div class="row mb-4 g-3">
        <div class="col-md-4">
          <div class="card">
            <div class="card-header"><h6 class="mb-0">{{ '::ProspectDetails' | abpLocalization }}</h6></div>
            <div class="card-body">
              <dl class="row mb-0">
                <dt class="col-sm-5">{{ '::Source' | abpLocalization }}</dt>
                <dd class="col-sm-7">{{ prospect().source || '—' }}</dd>
                <dt class="col-sm-5">{{ '::Notes' | abpLocalization }}</dt>
                <dd class="col-sm-7">{{ prospect().notes || '—' }}</dd>
              </dl>
            </div>
          </div>
        </div>
        <div class="col-md-8">
          <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h6 class="mb-0">{{ '::LinkedLeads' | abpLocalization }}</h6>
              <span class="badge bg-primary">{{ prospect().leads?.length || 0 }}</span>
            </div>
            <div class="card-body p-0">
              @if (prospect().leads?.length > 0) {
                <table class="table table-sm mb-0">
                  <thead><tr><th>{{ '::Name' | abpLocalization }}</th><th>{{ '::Email' | abpLocalization }}</th><th>{{ '::Status' | abpLocalization }}</th></tr></thead>
                  <tbody>
                    @for (lead of prospect().leads; track $index) {
                      <tr>
                        <td>{{ lead.leadName || lead.name }}</td>
                        <td>{{ lead.email || '—' }}</td>
                        <td><app-status-badge [status]="lead.status || '—'" /></td>
                      </tr>
                    }
                  </tbody>
                </table>
              } @else {
                <p class="text-muted text-center py-3 mb-0">{{ '::NoLeadsLinked' | abpLocalization }}</p>
              }
            </div>
          </div>
        </div>
      </div>

      <app-activity-log documentType="Prospect" [documentId]="prospect().id" />
    }
  `,
})
export class ProspectDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);

  prospect = signal<any>(null);
  loading = signal(true);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.router.navigate(['/crm/prospects']); return; }
    this.http.get<any>(`/api/app/prospect/${id}`).subscribe({
      next: (p) => { this.prospect.set(p); this.loading.set(false); },
      error: () => { this.router.navigate(['/crm/prospects']); },
    });
  }
}
