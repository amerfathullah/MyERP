import { Directive, EventEmitter, HostListener, Output } from '@angular/core';

/**
 * Keyboard shortcut directive that emits on Ctrl+S / Cmd+S.
 * Prevents default browser save dialog.
 *
 * Usage:
 *   <form (appSaveShortcut)="save()">...</form>
 */
@Directive({
  selector: '[appSaveShortcut]',
  standalone: true,
})
export class SaveShortcutDirective {
  @Output('appSaveShortcut') saveTriggered = new EventEmitter<void>();

  @HostListener('document:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key === 's') {
      event.preventDefault();
      this.saveTriggered.emit();
    }
  }
}
