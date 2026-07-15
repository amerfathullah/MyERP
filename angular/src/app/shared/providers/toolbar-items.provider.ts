import { APP_INITIALIZER, Provider } from '@angular/core';
import { NavItemsService, NavItem } from '@abp/ng.theme.shared';
import { GlobalSearchComponent } from '../components/global-search/global-search.component';
import { CompanySelectorComponent } from '../components/company-selector/company-selector.component';

/**
 * Registers toolbar items (Global Search + Company Selector) into the LeptonX navbar.
 * Uses ABP's NavItemsService to inject components into the top toolbar area.
 */
export const TOOLBAR_ITEMS_PROVIDER: Provider = {
  provide: APP_INITIALIZER,
  useFactory: (navItems: NavItemsService) => () => {
    // Add Global Search to the navbar (order 90 = before user menu)
    navItems.addItems([
      new NavItem({
        id: 'MyERP.GlobalSearch',
        component: GlobalSearchComponent,
        order: 90,
      }),
    ]);

    // Add Company Selector to the navbar (order 80 = before search)
    navItems.addItems([
      new NavItem({
        id: 'MyERP.CompanySelector',
        component: CompanySelectorComponent,
        order: 80,
      }),
    ]);
  },
  deps: [NavItemsService],
  multi: true,
};
