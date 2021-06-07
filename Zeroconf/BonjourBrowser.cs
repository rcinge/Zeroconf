#if __IOS__
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Foundation;
using UIKit;
using Network;

using Zeroconf;
using ObjCRuntime;

namespace Zeroconf
{
    class BonjourBrowser : NSNetServiceBrowserDelegate
    {
        NSNetServiceBrowser browser = new NSNetServiceBrowser();

        Dictionary<string, NSNetService> discoveredServiceDict = new Dictionary<string, NSNetService>();
        Dictionary<string, ZeroconfHost> zeroconfHostDict = new Dictionary<string, ZeroconfHost>();

        readonly ResolveOptions _options;
        readonly Action<IZeroconfHost> _callback = null;
        readonly CancellationToken _cancellationToken;
        readonly System.Net.NetworkInformation.NetworkInterface[] _netInterfacesToSendRequestOn;

        public BonjourBrowser(ResolveOptions options,
                                    Action<IZeroconfHost> callback = null,
                                    CancellationToken cancellationToken = default(CancellationToken),
                                    System.Net.NetworkInformation.NetworkInterface[] netInterfacesToSendRequestOn = null)
        {
            Init();

            _options = options;
            _callback = callback;
            _cancellationToken = cancellationToken;
            _netInterfacesToSendRequestOn = netInterfacesToSendRequestOn;

            browser.FoundService += Browser_FoundService;
            browser.NotSearched += Browser_NotSearched;
            browser.ServiceRemoved += Browser_ServiceRemoved;
        }

        private void Browser_FoundService(object sender, NSNetServiceEventArgs e)
        {
            NSNetService service = e.Service;

            const double timeout = 5D;

            if (service != null)
            {
                service.Resolve(timeout);

                Debug.WriteLine($"FoundService: Name {service.Name} Type {service.Type} Domain {service.Domain}");
                Debug.WriteLine($"              HostName {service.HostName} Port {service.Port}");

                if (service.TxtRecordData != null)
                {
                    NSDictionary dict = NSNetService.DictionaryFromTxtRecord(service.TxtRecordData);
                    if (dict != null)
                    {
                        if (dict.Count > 0)
                        {
                            foreach (var key in dict.Keys)
                            {
                                Debug.WriteLine($"FoundService: Key {key} Value {dict[key].ToString()}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"FoundService: Service.DictionaryFromTxtRecord has 0 entries");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"FoundService: Service.DictionaryFromTxtRecord returned null");
                    }
                }
                else
                {
                    Debug.WriteLine($"FoundService: TxtRecordData is null");
                }

                string serviceKey = GetNsNetServiceKey(service);
                lock (discoveredServiceDict)
                {
                    discoveredServiceDict[serviceKey] = service;
                }
            }
            else
            {
                Debug.WriteLine($"FoundService: service is null");
            }
        }

        private void Browser_ServiceRemoved(object sender, NSNetServiceEventArgs e)
        {
            NSNetService service = e.Service;

            Debug.WriteLine($"ServiceRemoved: Name {service.Name} Type {service.Type} Domain {service.Domain}");
            Debug.WriteLine($"                HostName {service.HostName} Port {service.Port}");

            string serviceKey = GetNsNetServiceKey(service);
            lock (discoveredServiceDict)
            {
                discoveredServiceDict.Remove(serviceKey);
            }
        }

        private void Browser_NotSearched(object sender, NSNetServiceErrorEventArgs e)
        {
            NSDictionary errors = e.Errors;

            Debug.WriteLine($"NotSearched: Errors {errors?.ToString()}");

            if (errors != null)
            {
                if (errors.Count > 0)
                {
                    foreach (var key in errors.Keys)
                    {
                        Debug.WriteLine($"NotSearched: Key {key} Value {errors[key].ToString()}");
                    }
                }
                else
                {
                    Debug.WriteLine($"NotSearched: errors has 0 entries");
                }
            }
            else
            {
                Debug.WriteLine($"NotSearched: errors is null");
            }
        }

        //
        // Internal API
        //

        public void StartDiscovery()
        {
            const string localDomainForParse = ".local.";
            const string localDomain = "local.";
            int localDomainLength = localDomain.Length;

            // All previous service results are discarded
            lock (discoveredServiceDict)
            {
                discoveredServiceDict.Clear();
            }

            lock (zeroconfHostDict)
            {
                zeroconfHostDict.Clear();
            }

            foreach (var protocol in _options.Protocols)
            {
                string serviceType = string.Empty;
                string domain = string.Empty;

                if (protocol.ToLower().EndsWith(localDomainForParse))
                {
                    serviceType = protocol.Substring(0, protocol.Length - localDomainLength);
                    domain = protocol.Substring(serviceType.Length);
                }
                else
                {
                    serviceType = GetServiceType(protocol);
                    if (serviceType != null)
                    {
                        if (protocol.Length > serviceType.Length)
                        {
                            domain = protocol.Substring(serviceType.Length);

                            //           6 = delim.Length
                            //          /----\ 
                            // _foo._bar._tcp. example.com.
                            // 012345678901234 567890123456 index = [0, 26]
                            // 123456789012345 678901234567 length = 27
                            //   serviceType      domain
                        }
                        else
                        {
                            domain = string.Empty;
                        }
                    }
                    else
                    {
                        serviceType = protocol;
                        domain = string.Empty;
                    }
                }

                Debug.WriteLine($"SearchForServices: Type {serviceType} Domain {domain}");
                browser.SearchForServices(serviceType, domain);
            }
        }

        string GetServiceType(string protocol)
        {
            string serviceType = null;
            string[] delimArray = { "._tcp.", "._udp." };

            foreach (string delim in delimArray)
            {
                if (protocol.Contains(delim))
                {
                    serviceType = protocol.Substring(0, protocol.IndexOf(delim) + delim.Length);
                    break;
                }
            }

            return serviceType;
        }

        public void StopDiscovery()
        {
            Debug.WriteLine($"StopDiscovery");
            browser.Stop();

            List<NSNetService> nsNetServiceList = new List<NSNetService>();
            lock (discoveredServiceDict)
            {
                nsNetServiceList.AddRange(discoveredServiceDict.Values);
            }

            foreach (var discovered in nsNetServiceList)
            {
                NSData txtRecordData = discovered.GetTxtRecordData();
                if (txtRecordData != null)
                {
                    Debug.WriteLine($"StopDiscovery: Name {discovered.Name} Type {discovered.Type} Domain {discovered.Domain}");
                    Debug.WriteLine($"               HostName {discovered.HostName} Port {discovered.Port}");

                    NSDictionary txtDict = NSNetService.DictionaryFromTxtRecord(txtRecordData);
                    if (txtDict != null)
                    {
                        if (txtDict.Count > 0)
                        {
                            foreach (var key in txtDict.Keys)
                            {
                                Debug.WriteLine($"StopDiscovery: Key {key} Value {txtDict[key].ToString()}");
                            }

                            AddZeroconfHostService(discovered, txtDict);
                        }
                        else
                        {
                            Debug.WriteLine($"StopDiscovery: Service.DictionaryFromTxtRecord has 0 entries");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"StopDiscovery: Service.DictionaryFromTxtRecord returned null");
                    }
                }
                else
                {
                    Debug.WriteLine($"StopDiscovery: GetTxtRecordData() returned null");
                }
            }
        }

        string GetNsNetServiceKey(NSNetService service)
        {
            string hostKey = GetZeroconfHostKey(service);
            return $"{hostKey}:{service.Type}{service.Domain}";
        }

        string GetNsNetServiceName(NSNetService service)
        {
            return $"{service.Type}{service.Domain}";
        }

        public IReadOnlyList<IZeroconfHost> ReturnDiscoveryResults()
        {
            List<IZeroconfHost> hostList = GetZeroconfHostList();
            return hostList;
        }

        ZeroconfHost GetZeroconfHost(NSNetService service)
        {
            ZeroconfHost host;
            string hostKey = GetZeroconfHostKey(service);

            lock (zeroconfHostDict)
            {
                if (!zeroconfHostDict.TryGetValue(hostKey, out host))
                {

                    host = new ZeroconfHost();
                    host.DisplayName = service.Name;

                    List<string> ipAddrList = new List<string>();
                    foreach (NSData address in service.Addresses)
                    {
                        Sockaddr saddr = Sockaddr.CreateSockaddr(address.Bytes);
                        IPAddress ipAddr = Sockaddr.CreateIPAddress(saddr);

                        ipAddrList.Add(ipAddr.ToString());
                    }
                    host.IPAddresses = ipAddrList;

                    host.Id = host.IPAddress;

                    zeroconfHostDict[hostKey] = host;
                }
            }

            return host;
        }

        string GetZeroconfHostKey(NSNetService service)
        {
            StringBuilder sb = new StringBuilder();

            foreach (NSData address in service.Addresses)
            {
                Sockaddr saddr = Sockaddr.CreateSockaddr(address.Bytes);
                IPAddress ipAddr = Sockaddr.CreateIPAddress(saddr);

                sb.Append((sb.Length == 0 ? ipAddr.ToString() : $";{ipAddr.ToString()}"));
            }

            return sb.ToString();
        }

        void AddZeroconfHostService(NSNetService service, NSDictionary txtDict)
        {
            ZeroconfHost host = GetZeroconfHost(service);

            Service svc = new Service();
            svc.Name = GetNsNetServiceName(service);
            svc.Port = (int)service.Port;
            // svc.Ttl = is not available

            Dictionary<string, string> propertyDict = new Dictionary<string, string>();

            foreach (var key in txtDict.Keys)
            {
                propertyDict[key.ToString()] = txtDict[key].ToString();
            }
            svc.AddPropertySet(propertyDict);

            host.AddService(svc);
        }

        List<IZeroconfHost> GetZeroconfHostList()
        {
            List<IZeroconfHost> hostList = new List<IZeroconfHost>();

            lock (zeroconfHostDict)
            {
                hostList.AddRange(zeroconfHostDict.Values);
            }

            return hostList;
        }
    }
}
#endif