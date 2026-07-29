import { Pipe, PipeTransform, inject } from '@angular/core';
import { CompanyContextService } from '../services/company-context.service';

/**
 * Resolves the current company's base currency code.
 * Usage: {{ amount | number:'1.2-2' }} {{ '' | companyCurrency }}
 * Or with a document's own currency: {{ invoice.currencyCode || ('' | companyCurrency) }}
 */
@Pipe({ name: 'companyCurrency', standalone: true, pure: false })
export class CompanyCurrencyPipe implements PipeTransform {
  private ctx = inject(CompanyContextService);

  transform(_value: unknown): string {
    return this.ctx.currentCurrency();
  }
}
