using NvAPIWrapper;
using NvAPIWrapper.DRS;
using NvAPIWrapper.Native.Exceptions;
using NvAPIWrapper.Native.General;

namespace Overclutch.Core.Native;

/// <summary>Reads and writes DWORD settings of the NVIDIA driver's global profile.</summary>
public static class NvidiaDrs
{
    private static readonly Lazy<bool> Initialized = new(() =>
    {
        try
        {
            NVIDIA.Initialize();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    });

    public static bool IsAvailable => Initialized.Value;

    /// <summary>Returns the user-set value, or null when the driver default is in effect.</summary>
    public static uint? Read(uint settingId)
    {
        using var session = DriverSettingsSession.CreateAndLoad();
        try
        {
            var setting = session.CurrentGlobalProfile.GetSetting(settingId);
            if (setting.IsCurrentValuePredefined)
                return null;
            return setting.CurrentValue is uint value ? value : null;
        }
        catch (NVIDIAApiException e) when (e.Status == Status.SettingNotFound)
        {
            return null;
        }
    }

    /// <summary>Writes a value, or restores the driver default when <paramref name="value"/> is null.</summary>
    public static void Write(uint settingId, uint? value)
    {
        using var session = DriverSettingsSession.CreateAndLoad();
        var profile = session.CurrentGlobalProfile;
        if (value is { } v)
        {
            profile.SetSetting(settingId, v);
        }
        else
        {
            try { profile.RestoreSettingToDefault(settingId); }
            catch (NVIDIAApiException e) when (e.Status == Status.SettingNotFound) { }
        }
        session.Save();
    }
}
