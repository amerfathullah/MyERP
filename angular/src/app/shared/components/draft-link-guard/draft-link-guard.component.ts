import { Component, Input, Output, EventEmitter, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DraftLinkGuardService } from '../../../proxy/core/draft-link-guard.service';
import { LocalizationPipe } from '@abp/ng.core';

/**
 * Shows a warning dialog when a draft document already exists for a conversion.
 * Per ERPNext PR #57299: prevents accidental duplicate document creation.
 *
 * Usage:
 *   <app-draft-link-guard
 *     [sourceDocType]="'SalesOrder'"
 *     [sourceId]="order.id"
 *     [targetDocType]="'DeliveryNote'"
 *     (proceed)="createDeliveryNote()"
 *   />
 *
 * The component checks on init. If no drafts exist, it immediately emits (proceed).
 * If drafts exist, it shows a warning with links to existing drafts and a "Create Anyway" button.
 */
@Component({
  selector: 'app-draft-link-guard',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  template: `
    @if (isChecking()) {
      <div class="text-center py-2">
        <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
        <span class="ms-2 text-muted">{{ '::CheckingExistingDrafts' | abpLocalization }}</span>
      </div>
    }
    @if (existingDrafts().length > 0 && !isChecking()) {
      <div class="alert alert-warning d-flex align-items-start gap-2 mb-3">
        <i class="fa fa-triangle-exclamation mt-1"></i>
        <div class="flex-grow-1">
          <strong>{{ '::DraftAlreadyExists' | abpLocalization }}</strong>
          <p class="mb-2 small">{{ '::DraftLinkGuardMessage' | abpLocalization }}</p>
          <ul class="list-unstyled mb-2">
            @for (draft of existingDrafts(); track draft.documentId) {
              <li>
                <a [routerLink]="draft.url" class="text-decoration-none">
                  <i class="fa fa-file-pen me-1"></i>
                  {{ draft.documentNumber || draft.documentId }}
                </a>
                <span class="badge bg-secondary ms-1">{{ '::Draft' | abpLocalization }}</span>
              </li>
            }
          </ul>
          <div class="d-flex gap-2">
            <button class="btn btn-sm btn-outline-warning" (click)="onProceed()">
              <i class="fa fa-plus me-1"></i>{{ '::CreateAnyway' | abpLocalization }}
            </button>
            <button class="btn btn-sm btn-outline-secondary" (click)="onCancel()">
              {{ '::Cancel' | abpLocalization }}
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class DraftLinkGuardComponent implements OnInit {
  @Input({ required: true }) sourceDocType!: string;
  @Input({ required: true }) sourceId!: string;
  @Input({ required: true }) targetDocType!: string;
  @Input() autoCheck = true;

  /** Emits when user confirms creation (either no drafts found, or clicked "Create Anyway") */
  @Output() proceed = new EventEmitter<void>();
  /** Emits when user cancels the action (clicked "Cancel") */
  @Output() cancelled = new EventEmitter<void>();

  isChecking = signal(false);
  existingDrafts = signal<any[]>([]);
  private draftLinkGuardService = inject(DraftLinkGuardService);

  ngOnInit() {
    if (this.autoCheck && this.sourceId && this.targetDocType) {
      this.checkDrafts();
    }
  }

  checkDrafts() {
    this.isChecking.set(true);
    this.draftLinkGuardService.getExistingDrafts(this.sourceDocType, this.sourceId, this.targetDocType).subscribe({
      next: (drafts) => {
        this.isChecking.set(false);
        if (!drafts || drafts.length === 0) {
          // No existing drafts — safe to proceed immediately
          this.proceed.emit();
        } else {
          this.existingDrafts.set(drafts);
        }
      },
      error: () => {
        // On error, proceed anyway (guard is advisory, not blocking)
        this.isChecking.set(false);
        this.proceed.emit();
      }
    });
  }

  onProceed() {
    this.existingDrafts.set([]);
    this.proceed.emit();
  }

  onCancel() {
    this.existingDrafts.set([]);
    this.cancelled.emit();
  }
}
