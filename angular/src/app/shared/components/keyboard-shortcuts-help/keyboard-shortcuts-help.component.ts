import { Component, inject, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';

interface ShortcutGroup {
  title: string;
  shortcuts: { keys: string; description: string }[];
}

@Component({
  selector: 'app-keyboard-shortcuts-help',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  template: `
    @if (isOpen()) {
      <div class="modal d-block" tabindex="-1" (click)="close()" style="background: rgba(0,0,0,0.5)">
        <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable" (click)="$event.stopPropagation()">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title"><i class="fa fa-keyboard me-2"></i>{{ '::KeyboardShortcuts' | abpLocalization }}</h5>
              <button type="button" class="btn-close" (click)="close()"></button>
            </div>
            <div class="modal-body">
              <div class="row">
                @for (group of shortcutGroups; track group.title) {
                  <div class="col-md-6 mb-4">
                    <h6 class="text-muted fw-bold text-uppercase mb-2" style="font-size: 0.75rem; letter-spacing: 0.05em">{{ group.title | abpLocalization }}</h6>
                    @for (s of group.shortcuts; track s.keys) {
                      <div class="d-flex justify-content-between align-items-center py-1 border-bottom">
                        <span class="text-muted small">{{ s.description | abpLocalization }}</span>
                        <span>
                          @for (key of s.keys.split('+'); track key; let last = $last) {
                            <kbd class="bg-light text-dark border" style="font-size: 0.75rem">{{ key }}</kbd>
                            @if (!last) { <span class="text-muted mx-1">+</span> }
                          }
                        </span>
                      </div>
                    }
                  </div>
                }
              </div>
              <div class="text-center text-muted small mt-2">
                <kbd class="bg-light text-dark border">?</kbd> {{ '::OpenThisDialog' | abpLocalization }}
              </div>
            </div>
          </div>
        </div>
      </div>
    }
  `
})
export class KeyboardShortcutsHelpComponent {
  isOpen = signal(false);

  shortcutGroups: ShortcutGroup[] = [
    {
      title: '::Navigation',
      shortcuts: [
        { keys: 'Ctrl+K', description: '::GlobalSearch' },
        { keys: 'Escape', description: '::CloseDialogOrClearSearch' },
        { keys: '?', description: '::ShowKeyboardShortcuts' },
      ]
    },
    {
      title: '::Forms',
      shortcuts: [
        { keys: 'Ctrl+S', description: '::SaveDocument' },
        { keys: 'Tab', description: '::NextField' },
        { keys: 'Shift+Tab', description: '::PreviousField' },
      ]
    },
    {
      title: '::Lists',
      shortcuts: [
        { keys: 'Enter', description: '::SearchOrApplyFilter' },
        { keys: 'Ctrl+Shift+E', description: '::ExportCSV' },
      ]
    },
    {
      title: '::Documents',
      shortcuts: [
        { keys: 'Ctrl+P', description: '::PrintDocument' },
      ]
    }
  ];

  @HostListener('document:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    if (event.key === '?' && !this.isInputFocused(event)) {
      event.preventDefault();
      this.isOpen.set(!this.isOpen());
    }
    if (event.key === 'Escape' && this.isOpen()) {
      this.close();
    }
  }

  open(): void { this.isOpen.set(true); }
  close(): void { this.isOpen.set(false); }

  private isInputFocused(event: KeyboardEvent): boolean {
    const tag = (event.target as HTMLElement)?.tagName?.toLowerCase();
    return tag === 'input' || tag === 'textarea' || tag === 'select';
  }
}
