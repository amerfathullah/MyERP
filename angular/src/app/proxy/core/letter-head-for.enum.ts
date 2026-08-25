import { mapEnumToOptions } from '@abp/ng.core';

export enum LetterHeadFor {
  DocType = 0,
  Report = 1,
}

export const letterHeadForOptions = mapEnumToOptions(LetterHeadFor);
