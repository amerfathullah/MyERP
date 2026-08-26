import type { FullAuditedEntityDto, EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { CallDirection } from './call-direction.enum';
import type { CallStatus } from './call-status.enum';
import type { CallRoutingMode } from './call-routing-mode.enum';

export interface TelephonyCallTypeDto extends FullAuditedEntityDto<string> {
  callTypeName: string;
  isActive: boolean;
}

export interface CreateUpdateTelephonyCallTypeDto {
  callTypeName: string;
  isActive: boolean;
}

export interface GetTelephonyCallTypeListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  isActive?: boolean | null;
}

export interface CallLogDto extends FullAuditedEntityDto<string> {
  callId: string;
  from: string;
  to: string;
  callDirection: CallDirection;
  status: CallStatus;
  duration: number;
  startTime?: string | null;
  endTime?: string | null;
  recordingUrl?: string | null;
  medium?: string | null;
  customerId?: string | null;
  employeeUserId?: string | null;
  callReceivedByEmployeeId?: string | null;
  telephonyCallTypeId?: string | null;
  summary?: string | null;
}

export interface CreateCallLogDto {
  callId: string;
  from: string;
  to: string;
  callDirection: CallDirection;
  status: CallStatus;
  startTime?: string | null;
  medium?: string | null;
  customerId?: string | null;
  employeeUserId?: string | null;
  callReceivedByEmployeeId?: string | null;
  telephonyCallTypeId?: string | null;
  summary?: string | null;
}

export interface UpdateCallLogDto {
  status: CallStatus;
  duration: number;
  endTime?: string | null;
  recordingUrl?: string | null;
  customerId?: string | null;
  employeeUserId?: string | null;
  callReceivedByEmployeeId?: string | null;
  telephonyCallTypeId?: string | null;
  summary?: string | null;
}

export interface GetCallLogListDto extends PagedAndSortedResultRequestDto {
  callDirection?: CallDirection | null;
  status?: CallStatus | null;
  telephonyCallTypeId?: string | null;
  customerId?: string | null;
  filter?: string | null;
}

export interface IncomingCallHandlingScheduleDto extends EntityDto<string> {
  incomingCallSettingsId?: string;
  dayOfWeek: number;
  fromTime: string;
  toTime: string;
  employeeGroupId: string;
}

export interface IncomingCallSettingsDto extends FullAuditedEntityDto<string> {
  callRouting: CallRoutingMode;
  greetingMessage?: string | null;
  agentBusyMessage?: string | null;
  agentUnavailableMessage?: string | null;
  schedules: IncomingCallHandlingScheduleDto[];
}

export interface CreateUpdateIncomingCallScheduleDto {
  dayOfWeek: number;
  fromTime: string;
  toTime: string;
  employeeGroupId: string;
}

export interface UpdateIncomingCallSettingsDto {
  callRouting: CallRoutingMode;
  greetingMessage?: string | null;
  agentBusyMessage?: string | null;
  agentUnavailableMessage?: string | null;
  schedules: CreateUpdateIncomingCallScheduleDto[];
}
