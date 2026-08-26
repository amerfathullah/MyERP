import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { VideoSettingsService } from '../../proxy/utilities/video-settings.service';

@Component({
  selector: 'app-video-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Video Settings</h5>
        <a routerLink="/utilities/videos" class="btn btn-secondary btn-sm">Back to Videos</a>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="form-check form-switch mb-3">
            <input class="form-check-input" type="checkbox" id="enableYoutubeTracking" formControlName="enableYoutubeTracking">
            <label class="form-check-label fw-semibold" for="enableYoutubeTracking">Enable YouTube Tracking</label>
          </div>

          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">YouTube Data API Key</label>
              <input type="password" class="form-control" formControlName="apiKey" placeholder="Enter API Key...">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Tracking Sync Frequency</label>
              <select class="form-select" formControlName="frequencyMinutes">
                <option [ngValue]="30">Every 30 minutes</option>
                <option [ngValue]="60">Every 1 hour</option>
                <option [ngValue]="360">Every 6 hours</option>
                <option [ngValue]="1440">Daily</option>
              </select>
            </div>
          </div>

          <button type="submit" class="btn btn-primary">Save Settings</button>
        </form>
      </div>
    </div>
  `
})
export class VideoSettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(VideoSettingsService);
  private toaster = inject(ToasterService);

  form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      enableYoutubeTracking: [false],
      apiKey: [''],
      frequencyMinutes: [60],
    });
  }

  ngOnInit() {
    this.service.get().subscribe(res => {
      this.form.patchValue(res);
    });
  }

  save() {
    this.service.update(this.form.value).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
