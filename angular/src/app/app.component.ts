import { Component } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { KeyboardShortcutsHelpComponent } from './shared/components/keyboard-shortcuts-help/keyboard-shortcuts-help.component';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
    <app-keyboard-shortcuts-help />
  `,
  imports: [LoaderBarComponent, DynamicLayoutComponent, KeyboardShortcutsHelpComponent],
})
export class AppComponent {}
