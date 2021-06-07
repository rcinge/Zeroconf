#if __IOS__
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Foundation;
using UIKit;
using Network;

using Zeroconf;
using ObjCRuntime;

namespace Zeroconf
{
    public class ZeroconfNetServiceBrowser
    {
        public static Task<IReadOnlyList<IZeroconfHost>> ResolveAsync(string protocol,
                                                      TimeSpan scanTime = default(TimeSpan),
                                                      int retries = 2,
                                                      int retryDelayMilliseconds = 2000,
                                                      Action<IZeroconfHost> callback = null,
                                                      CancellationToken cancellationToken = default(CancellationToken),
                                                      System.Net.NetworkInformation.NetworkInterface[] netInterfacesToSendRequestOn = null)
        {
            if (string.IsNullOrWhiteSpace(protocol))
                throw new ArgumentNullException(nameof(protocol));

            return ResolveAsync(new[] { protocol },
                    scanTime,
                    retries,
                    retryDelayMilliseconds, callback, cancellationToken, netInterfacesToSendRequestOn);
        }

        public static async Task<IReadOnlyList<IZeroconfHost>> ResolveAsync(IEnumerable<string> protocols,
                                                                    TimeSpan scanTime = default(TimeSpan),
                                                                    int retries = 2,
                                                                    int retryDelayMilliseconds = 2000,
                                                                    Action<IZeroconfHost> callback = null,
                                                                    CancellationToken cancellationToken = default(CancellationToken),
                                                                    System.Net.NetworkInformation.NetworkInterface[] netInterfacesToSendRequestOn = null)
        {
            if (retries <= 0) throw new ArgumentOutOfRangeException(nameof(retries));
            if (retryDelayMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(retryDelayMilliseconds));
            if (scanTime == default(TimeSpan))
                scanTime = TimeSpan.FromSeconds(2);

            var options = new ResolveOptions(protocols)
            {
                Retries = retries,
                RetryDelay = TimeSpan.FromMilliseconds(retryDelayMilliseconds),
                ScanTime = scanTime
            };

            return await ResolveAsync(options, callback, cancellationToken, netInterfacesToSendRequestOn).ConfigureAwait(false);
        }

        public static async Task<IReadOnlyList<IZeroconfHost>> ResolveAsync(ResolveOptions options,
                                                            Action<IZeroconfHost> callback = null,
                                                            CancellationToken cancellationToken = default(CancellationToken),
                                                            System.Net.NetworkInformation.NetworkInterface[] netInterfacesToSendRequestOn = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            BonjourBrowser bonjourBrowser = new BonjourBrowser(options, callback, cancellationToken, netInterfacesToSendRequestOn);

            bonjourBrowser.StartDiscovery();

            await Task.Delay(options.ScanTime, cancellationToken)
                .ConfigureAwait(false);

            bonjourBrowser.StopDiscovery();
            
            var results = bonjourBrowser.ReturnDiscoveryResults();

            return results;
        }
    }
}
#endif