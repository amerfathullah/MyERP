import { mapEnumToOptions } from '@abp/ng.core';

export enum BisectAlgorithm {
  BFS = 1,
  DFS = 2,
}

export const bisectAlgorithmOptions = mapEnumToOptions(BisectAlgorithm);
