import type { FullAuditedEntityDto, EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { CommunicationMediumType } from './communication-medium-type.enum';

export interface CommunicationMediumTimeslotDto extends EntityDto<string> {
  communicationMediumId?: string;
  dayOfWeek: number;
  fromTime: string;
  toTime: string;
  employeeGroupId: string;
}

export interface CommunicationMediumDto extends FullAuditedEntityDto<string> {
  communicationMediumType: CommunicationMediumType;
  communicationChannel?: string | null;
  catchAllEmployeeGroupId?: string | null;
  providerSupplierId?: string | null;
  isDisabled: boolean;
  timeslots: CommunicationMediumTimeslotDto[];
}

export interface CreateUpdateCommunicationMediumTimeslotDto {
  dayOfWeek: number;
  fromTime: string;
  toTime: string;
  employeeGroupId: string;
}

export interface CreateUpdateCommunicationMediumDto {
  communicationMediumType: CommunicationMediumType;
  communicationChannel?: string | null;
  catchAllEmployeeGroupId?: string | null;
  providerSupplierId?: string | null;
  isDisabled?: boolean;
  timeslots: CreateUpdateCommunicationMediumTimeslotDto[];
}

export interface GetCommunicationMediumListDto extends PagedAndSortedResultRequestDto {
  communicationMediumType?: CommunicationMediumType | null;
  isDisabled?: boolean | null;
  filter?: string | null;
}
