import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { VideoService } from '../../proxy/utilities/video.service';
import { VideoDto } from '../../proxy/utilities/models';
import { VideoProvider } from '../../proxy/utilities/video-provider.enum';

@Component({
  selector: 'app-video-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Videos</h5>
        <div class="d-flex gap-2">
          <a routerLink="/utilities/settings" class="btn btn-outline-secondary btn-sm">Settings</a>
          <a routerLink="/utilities/videos/new" class="btn btn-primary btn-sm">New Video</a>
        </div>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by title, url, or description..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
          <div class="col-md-3">
            <select class="form-select form-select-sm" [(ngModel)]="selectedProvider" (ngModelChange)="load()">
              <option [ngValue]="null">-- All Providers --</option>
              <option [ngValue]="0">YouTube</option>
              <option [ngValue]="1">Vimeo</option>
              <option [ngValue]="2">Custom</option>
            </select>
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Title</th>
              <th>Provider</th>
              <th>Duration</th>
              <th class="text-center">Views</th>
              <th class="text-center">Likes</th>
              <th class="text-center" style="width: 100px;">Status</th>
              <th class="text-end" style="width: 160px;">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/utilities/videos', item.id, 'edit']" class="fw-semibold">
                    {{ item.title }}
                  </a>
                  <div class="small text-muted text-truncate" style="max-width: 350px;">{{ item.url }}</div>
                </td>
                <td>
                  <span class="badge" [ngClass]="getProviderBadge(item.provider)">
                    {{ getProviderName(item.provider) }}
                  </span>
                </td>
                <td>{{ item.durationSeconds ? (item.durationSeconds + 's') : '—' }}</td>
                <td class="text-center">{{ item.viewCount | number }}</td>
                <td class="text-center">{{ item.likeCount | number }}</td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ item.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/utilities/videos', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="7" class="text-center text-muted py-4">No videos found.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class VideoListComponent implements OnInit {
  private service = inject(VideoService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: VideoDto[] = [];
  filter = '';
  selectedProvider: VideoProvider | null = null;

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({
      filter: this.filter,
      provider: this.selectedProvider,
      maxResultCount: 200,
      skipCount: 0
    } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: VideoDto): void {
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

  getProviderName(provider: VideoProvider): string {
    switch (provider) {
      case VideoProvider.YouTube: return 'YouTube';
      case VideoProvider.Vimeo: return 'Vimeo';
      case VideoProvider.Custom: return 'Custom';
      default: return 'Unknown';
    }
  }

  getProviderBadge(provider: VideoProvider): string {
    switch (provider) {
      case VideoProvider.YouTube: return 'bg-danger';
      case VideoProvider.Vimeo: return 'bg-info text-dark';
      case VideoProvider.Custom: return 'bg-secondary';
      default: return 'bg-light text-dark';
    }
  }
}
