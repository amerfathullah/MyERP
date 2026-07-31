import { Component, Input, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { DocumentConnectionsService } from '../../../proxy/core/document-connections.service';

export interface ConnectionGroup {
  label: string;
  items: ConnectionItem[];
}

export interface ConnectionItem {
  documentType: string;
  count: number;
  route: string;
  documents: ConnectionDocument[];
}

export interface ConnectionDocument {
  id: string;
  documentNumber?: string;
  status?: string;
  amount?: number;
  date?: string;
  route: string;
}

@Component({
  selector: 'app-document-connections',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  template: `
    <div class="card mb-3">
      <div class="card-header py-2">
        <h6 class="card-title mb-0">
          <i class="fa fa-link me-2"></i>{{ '::Connections' | abpLocalization }}
        </h6>
      </div>
      <div class="card-body py-2">
        @if (loading()) {
          <div class="text-center py-2">
            <span class="spinner-border spinner-border-sm text-muted"></span>
          </div>
        } @else if (noConnections()) {
          <p class="text-muted mb-0 small">
            <i class="fa fa-info-circle me-1"></i>{{ '::NoLinkedDocuments' | abpLocalization }}
          </p>
        } @else {
          @for (group of groups(); track group.label) {
            <div class="mb-2">
              <small class="text-muted fw-semibold text-uppercase">{{ group.label }}</small>
              <div class="mt-1">
                @for (item of group.items; track item.documentType) {
                  <div class="mb-1">
                    <span class="badge bg-light text-dark border me-1">
                      {{ item.documentType }}
                      <span class="badge bg-primary ms-1">{{ item.count }}</span>
                    </span>
                    <div class="ps-2 mt-1">
                      @for (doc of item.documents; track doc.id) {
                        <div class="d-flex align-items-center gap-2 py-1 border-bottom">
                          <a [routerLink]="doc.route" class="text-decoration-none fw-medium small">
                            {{ doc.documentNumber || doc.id.substring(0, 8) }}
                          </a>
                          @if (doc.status) {
                            <span class="badge rounded-pill"
                                  [class]="getStatusClass(doc.status)">
                              {{ doc.status }}
                            </span>
                          }
                          @if (doc.amount) {
                            <span class="text-muted small ms-auto">
                              {{ doc.amount | number:'1.2-2' }}
                            </span>
                          }
                          @if (doc.date) {
                            <span class="text-muted small">
                              {{ doc.date | date:'dd/MM/yyyy' }}
                            </span>
                          }
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .card-header { background: transparent; }
    .border-bottom:last-child { border-bottom: none !important; }
  `]
})
export class DocumentConnectionsComponent implements OnInit {
  @Input() documentType!: string;
  @Input() documentId!: string;

  private connectionsService = inject(DocumentConnectionsService);

  groups = signal<ConnectionGroup[]>([]);
  loading = signal(false);
  noConnections = computed(() =>
    !this.loading() && this.groups().every(g => g.items.length === 0)
  );

  ngOnInit(): void {
    if (this.documentType && this.documentId) {
      this.loadConnections();
    }
  }

  loadConnections(): void {
    this.loading.set(true);
    this.connectionsService
      .getConnections(this.documentType, this.documentId)
      .subscribe({
        next: (result) => {
          this.groups.set(result.groups || []);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  getStatusClass(status: string): string {
    const s = status?.toLowerCase();
    if (s === 'posted' || s === 'completed' || s === 'fullyrepaid') return 'bg-success-subtle text-success';
    if (s === 'submitted' || s === 'todeliverandbill') return 'bg-info-subtle text-info';
    if (s === 'cancelled') return 'bg-danger-subtle text-danger';
    if (s === 'draft') return 'bg-secondary-subtle text-secondary';
    return 'bg-light text-dark';
  }
}
