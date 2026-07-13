import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="d-flex flex-column align-items-center justify-content-center py-5" style="min-height: 60vh;">
      <i class="fa fa-circle-question fa-5x text-muted mb-4"></i>
      <h1 class="fw-bold text-muted">404</h1>
      <p class="text-muted fs-5 mb-4">The page you are looking for does not exist.</p>
      <a routerLink="/" class="btn btn-primary">
        <i class="fa fa-home me-1"></i> Back to Dashboard
      </a>
    </div>
  `,
})
export class NotFoundComponent {}
