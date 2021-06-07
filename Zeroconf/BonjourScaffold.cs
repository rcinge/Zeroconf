using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

#if __IOS__
using UIKit;
#endif

namespace Zeroconf
{
    public static class BonjourScaffold
    {
        public static async Task<IReadOnlyList<IZeroconfHost>> ResolveAsync(string protocol, TimeSpan scanTime = default(TimeSpan))
        {
            IReadOnlyList<IZeroconfHost> results = null;

            try
            {
#if __IOS__
                Debug.WriteLine($"System Version = {UIDevice.CurrentDevice.SystemVersion}");
                if (UIDevice.CurrentDevice.CheckSystemVersion(14, 5))
                {
                    Debug.WriteLine($"Bonjour-based ZeroconfNetServiceBrowser called: System Version >= 14.5");
                    results = await ZeroconfNetServiceBrowser.ResolveAsync(protocol, scanTime);
                }
                else
                {
                    Debug.WriteLine($"Generic ZeroconfResolver called: System Version < 14.5");
                    results = await ZeroconfResolver.ResolveAsync(protocol, scanTime);
                }
#else
                Debug.WriteLine($"Generic ZeroconfResolver called: not iOS/not linked to iOS library");
                results = await ZeroconfResolver.ResolveAsync(protocol, scanTime);
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(ResolveAsync)}: Caught {ex.GetType().Name} Msg {ex.Message} Stack {ex.StackTrace}");
            }

            return results;
        }
    }
}
