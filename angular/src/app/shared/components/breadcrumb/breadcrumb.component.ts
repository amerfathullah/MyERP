import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, ActivatedRoute, NavigationEnd } from '@angular/router';
import { filter, map } from 'rxjs';

interface Breadcrumb {
  label: string;
  url: string;
}

@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    @if (breadcrumbs.length > 1) {
      <nav aria-label="breadcrumb" class="mb-3">
        <ol class="breadcrumb mb-0">
          <li class="breadcrumb-item"><a routerLink="/"><i class="fa fa-home"></i></a></li>
          @for (bc of breadcrumbs; track bc.url; let last = $last) {
            @if (last) {
              <li class="breadcrumb-item active" aria-current="page">{{ bc.label }}</li>
            } @else {
              <li class="breadcrumb-item"><a [routerLink]="bc.url">{{ bc.label }}</a></li>
            }
          }
        </ol>
      </nav>
    }
  `,
  styles: [`:host { display: block; } .breadcrumb { font-size: 0.85rem; }`],
})
export class BreadcrumbComponent {
  private router = inject(Router);
  breadcrumbs: Breadcrumb[] = [];

  constructor() {
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(() => this.buildBreadcrumbs())
    ).subscribe(bcs => this.breadcrumbs = bcs);
  }

  private buildBreadcrumbs(): Breadcrumb[] {
    const url = this.router.url.split('?')[0];
    const segments = url.split('/').filter(s => s.length > 0);
    const crumbs: Breadcrumb[] = [];
    let path = '';

    for (const segment of segments) {
      path += '/' + segment;
      // Skip UUID-like segments in display
      if (segment.length === 36 && segment.includes('-')) {
        crumbs.push({ label: 'Detail', url: path });
      } else {
        const label = this.formatLabel(segment);
        crumbs.push({ label, url: path });
      }
    }
    return crumbs;
  }

  private formatLabel(segment: string): string {
    // Convert kebab-case/path segments to Title Case
    return segment
      .replace(/-/g, ' ')
      .replace(/\b\w/g, c => c.toUpperCase())
      .replace(/\bnew\b/i, 'New')
      .replace(/\breports\b/i, 'Reports');
  }
}
