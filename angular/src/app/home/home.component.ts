import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

interface DashboardCard {
  title: string;
  value: string;
  icon: string;
  color: string;
  link: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule, MatCardModule, MatIconModule, MatButtonModule],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
})
export class HomeComponent {
  private authService = inject(AuthService);

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  cards: DashboardCard[] = [
    { title: 'Revenue (MTD)', value: 'MYR 125,400', icon: 'trending_up', color: 'text-green-600', link: '/accounting/accounts' },
    { title: 'Outstanding Invoices', value: '12', icon: 'receipt_long', color: 'text-blue-600', link: '/sales/invoices' },
    { title: 'Purchase Pending', value: '5', icon: 'shopping_cart', color: 'text-orange-600', link: '/purchasing/invoices' },
    { title: 'e-Invoice Submitted', value: '47', icon: 'cloud_done', color: 'text-purple-600', link: '/e-invoice/dashboard' },
  ];

  quickLinks = [
    { label: 'New Sales Invoice', icon: 'add_circle', route: '/sales/invoices/new' },
    { label: 'New Purchase Invoice', icon: 'add_circle', route: '/purchasing/invoices/new' },
    { label: 'Journal Entry', icon: 'book', route: '/accounting/journal-entries/new' },
    { label: 'LHDN Dashboard', icon: 'dashboard', route: '/e-invoice/dashboard' },
  ];

  login() {
    this.authService.navigateToLogin();
  }
}
