import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { VideoService } from '../../proxy/utilities/video.service';
import { VideoProvider } from '../../proxy/utilities/video-provider.enum';

@Component({
  selector: 'app-video-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Video</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Title *</label>
              <input type="text" class="form-control" formControlName="title" placeholder="e.g. ERP Introduction">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Provider *</label>
              <select class="form-select" formControlName="provider">
                <option [ngValue]="0">YouTube</option>
                <option [ngValue]="1">Vimeo</option>
                <option [ngValue]="2">Custom</option>
              </select>
            </div>
          </div>

          <div class="row">
            <div class="col-md-8 mb-3">
              <label class="form-label">Video URL *</label>
              <input type="text" class="form-control" formControlName="url" placeholder="https://www.youtube.com/watch?v=...">
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">YouTube Video ID</label>
              <input type="text" class="form-control" formControlName="youtubeVideoId" placeholder="e.g. dQw4w9WgXcQ">
            </div>
          </div>

          <div class="row">
            <div class="col-md-4 mb-3">
              <label class="form-label">Publish Date</label>
              <input type="date" class="form-control" formControlName="publishDate">
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">Duration (seconds)</label>
              <input type="number" class="form-control" formControlName="durationSeconds" placeholder="e.g. 300">
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">Thumbnail Image URL</label>
              <input type="text" class="form-control" formControlName="imageUrl" placeholder="https://...">
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Description</label>
            <textarea class="form-control" rows="3" formControlName="description" placeholder="Video summary and notes..."></textarea>
          </div>

          <div class="form-check form-switch mb-4">
            <input class="form-check-input" type="checkbox" id="isActive" formControlName="isActive">
            <label class="form-check-label" for="isActive">Active</label>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/utilities/videos" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class VideoFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(VideoService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(250)]],
      provider: [VideoProvider.YouTube, Validators.required],
      url: ['', [Validators.required, Validators.maxLength(1000)]],
      youtubeVideoId: ['', Validators.maxLength(100)],
      publishDate: [null],
      durationSeconds: [null],
      description: ['', Validators.maxLength(4000)],
      imageUrl: ['', Validators.maxLength(1000)],
      isActive: [true],
    });
  }

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue({
          title: res.title,
          provider: res.provider,
          url: res.url,
          youtubeVideoId: res.youtubeVideoId,
          publishDate: res.publishDate ? res.publishDate.split('T')[0] : null,
          durationSeconds: res.durationSeconds,
          description: res.description,
          imageUrl: res.imageUrl,
          isActive: res.isActive,
        });
      });
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/utilities/videos']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
