import { Component, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-loading-overlay',
  standalone: true,
  imports: [],
  templateUrl: './loading-overlay.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./loading-overlay.component.scss'],
})
export class LoadingOverlayComponent {}
