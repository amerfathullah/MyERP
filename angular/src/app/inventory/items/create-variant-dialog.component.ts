import { Component, inject, Input, Output, EventEmitter, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ItemService } from '../../proxy/inventory/item.service';
import { ItemAttributeService } from '../../proxy/inventory/item-attribute.service';
import { ToasterService } from '@abp/ng.theme.shared';

interface AttributeInput {
  attributeId: string;
  attributeName: string;
  value: string;
  options: string[];
  isNumeric: boolean;
}

@Component({
  selector: 'app-create-variant-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (visible) {
      <div class="modal show d-block" tabindex="-1" style="background: rgba(0,0,0,0.5);">
        <div class="modal-dialog modal-lg">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title">
                <i class="bi bi-layers me-2"></i>Create Variant from Template
              </h5>
              <button type="button" class="btn-close" (click)="close()"></button>
            </div>
            <div class="modal-body">
              <p class="text-muted small mb-3">
                Select attribute values to create a new variant of <strong>{{ templateName }}</strong>.
                Each unique combination creates a distinct product variant.
              </p>

              @if (loading()) {
                <div class="text-center py-3"><div class="spinner-border text-primary"></div></div>
              } @else if (attributes.length === 0) {
                <div class="alert alert-warning">
                  <i class="bi bi-exclamation-triangle me-2"></i>
                  No item attributes defined. Please create Item Attributes first.
                </div>
              } @else {
                @for (attr of attributes; track attr.attributeId) {
                  <div class="mb-3">
                    <label class="form-label fw-medium">{{ attr.attributeName }}</label>
                    @if (attr.isNumeric) {
                      <input type="text" class="form-control" [(ngModel)]="attr.value"
                        placeholder="Enter numeric value" />
                    } @else if (attr.options.length > 0) {
                      <select class="form-select" [(ngModel)]="attr.value">
                        <option value="">-- Select --</option>
                        @for (opt of attr.options; track opt) {
                          <option [value]="opt">{{ opt }}</option>
                        }
                      </select>
                    } @else {
                      <input type="text" class="form-control" [(ngModel)]="attr.value"
                        placeholder="Enter value" />
                    }
                  </div>
                }
              }
            </div>
            <div class="modal-footer">
              <button class="btn btn-secondary" (click)="close()">Cancel</button>
              <button class="btn btn-primary" (click)="createVariant()"
                [disabled]="saving() || !isValid()">
                <i class="bi bi-plus-lg me-1"></i>Create Variant
              </button>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class CreateVariantDialogComponent implements OnInit {
  @Input() visible = false;
  @Input() templateItemId = '';
  @Input() templateName = '';
  @Output() closed = new EventEmitter<void>();
  @Output() created = new EventEmitter<any>();

  private itemService = inject(ItemService);
  private attributeService = inject(ItemAttributeService);
  private toaster = inject(ToasterService);

  attributes: AttributeInput[] = [];
  loading = signal(true);
  saving = signal(false);

  ngOnInit() {
    if (this.visible) this.loadAttributes();
  }

  loadAttributes() {
    this.loading.set(true);
    this.attributeService.getList().subscribe({
      next: (items: any[]) => {
        this.attributes = (items ?? []).map((attr: any) => ({
          attributeId: attr.id,
          attributeName: attr.name ?? attr.attributeName ?? attr.id,
          value: '',
          options: attr.values?.map((v: any) => v.value ?? v.attributeValue ?? v) ?? [],
          isNumeric: attr.isNumeric ?? false,
        }));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  isValid(): boolean {
    return this.attributes.length > 0 && this.attributes.every(a => a.value.trim() !== '');
  }

  createVariant() {
    if (!this.isValid()) return;
    this.saving.set(true);

    const input = {
      attributes: this.attributes.map(a => ({
        attributeId: a.attributeId,
        value: a.value.trim(),
      })),
    };

    this.itemService.createVariant(this.templateItemId, input).subscribe({
      next: (variant) => {
        this.saving.set(false);
        this.toaster.success(`Variant created: ${variant.itemCode}`);
        this.created.emit(variant);
        this.close();
      },
      error: (err) => {
        this.saving.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Failed to create variant');
      },
    });
  }

  close() {
    this.visible = false;
    this.closed.emit();
  }
}
