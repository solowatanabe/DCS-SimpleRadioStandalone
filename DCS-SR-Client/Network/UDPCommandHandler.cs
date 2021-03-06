﻿using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public class UDPCommandHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private UdpClient _udpCommandListener;
        private readonly SettingsStore _settings = SettingsStore.Instance;
        private volatile bool _stop  = false;

        public void Start()
        {
            StartUDPCommandListener();
        }

        private void StartUDPCommandListener()
        {
            _udpCommandListener = new UdpClient();
            _udpCommandListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpCommandListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            var localEp = new IPEndPoint(IPAddress.Any, _settings.GetNetworkSetting(SettingsKeys.CommandListenerUDP));
            _udpCommandListener.Client.Bind(localEp);

            Task.Factory.StartNew(() =>
            {
                using (_udpCommandListener)
                {
                    while (!_stop)
                    {
                        try
                        {
                            var groupEp = new IPEndPoint(IPAddress.Any,
                            _settings.GetNetworkSetting(SettingsKeys.CommandListenerUDP));
                            var bytes = _udpCommandListener.Receive(ref groupEp);

                            //Logger.Info("Recevied Message from UDP COMMAND INTERFACE: "+ Encoding.UTF8.GetString(
                            //          bytes, 0, bytes.Length));
                            var message =
                                JsonConvert.DeserializeObject<UDPInterfaceCommand>(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));

                            if (message?.Command == UDPInterfaceCommand.UDPCommandType.FREQUENCY)
                            {
                                RadioHelper.UpdateRadioFrequency(message.Frequency, message.RadioId);
                            }
                            else if (message?.Command == UDPInterfaceCommand.UDPCommandType.ACTIVE_RADIO)
                            {
                                RadioHelper.SelectRadio(message.RadioId);
                            }
                            else if (message?.Command == UDPInterfaceCommand.UDPCommandType.TOGGLE_GUARD)
                            {
                                RadioHelper.ToggleGuard(message.RadioId);
                            }
                            else if (message?.Command == UDPInterfaceCommand.UDPCommandType.CHANNEL_UP)
                            {
                                RadioHelper.RadioChannelUp(message.RadioId);
                            }
                            else if (message?.Command == UDPInterfaceCommand.UDPCommandType.CHANNEL_DOWN)
                            {
                                RadioHelper.RadioChannelDown(message.RadioId);
                            }
                            else if (message?.Command == UDPInterfaceCommand.UDPCommandType.SET_VOLUME)
                            {
                                RadioHelper.SetRadioVolume(message.Volume, message.RadioId);
                            }
                            else
                            {
                                Logger.Error("Unknown UDP Command!");
                            }
                        }
                        catch (SocketException e)
                        {
                            // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                            if (!_stop)
                            {
                                Logger.Error(e, "SocketException Handling DCS  Message");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS  Message");
                        }
                    }

                    try
                    {
                        _udpCommandListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS listener ");
                    }
                }
            });
        }

        public void Stop()
        {
            _stop = true;

            try
            {
                _udpCommandListener?.Close();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
