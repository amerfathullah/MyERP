import { mapEnumToOptions } from '@abp/ng.core';

export enum StockEntryType {
  MaterialReceipt = 0,
  MaterialIssue = 1,
  MaterialTransfer = 2,
  MaterialTransferForManufacture = 3,
  Manufacture = 4,
  Repack = 5,
  SendToSubcontractor = 6,
  MaterialConsumptionForManufacture = 7,
  Disassemble = 8,
  SendToWarehouse = 9,
  ReceiveAtWarehouse = 10,
  SubcontractingDelivery = 11,
  SubcontractingReturn = 12,
  Adjustment = 13,
}

export const stockEntryTypeOptions = mapEnumToOptions(StockEntryType);
