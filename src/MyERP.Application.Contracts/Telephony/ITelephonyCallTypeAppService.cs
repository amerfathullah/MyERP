using System;
using Volo.Abp.Application.Services;

namespace MyERP.Telephony;

public interface ITelephonyCallTypeAppService : ICrudAppService<TelephonyCallTypeDto, Guid, GetTelephonyCallTypeListDto, CreateUpdateTelephonyCallTypeDto, CreateUpdateTelephonyCallTypeDto>
{
}
