import { mapEnumToOptions } from '@abp/ng.core';

export enum VideoProvider {
  YouTube = 0,
  Vimeo = 1,
  Custom = 2,
}

export const videoProviderOptions = mapEnumToOptions(VideoProvider);
