import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { VideoService } from '../../proxy/utilities/video.service';
import { VideoDto } from '../../proxy/utilities/models';
import { VideoProvider } from '../../proxy/utilities/video-provider.enum';

@Component({
  selector: 'app-video-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::Videos' | abpLocalization }}</h5>
        <div class="d-flex gap-2">
          <a routerLink="/utilities/settings" class="btn btn-outline-secondary btn-sm">{{ '::Settings' | abpLocalization }}</a>
          <a routerLink="/utilities/videos/new" class="btn btn-primary btn-sm">{{ '::NewVideo' | abpLocalization }}</a>
        </div>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" [placeholder]="'::Placeholder:FilterByTitleUrlOrDescription' | abpLocalization" [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
          <div class="col-md-3">
            <select class="form-select form-select-sm" [(ngModel)]="selectedProvider" (ngModelChange)="load()">
              <option [ngValue]="null">{{ '::AllProviders' | abpLocalization }}</option>
              <option [ngValue]="0">{{ '::YouTube' | abpLocalization }}</option>
              <option [ngValue]="1">{{ '::Vimeo' | abpLocalization }}</option>
              <option [ngValue]="2">{{ '::Custom' | abpLocalization }}</option>
            </select>
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>{{ '::Title' | abpLocalization }}</th>
              <th>{{ '::Provider' | abpLocalization }}</th>
              <th>{{ '::Duration' | abpLocalization }}</th>
              <th class="text-center">{{ '::Views' | abpLocalization }}</th>
              <th class="text-center">{{ '::Likes' | abpLocalization }}</th>
              <th class="text-center" style="width: 100px;">{{ '::Status' | abpLocalization }}</th>
              <th class="text-end" style="width: 160px;">{{ '::Actions' | abpLocalization }}</th>
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
                    {{ getProviderKey(item.provider) | abpLocalization }}
                  </span>
                </td>
                <td>{{ item.durationSeconds ? (item.durationSeconds + 's') : '—' }}</td>
                <td class="text-center">{{ item.viewCount | number }}</td>
                <td class="text-center">{{ item.likeCount | number }}</td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ (item.isActive ? '::Active' : '::Inactive') | abpLocalization }}
                  </span>
                </td>
                <td class="text-end text-nowrap">
                  <button type="button" class="btn btn-sm btn-outline-success me-2" (click)="watchVideo(item)">{{ '::Watch' | abpLocalization }}</button>
                  <button type="button" class="btn btn-sm btn-outline-info me-2" (click)="openStatsEditor(item)">{{ '::Stats' | abpLocalization }}</button>
                  <a [routerLink]="['/utilities/videos', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">{{ '::Edit' | abpLocalization }}</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">{{ '::Delete' | abpLocalization }}</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="7" class="text-center text-muted py-4">{{ '::NoVideosFound' | abpLocalization }}</td>
              </tr>
            }
          </tbody>
        </table>
        @if (editingStatsVideo; as vid) {
          <div class="card mt-3 bg-light">
            <div class="card-body">
              <h6 class="mb-2">{{ '::UpdateStats' | abpLocalization }}: {{ vid.title }}</h6>
              <div class="row g-2 align-items-end">
                <div class="col-md-2">
                  <label class="form-label small">{{ '::Views' | abpLocalization }}</label>
                  <input type="number" min="0" class="form-control form-control-sm" [(ngModel)]="statsForm.viewCount">
                </div>
                <div class="col-md-2">
                  <label class="form-label small">{{ '::Likes' | abpLocalization }}</label>
                  <input type="number" min="0" class="form-control form-control-sm" [(ngModel)]="statsForm.likeCount">
                </div>
                <div class="col-md-2">
                  <label class="form-label small">{{ '::Dislikes' | abpLocalization }}</label>
                  <input type="number" min="0" class="form-control form-control-sm" [(ngModel)]="statsForm.dislikeCount">
                </div>
                <div class="col-md-2">
                  <label class="form-label small">{{ '::Comments' | abpLocalization }}</label>
                  <input type="number" min="0" class="form-control form-control-sm" [(ngModel)]="statsForm.commentCount">
                </div>
                <div class="col-md-4 d-flex gap-2">
                  <button type="button" class="btn btn-sm btn-primary" (click)="saveStats()">{{ '::SaveStats' | abpLocalization }}</button>
                  <button type="button" class="btn btn-sm btn-secondary" (click)="editingStatsVideo = null">{{ '::Cancel' | abpLocalization }}</button>
                </div>
              </div>
            </div>
          </div>
        }
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
  editingStatsVideo: VideoDto | null = null;
  statsForm = { viewCount: 0, likeCount: 0, dislikeCount: 0, commentCount: 0 };

  ngOnInit() {
    this.load();
  }

  openStatsEditor(item: VideoDto): void {
    this.editingStatsVideo = item;
    this.statsForm = {
      viewCount: item.viewCount ?? 0,
      likeCount: item.likeCount ?? 0,
      dislikeCount: item.dislikeCount ?? 0,
      commentCount: item.commentCount ?? 0,
    };
  }

  saveStats(): void {
    if (!this.editingStatsVideo?.id) return;
    this.service.updateStats(this.editingStatsVideo.id, this.statsForm).subscribe({
      next: updated => {
        this.items = this.items.map(v => v.id === updated.id ? updated : v);
        this.editingStatsVideo = null;
        this.toaster.success('::SuccessfullyUpdated');
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
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

  getProviderKey(provider: VideoProvider): string {
    switch (provider) {
      case VideoProvider.YouTube: return '::YouTube';
      case VideoProvider.Vimeo: return '::Vimeo';
      case VideoProvider.Custom: return '::Custom';
      default: return '::Unknown';
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

  watchVideo(item: VideoDto): void {
    const rawUrl = item.url?.trim() ?? '';
    let parsed: URL | null = null;
    try {
      parsed = new URL(rawUrl);
    } catch {
      parsed = null;
    }

    if (!parsed || (parsed.protocol !== 'http:' && parsed.protocol !== 'https:')) {
      this.toaster.error('::InvalidVideoUrl');
      return;
    }

    window.open(parsed.href, '_blank', 'noopener,noreferrer');
  }
}
