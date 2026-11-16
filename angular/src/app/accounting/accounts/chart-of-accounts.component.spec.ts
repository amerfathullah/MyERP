import { describe, it, expect } from 'vitest';
import { FormBuilder, Validators } from '@angular/forms';
import { AccountType } from '../../proxy/accounting/account-type.enum';
import { flattenAccountNodes, parseCoaCsv, type AccountNode } from './chart-of-accounts.component';

describe('Chart of Accounts logic', () => {
  const fb = new FormBuilder();

  describe('parseCoaCsv', () => {
    it('should parse valid CSV rows with header and map AccountType enums', () => {
      const csv = [
        'AccountCode,AccountName,AccountType,IsGroup,ParentCode',
        '1000,Assets,Asset,true,',
        '1100,Cash,Asset,false,1000',
        '2000,Payables,Liability,false,',
        '4000,Sales,Revenue,false,',
        '5000,COGS,Expense,false,',
      ].join('\n');

      const rows = parseCoaCsv(csv);
      expect(rows.length).toBe(5);
      expect(rows[0]).toEqual({
        accountCode: '1000',
        accountName: 'Assets',
        accountType: AccountType.Asset,
        isGroup: true,
        parentCode: null,
      });
      expect(rows[1].isGroup).toBe(false);
      expect(rows[1].parentCode).toBe('1000');
      expect(rows[2].accountType).toBe(AccountType.Liability);
      expect(rows[3].accountType).toBe(AccountType.Revenue);
      expect(rows[4].accountType).toBe(AccountType.Expense);
    });

    it('should skip blank lines and malformed rows', () => {
      const rows = parseCoaCsv('\n  \nBADROW\n1000,Assets,Asset\n');
      expect(rows.length).toBe(1);
      expect(rows[0].accountCode).toBe('1000');
    });
  });

  describe('AccountForm DTO contract', () => {
    it('should require companyId, accountCode, accountName, and numeric accountType', () => {
      const form = fb.group({
        companyId: ['', Validators.required],
        accountCode: ['', [Validators.required, Validators.maxLength(20)]],
        accountName: ['', [Validators.required, Validators.maxLength(128)]],
        accountType: [AccountType.Asset, Validators.required],
        parentAccountId: [null as string | null],
        isGroup: [false],
        isFrozen: [false],
        isActive: [true],
        currency: ['MYR'],
      });

      expect(form.valid).toBe(false);
      form.patchValue({
        companyId: 'comp-1',
        accountCode: '1110',
        accountName: 'Petty Cash',
        accountType: AccountType.Asset,
        parentAccountId: 'acc-root',
      });
      expect(form.valid).toBe(true);
      expect(form.getRawValue().companyId).toBe('comp-1');
      expect(form.getRawValue().parentAccountId).toBe('acc-root');
    });
  });

  describe('flattenAccountNodes', () => {
    it('flattens hierarchical COA nodes for account merge selection', () => {
      const tree: AccountNode[] = [
        {
          id: '1',
          accountCode: '1000',
          accountName: 'Assets',
          accountType: 'Asset',
          isGroup: true,
          children: [
            { id: '2', accountCode: '1100', accountName: 'Cash', accountType: 'Asset', isGroup: false },
            { id: '3', accountCode: '1110', accountName: 'Petty Cash', accountType: 'Asset', isGroup: false },
          ],
        },
      ];

      const flat = flattenAccountNodes(tree);
      expect(flat.map(a => a.accountCode)).toEqual(['1000', '1100', '1110']);
    });
  });
});
