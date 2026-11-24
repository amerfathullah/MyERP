import { Component, ChangeDetectionStrategy } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';

@Component({
  selector: 'app-loading-overlay',
  standalone: true,
  imports: [LocalizationPipe],
  templateUrl: './loading-overlay.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./loading-overlay.component.scss'],
})
export class LoadingOverlayComponent {}
