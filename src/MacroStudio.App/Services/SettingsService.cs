using MacroStudio.Core.Settings;

namespace MacroStudio.App.Services;

/// <summary>Holds the current settings, saves every change and tells the rest of the app about it.</summary>
public sealed class SettingsService(SettingsStore store, AppSettings initial)
{
    public AppSettings Current { get; private set; } = initial;

    public event EventHandler<AppSettings>? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        AppSettings updated = change(Current).Normalize();
        if (updated == Current)
        {
            return;
        }

        Current = updated;
        store.Save(updated);
        Changed?.Invoke(this, updated);
    }
}
